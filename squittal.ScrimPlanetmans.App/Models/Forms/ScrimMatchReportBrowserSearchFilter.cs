﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.Forms;

public class ScrimMatchReportBrowserSearchFilter
{
    public DateTime? SearchStartDate { get; set; } = _defaultSearchStartDate; // new DateTime(2012,11, 20); // PlanetSide 2 release date
    public DateTime? SearchEndDate { get; set; } = _defaultSearchEndDate; //DateTime.UtcNow.AddDays(1);

    public int RulesetId { get => GetRulesetIdFromString(); }
    public string RulesetIdString { get; set; } = _defaultRulesetIdString;

    public WorldDefinition WorldId { get; set; }

    public int FacilityId { get => GetFacilityIdFromString(); }
    public string FacilityIdString { get; set; } = _defaultSearchFacilityIdString; //"0";

    public int MinimumRoundCount { get; set; } = _defaultSearchMinimumRoundCount; //2;

    public string InputSearchTerms { get; set; } = _defaultSearchInputTerms; // string.Empty;

    public List<string> SearchTermsList { get; private set; } = new List<string>();
    public List<string> AliasSearchTermsList { get; private set; } = new List<string>();

    private readonly AutoResetEvent _searchTermsAutoEvent = new AutoResetEvent(true);
    private readonly AutoResetEvent _worldAutoEvent = new AutoResetEvent(true);
    private readonly AutoResetEvent _facilityAutoEvent = new AutoResetEvent(true);
    private readonly AutoResetEvent _rulesetAutoEvent = new AutoResetEvent(true);

    private static readonly DateTime _defaultSearchStartDate = new DateTime(2012, 11, 20); // PlanetSide 2 release date
    private static readonly DateTime _defaultSearchEndDate = DateTime.UtcNow.AddDays(1);
    private static readonly WorldDefinition _defaultSearchWorldId = WorldDefinition.Jaeger;
    private static readonly string _defaultSearchFacilityIdString = "0"; // Any Facility
    private static readonly int _defaultSearchMinimumRoundCount = 2;
    private static readonly string _defaultSearchInputTerms = string.Empty;
    private static readonly string _defaultRulesetIdString = "0"; // Any Ruleset


    public bool IsDefaultFilter => GetIsDefaultFilter();

    private static Regex TeamAliasRegex { get; } = new Regex("^[A-Za-z0-9]{1,4}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private bool GetIsDefaultFilter()
    {
        return MinimumRoundCount == _defaultSearchMinimumRoundCount
            && WorldId == _defaultSearchWorldId
            && FacilityIdString == _defaultSearchFacilityIdString
            && SearchStartDate == _defaultSearchStartDate && SearchEndDate == _defaultSearchEndDate
            && InputSearchTerms == _defaultSearchInputTerms
            && !SearchTermsList.Any() && !AliasSearchTermsList.Any()
            && RulesetIdString == _defaultRulesetIdString;
    }

    public void ParseSearchTermsString()
    {
        _searchTermsAutoEvent.WaitOne();

        var searchTerms = InputSearchTerms;

        SearchTermsList = new List<string>();
        AliasSearchTermsList = new List<string>();

        if (string.IsNullOrWhiteSpace(searchTerms))
        {
            _searchTermsAutoEvent.Set();
            return;
        }

        var splitTerms = searchTerms.Split(' ');

        foreach (var term in splitTerms)
        {
            var termLower = term.ToLower();

            if (TeamAliasRegex.Match(termLower).Success && !AliasSearchTermsList.Contains(termLower) && termLower != "vs" && termLower != "ps2")
            {
                AliasSearchTermsList.Add(termLower);
            }
            if (!SearchTermsList.Contains(termLower) && termLower.Length > 1)
            {
                SearchTermsList.Add(termLower);
            }
        }

        _searchTermsAutoEvent.Set();
    }

    public void SetWorldId(WorldDefinition worldId)
    {
        _worldAutoEvent.WaitOne();
        WorldId = worldId;
        _worldAutoEvent.Set();
    }

    public void SetRulesetId(string rulesetIdString)
    {
        _rulesetAutoEvent.WaitOne();

        RulesetIdString = rulesetIdString;

        _rulesetAutoEvent.Set();
    }

    public bool SetFacilityId(int facilityId)
    {
        if (facilityId <= 0)
        {
            return false;
        }

        SetFacilityId(facilityId.ToString());

        return true;
    }

    public void SetFacilityId(string facilityIdString)
    {
        _facilityAutoEvent.WaitOne();

        FacilityIdString = facilityIdString;
        _facilityAutoEvent.Set();
    }

    private int GetFacilityIdFromString()
    {
        if (int.TryParse(FacilityIdString, out int intId))
        {
            return intId;
        }
        else
        {
            return -1;
        }
    }

    private int GetRulesetIdFromString()
    {
        if (int.TryParse(RulesetIdString, out int intId))
        {
            return intId;
        }
        else
        {
            return 0; // Default to Any Ruleset
        }
    }
}
