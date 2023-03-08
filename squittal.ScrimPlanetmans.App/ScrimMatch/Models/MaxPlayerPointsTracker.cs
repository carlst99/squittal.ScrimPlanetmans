using System.Threading;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class MaxPlayerPointsTracker
{
    private readonly AutoResetEvent _autoEvent = new(true);

    public int MaxPoints { get; private set; }
    public ulong? OwningCharacterId { get; private set; }

    public bool TryUpdateMaxPoints(int points, ulong characterId)
    {
        _autoEvent.WaitOne();

        if (OwningCharacterId is null)
        {
            MaxPoints = points;
            OwningCharacterId = characterId;

            _autoEvent.Set();

            return true;
        }

        if (points > MaxPoints)
        {
            MaxPoints = points;
            OwningCharacterId = characterId;

            _autoEvent.Set();

            return true;
        }

        _autoEvent.Set();
        return false;
    }
}
