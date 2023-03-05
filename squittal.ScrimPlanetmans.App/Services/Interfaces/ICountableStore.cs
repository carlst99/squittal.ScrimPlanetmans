using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Services.Interfaces;

public interface ICountableStore
{
    Task<int> GetCensusCountAsync();
    Task<int> GetStoreCountAsync();
}
