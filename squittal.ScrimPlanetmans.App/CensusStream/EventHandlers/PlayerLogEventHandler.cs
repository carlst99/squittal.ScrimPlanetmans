using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using squittal.ScrimPlanetmans.App.Models.Planetside.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusStream.EventHandlers;

public class PlayerLogEventHandler : IPayloadHandler<IPlayerLogin>, IPayloadHandler<IPlayerLogout>
{
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchScorer _scorer;

    public PlayerLogEventHandler
    (
        IScrimTeamsManager teamsManager,
        IScrimMessageBroadcastService messageService,
        IScrimMatchScorer scorer
    )
    {
        _teamsManager = teamsManager;
        _messageService = messageService;
        _scorer = scorer;
    }

    /// <inheritdoc />
    public Task HandleAsync(IPlayerLogin payload, CancellationToken ct = default)
    {
        Player? player = _teamsManager.GetPlayerFromId(payload.CharacterID);

        // TODO: use ScrimActionLoginEvent instead of PlayerLogin

        PlayerLogin dataModel = new()
        {
            CharacterId = payload.CharacterID,
            Timestamp = payload.Timestamp.UtcDateTime,
            WorldId = (int)payload.WorldID
        };

        _scorer.HandlePlayerLogin(dataModel);
        if (player is not null)
            _messageService.BroadcastPlayerLoginMessage(new PlayerLoginMessage(player, dataModel));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(IPlayerLogout payload, CancellationToken ct = default)
    {
        Player? player = _teamsManager.GetPlayerFromId(payload.CharacterID);

        // TODO: use ScrimActionLogoutEvent instead of PlayerLogout

        PlayerLogout dataModel = new()
        {
            CharacterId = payload.CharacterID,
            Timestamp = payload.Timestamp.UtcDateTime,
            WorldId = (int)payload.WorldID
        };

        _scorer.HandlePlayerLogout(dataModel);
        if (player is not null)
            _messageService.BroadcastPlayerLogoutMessage(new PlayerLogoutMessage(player, dataModel));

        return Task.CompletedTask;
    }
}
