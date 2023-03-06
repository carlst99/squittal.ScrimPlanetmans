using System;
using squittal.ScrimPlanetmans.App.Logging;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch;

public class ScrimMessageBroadcastService : IScrimMessageBroadcastService, IDisposable
{
    private bool _isDisposed;
    private LogFileWriter? _logFileWriter;

    public bool IsLoggingEnabled { get; private set; }

    #region Handler Events
    public event EventHandler<SimpleMessageEventArgs>? RaiseSimpleMessageEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamPlayerChangeMessage>>? RaiseTeamPlayerChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamOutfitChangeMessage>>? RaiseTeamOutfitChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamConstructedTeamChangeMessage>>? RaiseTeamConstructedTeamChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamAliasChangeMessage>>? RaiseTeamAliasChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamFactionChangeMessage>>? RaiseTeamFactionChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamLockStatusChangeMessage>>? RaiseTeamLockStatusChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<PlayerNameDisplayChangeMessage>>? RaisePlayerNameDisplayChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<PlayerStatUpdateMessage>>? RaisePlayerStatUpdateEvent;
    public event EventHandler<ScrimMessageEventArgs<TeamStatUpdateMessage>>? RaiseTeamStatUpdateEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimKillfeedEventMessage>>? RaiseScrimKillfeedEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimDeathActionEventMessage>>? RaiseScrimDeathActionEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimVehicleDestructionActionEventMessage>>? RaiseScrimVehicleDestructionActionEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimReviveActionEventMessage>>? RaiseScrimReviveActionEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimAssistActionEventMessage>>? RaiseScrimAssistActionEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimObjectiveTickActionEventMessage>>? RaiseScrimObjectiveTickActionEvent;
    public event EventHandler<ScrimMessageEventArgs<ScrimFacilityControlActionEventMessage>>? RaiseScrimFacilityControlActionEvent;
    public event EventHandler<ScrimMessageEventArgs<PlayerLoginMessage>>? RaisePlayerLoginEvent;
    public event EventHandler<ScrimMessageEventArgs<PlayerLogoutMessage>>? RaisePlayerLogoutEvent;
    public event EventHandler<ScrimMessageEventArgs<MatchStateUpdateMessage>>? RaiseMatchStateUpdateEvent;
    public event EventHandler<ScrimMessageEventArgs<MatchConfigurationUpdateMessage>>? RaiseMatchConfigurationUpdateEvent;
    public event EventHandler<ScrimMessageEventArgs<MatchTimerTickMessage>>? RaiseMatchTimerTickEvent;
    public event EventHandler<ScrimMessageEventArgs<ConstructedTeamMemberChangeMessage>>? RaiseConstructedTeamMemberChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<ConstructedTeamInfoChangeMessage>>? RaiseConstructedTeamInfoChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<ActiveRulesetChangeMessage>>? RaiseActiveRulesetChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<RulesetSettingChangeMessage>>? RaiseRulesetSettingChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<RulesetRuleChangeMessage>>? RaiseRulesetRuleChangeEvent;
    public event EventHandler<ScrimMessageEventArgs<RulesetOverlayConfigurationChangeMessage>>? RaiseRulesetOverlayConfigurationChangeEvent;

    #endregion Handler Events & Delegates

    #region Logging
    public void DisableLogging()
    {
        IsLoggingEnabled = false;
    }
    public void EnableLogging()
    {
        IsLoggingEnabled = true;
    }

