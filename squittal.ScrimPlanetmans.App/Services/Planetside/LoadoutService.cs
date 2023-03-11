using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

/// <inheritdoc />
public class LoadoutService : ILoadoutService
{
    private readonly ICensusLoadoutService _loadoutService;
    private readonly ICensusProfileService _profileService;

    public LoadoutService(ICensusLoadoutService loadoutService, ICensusProfileService profileService)
    {
        _loadoutService = loadoutService;
        _profileService = profileService;
    }

    /// <inheritdoc />
    public async Task<CensusProfileType?> GetLoadoutProfileTypeAsync(uint loadoutId, CancellationToken ct = default)
    {
        IReadOnlyList<CensusLoadout>? loadouts = await _loadoutService.GetAllAsync(ct);
        IReadOnlyList<CensusProfile>? profiles = await _profileService.GetAllAsync(ct);

        if (loadouts is null || profiles is null)
            return null;

        CensusLoadout? loadout = loadouts.FirstOrDefault(x => x.LoadoutId == loadoutId);
        if (loadout is null)
            return null;

        return (CensusProfileType?)profiles.FirstOrDefault(p => p.ProfileId == loadout.ProfileId)?
            .ProfileTypeId;
    }

    /// <inheritdoc />
    public async Task<bool> IsLoadoutOfProfileTypeAsync
    (
        uint loadoutId,
        CensusProfileType profileType,
        CancellationToken ct = default
    )
    {
        CensusProfileType? loadoutProfileType = await GetLoadoutProfileTypeAsync(loadoutId, ct);
        return loadoutProfileType == profileType;
    }
}
