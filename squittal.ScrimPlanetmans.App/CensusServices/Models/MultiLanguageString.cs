using Newtonsoft.Json;

namespace squittal.ScrimPlanetmans.App.CensusServices.Models;

public class MultiLanguageString
{
    [JsonProperty("en")]
    public string English { get; set; }
}