    public void SetLogFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        _logFileWriter?.Dispose();
        _logFileWriter = new LogFileWriter(fileName);
    }


    private void TrySaveToLogFile(string message)
    {
        if (!IsLoggingEnabled || _logFileWriter is null)
            return;

        string timestamp = DateTime.Now.ToLongTimeString();
        _logFileWriter.Write($"{timestamp}: {message}");
    }
    #endregion Loggin

    #region Match State Change
    public void BroadcastMatchStateUpdateMessage(MatchStateUpdateMessage message)
    {
        OnRaiseMatchStateUpdateEvent(new ScrimMessageEventArgs<MatchStateUpdateMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseMatchStateUpdateEvent(ScrimMessageEventArgs<MatchStateUpdateMessage> e)
    {
        RaiseMatchStateUpdateEvent?.Invoke(this, e);
    }

    public void BroadcastMatchConfigurationUpdateMessage(MatchConfigurationUpdateMessage message)
    {
        OnRaiseMatchConfigurationUpdateEvent(new ScrimMessageEventArgs<MatchConfigurationUpdateMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseMatchConfigurationUpdateEvent(ScrimMessageEventArgs<MatchConfigurationUpdateMessage> e)
    {
        RaiseMatchConfigurationUpdateEvent?.Invoke(this, e);
    }
    #endregion Match State Change

    #region Match Timer Tick
    public void BroadcastMatchTimerTickMessage(MatchTimerTickMessage message)
    {
        OnRaiseMatchTimerTickEvent(new ScrimMessageEventArgs<MatchTimerTickMessage>(message));
    }
    protected virtual void OnRaiseMatchTimerTickEvent(ScrimMessageEventArgs<MatchTimerTickMessage> e)
    {
        RaiseMatchTimerTickEvent?.Invoke(this, e);
    }
    #endregion Match Timer Tick

    #region Player Login / Logout
    public void BroadcastPlayerLoginMessage(PlayerLoginMessage message)
    {
        OnRaisePlayerLoginEvent(new ScrimMessageEventArgs<PlayerLoginMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaisePlayerLoginEvent(ScrimMessageEventArgs<PlayerLoginMessage> e)
    {
        RaisePlayerLoginEvent?.Invoke(this, e);
    }

    public void BroadcastPlayerLogoutMessage(PlayerLogoutMessage message)
    {
        OnRaisePlayerLogoutEvent(new ScrimMessageEventArgs<PlayerLogoutMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaisePlayerLogoutEvent(ScrimMessageEventArgs<PlayerLogoutMessage> e)
    {
        RaisePlayerLogoutEvent?.Invoke(this, e);
    }
    #endregion Player Login / Logout

    #region Player Stat Update & Scrim Events
    public void BroadcastPlayerStatUpdateMessage(PlayerStatUpdateMessage message)
    {
        OnRaisePlayerStatUpdateEvent(new ScrimMessageEventArgs<PlayerStatUpdateMessage>(message));
    }
    protected virtual void OnRaisePlayerStatUpdateEvent(ScrimMessageEventArgs<PlayerStatUpdateMessage> e)
    {
        RaisePlayerStatUpdateEvent?.Invoke(this, e);
    }

    public void BroadcastTeamStatUpdateMessage(TeamStatUpdateMessage message)
    {
        OnRaiseTeamStatUpdateEvent(new ScrimMessageEventArgs<TeamStatUpdateMessage>(message));
    }
    protected virtual void OnRaiseTeamStatUpdateEvent(ScrimMessageEventArgs<TeamStatUpdateMessage> e)
    {
        RaiseTeamStatUpdateEvent?.Invoke(this, e);
    }

    public void BroadcastScrimKillfeedEventMessage(ScrimKillfeedEventMessage message)
    {
        OnRaiseScrimKillfeedEventEvent(new ScrimMessageEventArgs<ScrimKillfeedEventMessage>(message));
    }
    protected virtual void OnRaiseScrimKillfeedEventEvent(ScrimMessageEventArgs<ScrimKillfeedEventMessage> e)
    {
        RaiseScrimKillfeedEvent?.Invoke(this, e);
    }


    public void BroadcastScrimDeathActionEventMessage(ScrimDeathActionEventMessage message)
    {
        OnRaiseScrimDeathActionEvent(new ScrimMessageEventArgs<ScrimDeathActionEventMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseScrimDeathActionEvent(ScrimMessageEventArgs<ScrimDeathActionEventMessage> e)
    {
        RaiseScrimDeathActionEvent?.Invoke(this, e);
    }

    public void BroadcastScrimVehicleDestructionActionEventMessage(ScrimVehicleDestructionActionEventMessage message)
    {
        OnRaiseScrimVehicleDestructionActionEvent(new ScrimMessageEventArgs<ScrimVehicleDestructionActionEventMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseScrimVehicleDestructionActionEvent(ScrimMessageEventArgs<ScrimVehicleDestructionActionEventMessage> e)
    {
        RaiseScrimVehicleDestructionActionEvent?.Invoke(this, e);
    }

    public void BroadcastScrimReviveActionEventMessage(ScrimReviveActionEventMessage message)
    {
        OnRaiseScrimReviveActionEvent(new ScrimMessageEventArgs<ScrimReviveActionEventMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseScrimReviveActionEvent(ScrimMessageEventArgs<ScrimReviveActionEventMessage> e)
    {
        RaiseScrimReviveActionEvent?.Invoke(this, e);
    }

    public void BroadcastScrimAssistActionEventMessage(ScrimAssistActionEventMessage message)
    {
        OnRaiseScrimAssistActionEvent(new ScrimMessageEventArgs<ScrimAssistActionEventMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseScrimAssistActionEvent(ScrimMessageEventArgs<ScrimAssistActionEventMessage> e)
    {
        RaiseScrimAssistActionEvent?.Invoke(this, e);
    }

    public void BroadcastScrimObjectiveTickActionEventMessage(ScrimObjectiveTickActionEventMessage message)
    {
        OnRaiseScrimObjectiveTickActionEvent(new ScrimMessageEventArgs<ScrimObjectiveTickActionEventMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseScrimObjectiveTickActionEvent(ScrimMessageEventArgs<ScrimObjectiveTickActionEventMessage> e)
    {
        RaiseScrimObjectiveTickActionEvent?.Invoke(this, e);
    }

    public void BroadcastScrimFacilityControlActionEventMessage(ScrimFacilityControlActionEventMessage message)
    {
        OnRaiseScrimFacilityControlActionEvent(new ScrimMessageEventArgs<ScrimFacilityControlActionEventMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseScrimFacilityControlActionEvent(ScrimMessageEventArgs<ScrimFacilityControlActionEventMessage> e)
    {
        RaiseScrimFacilityControlActionEvent?.Invoke(this, e);
    }
    #endregion Player Stat Update & Scrim Events

    #region Simple (string) Message
    public void BroadcastSimpleMessage(string message)
    {
        OnRaiseSimpleMessageEvent(new SimpleMessageEventArgs(message));

        TrySaveToLogFile(message);
    }
    protected virtual void OnRaiseSimpleMessageEvent(SimpleMessageEventArgs e)
    {
        RaiseSimpleMessageEvent?.Invoke(this, e);
    }
    #endregion Simple (string) Message


    #region Team Player/Outfit/Constructed Team Changes
    public void BroadcastTeamPlayerChangeMessage(TeamPlayerChangeMessage message)
    {
        OnRaiseTeamPlayerChangeEvent(new ScrimMessageEventArgs<TeamPlayerChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseTeamPlayerChangeEvent(ScrimMessageEventArgs<TeamPlayerChangeMessage> e)
    {
        RaiseTeamPlayerChangeEvent?.Invoke(this, e);
    }

    public void BroadcastTeamOutfitChangeMessage(TeamOutfitChangeMessage message)
    {
        OnRaiseTeamOutfitChangeEvent(new ScrimMessageEventArgs<TeamOutfitChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseTeamOutfitChangeEvent(ScrimMessageEventArgs<TeamOutfitChangeMessage> e)
    {
        RaiseTeamOutfitChangeEvent?.Invoke(this, e);
    }

    public void BroadcastTeamConstructedTeamChangeMessage(TeamConstructedTeamChangeMessage message)
    {
        OnRaiseTeamConstructedTeamChangeEvent(new ScrimMessageEventArgs<TeamConstructedTeamChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseTeamConstructedTeamChangeEvent(ScrimMessageEventArgs<TeamConstructedTeamChangeMessage> e)
    {
        RaiseTeamConstructedTeamChangeEvent?.Invoke(this, e);
    }
    #endregion Team Player/Outfit/Constructed Team Changes

    public void BroadcastTeamAliasChangeMessage(TeamAliasChangeMessage message)
    {
        OnRaiseTeamAliasChangeEvent(new ScrimMessageEventArgs<TeamAliasChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseTeamAliasChangeEvent(ScrimMessageEventArgs<TeamAliasChangeMessage> e)
    {
        RaiseTeamAliasChangeEvent?.Invoke(this, e);
    }

    public void BroadcastTeamFactionChangeMessage(TeamFactionChangeMessage message)
    {
        OnRaiseTeamFactionChangeEvent(new ScrimMessageEventArgs<TeamFactionChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseTeamFactionChangeEvent(ScrimMessageEventArgs<TeamFactionChangeMessage> e)
    {
        RaiseTeamFactionChangeEvent?.Invoke(this, e);
    }

    public void BroadcastTeamLockStatusChangeMessage(TeamLockStatusChangeMessage message)
    {
        OnRaiseTeamLockStatusChangeEvent(new ScrimMessageEventArgs<TeamLockStatusChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseTeamLockStatusChangeEvent(ScrimMessageEventArgs<TeamLockStatusChangeMessage> e)
    {
        RaiseTeamLockStatusChangeEvent?.Invoke(this, e);
    }

    public void BroadcastPlayerNameDisplayChangeMessage(PlayerNameDisplayChangeMessage message)
    {
        OnRaisePlayerNameDisplayChangeEvent(new ScrimMessageEventArgs<PlayerNameDisplayChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaisePlayerNameDisplayChangeEvent(ScrimMessageEventArgs<PlayerNameDisplayChangeMessage> e)
    {
        RaisePlayerNameDisplayChangeEvent?.Invoke(this, e);
    }


    #region Constructed Team Messages
    public void BroadcastConstructedTeamMemberChangeMessage(ConstructedTeamMemberChangeMessage message)
    {
        OnRaiseConstructedTeamMemberChangeEvent(new ScrimMessageEventArgs<ConstructedTeamMemberChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseConstructedTeamMemberChangeEvent(ScrimMessageEventArgs<ConstructedTeamMemberChangeMessage> e)
    {
        RaiseConstructedTeamMemberChangeEvent?.Invoke(this, e);
    }

    public void BroadcastConstructedTeamInfoChangeMessage(ConstructedTeamInfoChangeMessage message)
    {
        OnRaiseConstructedTeamInfoChangeEvent(new ScrimMessageEventArgs<ConstructedTeamInfoChangeMessage>(message));

        TrySaveToLogFile(message.Info);
    }
    protected virtual void OnRaiseConstructedTeamInfoChangeEvent(ScrimMessageEventArgs<ConstructedTeamInfoChangeMessage> e)
    {
        RaiseConstructedTeamInfoChangeEvent?.Invoke(this, e);
    }

    #endregion Constructed Team Messages

    #region Ruleset Messages
    public void BroadcastActiveRulesetChangeMessage(ActiveRulesetChangeMessage message)
    {
        OnRaiseActiveRulesetChangeEvent(new ScrimMessageEventArgs<ActiveRulesetChangeMessage>(message));
    }
    protected virtual void OnRaiseActiveRulesetChangeEvent(ScrimMessageEventArgs<ActiveRulesetChangeMessage> e)
    {
        RaiseActiveRulesetChangeEvent?.Invoke(this, e);
    }

    public void BroadcastRulesetSettingChangeMessage(RulesetSettingChangeMessage message)
    {
        OnRaiseRulesetSettingChangeEvent(new ScrimMessageEventArgs<RulesetSettingChangeMessage>(message));
    }
    protected virtual void OnRaiseRulesetSettingChangeEvent(ScrimMessageEventArgs<RulesetSettingChangeMessage> e)
    {
        RaiseRulesetSettingChangeEvent?.Invoke(this, e);
    }

    public void BroadcastRulesetOverlayConfigurationChangeMessage(RulesetOverlayConfigurationChangeMessage message)
    {
        OnRaiseRulesetOverlayConfigurationChangeEvent(new ScrimMessageEventArgs<RulesetOverlayConfigurationChangeMessage>(message));
    }
    protected virtual void OnRaiseRulesetOverlayConfigurationChangeEvent(ScrimMessageEventArgs<RulesetOverlayConfigurationChangeMessage> e)
    {
        RaiseRulesetOverlayConfigurationChangeEvent?.Invoke(this, e);
    }

    public void BroadcastRulesetRuleChangeMessage(RulesetRuleChangeMessage message)
    {
        OnRaiseRulesetRuleChangeEvent(new ScrimMessageEventArgs<RulesetRuleChangeMessage>(message));
    }
    protected virtual void OnRaiseRulesetRuleChangeEvent(ScrimMessageEventArgs<RulesetRuleChangeMessage> e)
    {
        RaiseRulesetRuleChangeEvent?.Invoke(this, e);
    }
    #endregion Ruleset Messages

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposeManaged)
    {
        if (_isDisposed)
            return;

        if (disposeManaged)
            _logFileWriter?.Dispose();

        _isDisposed = true;
    }
}
