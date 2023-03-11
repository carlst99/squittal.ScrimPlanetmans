using System.Collections.Generic;
using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

/// <summary>
/// This is a Sanctuary.Census model.
/// </summary>
/// <param name="ItemCategoryId">The ID of the item category.</param>
/// <param name="Name">The globalised name of the item category.</param>
/// <param name="ParentCategoryIds">The IDs of the category's parents.</param>
public record CensusItemCategory
(
    uint ItemCategoryId,
    GlobalizedString Name,
    IReadOnlyList<uint>? ParentCategoryIds
);
