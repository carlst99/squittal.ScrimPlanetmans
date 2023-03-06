using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusServices.Models;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class ProfileService : IProfileService, IDisposable
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusProfile _censusProfile;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<ProfileService> _logger;

    private readonly ConcurrentDictionary<int, Profile> _loadoutProfilesMap = new();
    private readonly SemaphoreSlim _mapSetUpSemaphore = new(1);

    public string BackupSqlScriptFileName => Path.Combine("CensusBackups", "dbo.Profile.Table.sql");


    public ProfileService(IDbContextHelper dbContextHelper, CensusProfile censusProfile, ISqlScriptRunner sqlScriptRunner, ILogger<ProfileService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusProfile = censusProfile;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task<IEnumerable<Profile>> GetAllProfilesAsync()
    {
        if (_loadoutProfilesMap.IsEmpty)
            await SetUpLoadoutProfilesMapAsync();

        return GetAllProfiles();
    }

    private IEnumerable<Profile> GetAllProfiles()
    {
        return _loadoutProfilesMap.Values.ToList();
    }

    public async Task<Profile?> GetProfileFromLoadoutIdAsync(int loadoutId)
    {
        if (_loadoutProfilesMap.IsEmpty)
            await SetUpLoadoutProfilesMapAsync();

        return GetProfileFromLoadoutId(loadoutId);
    }

    private Profile? GetProfileFromLoadoutId(int loadoutId)
    {
        _loadoutProfilesMap.TryGetValue(loadoutId, out Profile? profile);

        return profile;
    }

    private async Task SetUpLoadoutProfilesMapAsync()
    {
        await _mapSetUpSemaphore.WaitAsync();

        try
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var storeProfiles = await dbContext.Profiles.ToListAsync();

            var storeLoadouts = await dbContext.Loadouts.ToListAsync();

            foreach (var loadoutId in _loadoutProfilesMap.Keys)
            {
                if (storeLoadouts.All(l => l.Id != loadoutId))
                {
                    _loadoutProfilesMap.TryRemove(loadoutId, out _);
                    continue;
                }

                var profileId = storeLoadouts.Where(l => l.Id == loadoutId).Select(l => l.ProfileId).FirstOrDefault();

                if (profileId <= 0)
                {
                    _loadoutProfilesMap.TryRemove(loadoutId, out _);
                }
            }

            foreach (var profile in storeProfiles)
            {
                var loadoutId = storeLoadouts.Where(l => l.ProfileId == profile.Id).Select(l => l.ProfileId).FirstOrDefault();
                if (loadoutId <= 0)
                {
                    continue;
                }

                if (_loadoutProfilesMap.ContainsKey(loadoutId))
                {
                    _loadoutProfilesMap[loadoutId] = profile;
                }
                else
                {
                    _loadoutProfilesMap.TryAdd(loadoutId, profile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Loadout Profiles Map");
        }
        finally
        {
            _mapSetUpSemaphore.Release();
        }
    }

    public static bool IsMaxLoadoutId(int? loadoutId)
    {
        return loadoutId switch
        {
            7 => true,
            14 => true,
            21 => true,
            45 => true,
            null => false,
            _ => false,
        };
    }

    public static bool IsMaxProfileId(int profileId)
    {
        return profileId switch
        {
            8 => true,
            16 => true,
            23 => true,
            252 => true,
            _ => false,
        };
    }

    public async Task RefreshStoreAsync
    (
        bool onlyQueryCensusIfEmpty = false,
        bool canUseBackupScript = false,
        CancellationToken ct = default
    )
    {
        if (onlyQueryCensusIfEmpty)
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var anyProfiles = await dbContext.Profiles.AnyAsync(cancellationToken: ct);
            if (anyProfiles)
            {
                await SetUpLoadoutProfilesMapAsync();

                return;
            }
        }

        var success = await RefreshStoreFromCensusAsync(ct);

        if (!success && canUseBackupScript)
        {
            RefreshStoreFromBackup(ct);
        }

        await SetUpLoadoutProfilesMapAsync();
    }

    public async Task<bool> RefreshStoreFromCensusAsync(CancellationToken ct)
    {
        CensusProfileModel[] censusProfiles;

        try
        {
            censusProfiles = (await _censusProfile.GetAllProfilesAsync()).ToArray();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all Profiles. Refreshing store from backup...");
            return false;
        }

        if (!censusProfiles.Any())
            return false;

        await UpsertRangeAsync(censusProfiles.Select(ConvertToDbModel));
        _logger.LogInformation("Refreshed Profiles store");

        return true;
    }

    private async Task UpsertRangeAsync(IEnumerable<Profile> censusEntities)
    {
        var createdEntities = new List<Profile>();

        using (var factory = _dbContextHelper.GetFactory())
        {
            var dbContext = factory.GetDbContext();

            var storedEntities = await dbContext.Profiles.ToListAsync();

            foreach (var censusEntity in censusEntities)
            {
                var storeEntity = storedEntities.FirstOrDefault(e => e.Id == censusEntity.Id);
                if (storeEntity == null)
                {
                    createdEntities.Add(censusEntity);
                }
                else
                {
                    storeEntity = censusEntity;
                    dbContext.Profiles.Update(storeEntity);
                }
            }

            if (createdEntities.Any())
            {
                await dbContext.Profiles.AddRangeAsync(createdEntities);
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private static Profile ConvertToDbModel(CensusProfileModel censusModel)
    {
        return new Profile
        {
            Id = censusModel.ProfileId,
            ProfileTypeId = censusModel.ProfileTypeId,
            FactionId = censusModel.FactionId,
            Name = censusModel.Name.English,
            ImageId = censusModel.ImageId
        };
    }

    public void Dispose()
    {
        _mapSetUpSemaphore.Dispose();
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusProfile.GetProfilesCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.Profiles.CountAsync();
    }

    public void RefreshStoreFromBackup(CancellationToken ct = default)
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }
}
