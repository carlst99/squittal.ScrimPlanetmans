﻿using System.Threading;
using squittal.ScrimPlanetmans.App.Services.Rulesets;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class MatchConfiguration
{
    public string Title { get; set; } = "PS2 Scrims";

    public bool IsManualTitle { get; private set; } = false;

    public int RoundSecondsTotal { get; set; } = 900;
    public bool IsManualRoundSecondsTotal { get; private set; } = false;

    // Target Base Configuration
    public bool IsManualWorldId { get; private set; } = false;
    public bool IsWorldIdSet { get; private set; } = false;
    public int WorldId { get => GetWorldIdFromString(); }
    public string WorldIdString { get; set; } = "19";
    public uint FacilityId => GetFacilityIdFromString();
    public string FacilityIdString { get; set; } = "-1";

    public bool EndRoundOnFacilityCapture { get; set; } = false; // TODO: move this setting to the Ruleset model
    public bool IsManualEndRoundOnFacilityCapture { get; set; } = false;

    private readonly AutoResetEvent _autoEvent = new AutoResetEvent(true);
    private readonly AutoResetEvent _autoEventRoundSeconds = new AutoResetEvent(true);
    private readonly AutoResetEvent _autoEventMatchTitle = new AutoResetEvent(true);
    private readonly AutoResetEvent _autoEndRoundOnFacilityCapture = new AutoResetEvent(true);

    public bool SaveLogFiles { get; set; } = true;
    public bool SaveEventsToDatabase { get; set; } = true;

    public bool TrySetTitle(string title, bool isManualValue)
    {
        if (!RulesetDataService.IsValidRulesetDefaultMatchTitle(title))
        {
            return false;
        }

        _autoEventMatchTitle.WaitOne();

        if (isManualValue)
        {
            Title = title;
            IsManualTitle = true;

            _autoEventMatchTitle.Set();

            return true;
        }
        else if (!IsManualTitle)
        {
            Title = title;

            _autoEventMatchTitle.Set();

            return true;
        }
        else
        {
            _autoEventMatchTitle.Set();

            return false;
        }
    }

    public bool TrySetRoundLength(int seconds, bool isManualValue)
    {
        if (seconds <= 0)
        {
            return false;
        }

        _autoEventRoundSeconds.WaitOne();

        if (isManualValue)
        {
            RoundSecondsTotal = seconds;
            IsManualRoundSecondsTotal = true;

            _autoEventRoundSeconds.Set();

            return true;
        }
        else if (!IsManualRoundSecondsTotal)
        {
            RoundSecondsTotal = seconds;

            _autoEventRoundSeconds.Set();

            return true;
        }
        else
        {
            _autoEventRoundSeconds.Set();

            return false;
        }
    }

    public bool TrySetEndRoundOnFacilityCapture(bool endOnCapture, bool isManualValue)
    {
        _autoEndRoundOnFacilityCapture.WaitOne();

        if (isManualValue)
        {
            EndRoundOnFacilityCapture = endOnCapture;
            IsManualEndRoundOnFacilityCapture = true;

            _autoEndRoundOnFacilityCapture.Set();

            return true;
        }
        else if (!IsManualEndRoundOnFacilityCapture)
        {
            EndRoundOnFacilityCapture = endOnCapture;

            _autoEndRoundOnFacilityCapture.Set();

            return true;
        }
        else
        {
            _autoEndRoundOnFacilityCapture.Set();

            return false;
        }
    }

    public void ResetEndRoundOnFacilityCapture()
    {
        EndRoundOnFacilityCapture = false;
        IsManualEndRoundOnFacilityCapture = false;
    }

    public void ResetWorldId()
    {
        WorldIdString = "19";
        IsManualWorldId = false;
        IsWorldIdSet = false;
    }

    public bool TrySetWorldId(int worldId, bool isManualValue = false, bool isRollBack = false)
    {
        if (worldId <= 0)
        {
            return false;
        }
        return TrySetWorldId(worldId.ToString(), isManualValue, isRollBack);
    }

    public bool TrySetWorldId(string worldIdString, bool isManualValue = false, bool isRollBack = false)
    {
        _autoEvent.WaitOne();

        if (isManualValue)
        {
            WorldIdString = worldIdString;
            IsManualWorldId = true;
            IsWorldIdSet = true;

            _autoEvent.Set();

            return true;
        }
        else if (!IsManualWorldId && (!IsWorldIdSet || isRollBack))
        {
            WorldIdString = worldIdString;

            IsWorldIdSet = true;

            _autoEvent.Set();

            return true;
        }
        else
        {
            _autoEvent.Set();

            return false;
        }
    }

    private uint GetFacilityIdFromString()
        => uint.TryParse(FacilityIdString, out uint facilityId)
            ? facilityId
            : 0;

    private int GetWorldIdFromString()
    {
        if (int.TryParse(WorldIdString, out int intId))
        {
            return intId;
        }
        else
        {
            return 19; // Default to Jaeger
        }
    }
}
