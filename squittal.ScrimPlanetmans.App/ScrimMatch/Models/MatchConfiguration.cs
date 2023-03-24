using System;
using System.Threading;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Services.Rulesets;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class MatchConfiguration
{
    public string Title { get; set; } = "PS2 Scrims";

    public bool IsManualTitle { get; private set; }

    public int RoundSecondsTotal { get; set; } = 900;
    public bool IsManualRoundSecondsTotal { get; private set; }

    // Target Base Configuration
    public bool IsManualWorldId { get; private set; }
    public bool IsWorldIdSet { get; private set; }
    public WorldDefinition WorldId { get; set; } = WorldDefinition.Jaeger;
    public uint FacilityId => GetFacilityIdFromString();
    public string FacilityIdString { get; set; } = "-1";

    public bool EndRoundOnFacilityCapture { get; set; } // TODO: move this setting to the Ruleset model
    public bool IsManualEndRoundOnFacilityCapture { get; set; }

    private readonly AutoResetEvent _autoEvent = new(true);
    private readonly AutoResetEvent _autoEventRoundSeconds = new(true);
    private readonly AutoResetEvent _autoEventMatchTitle = new(true);
    private readonly AutoResetEvent _autoEndRoundOnFacilityCapture = new(true);

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
        WorldId = WorldDefinition.Jaeger;
        IsManualWorldId = false;
        IsWorldIdSet = false;
    }

    public bool TrySetWorldId(string worldIdString, bool isManualValue = false, bool isRollback = false)
        => Enum.TryParse(worldIdString, out WorldDefinition worldId)
            && TrySetWorldId(worldId, isManualValue, isRollback);

    public bool TrySetWorldId(WorldDefinition worldId, bool isManualValue = false, bool isRollBack = false)
    {
        _autoEvent.WaitOne();

        if (isManualValue)
        {
            WorldId = worldId;
            IsManualWorldId = true;
            IsWorldIdSet = true;

            _autoEvent.Set();

            return true;
        }
        else if (!IsManualWorldId && (!IsWorldIdSet || isRollBack))
        {
            WorldId = worldId;

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
}
