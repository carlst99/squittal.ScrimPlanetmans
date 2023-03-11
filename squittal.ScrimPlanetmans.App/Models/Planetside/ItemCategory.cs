namespace squittal.ScrimPlanetmans.App.Models.Planetside;

public record ItemCategory(uint Id, string Name, bool IsWeaponCategory, ItemCategoryDomain Domain);
// {
//     [Required]
//     public required int Id { get; init; }
//
//     public required string Name { get; init; }
//
//     public bool IsWeaponCategory { get; set; }
//
//     public ItemCategoryDomain Domain { get; set; }
// }
