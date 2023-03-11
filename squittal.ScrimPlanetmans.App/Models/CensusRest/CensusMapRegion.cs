namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusMapRegion
(
    uint MapRegionId,
    uint ZoneId,
    uint FacilityId,
    string FacilityName
);
