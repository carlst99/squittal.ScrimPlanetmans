﻿using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusMapRegion
(
    uint MapRegionId,
    ZoneDefinition ZoneId,
    uint FacilityId,
    string FacilityName
);
