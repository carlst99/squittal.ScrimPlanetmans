using System.ComponentModel.DataAnnotations;

namespace squittal.ScrimPlanetmans.App.Models.Planetside;

public class FacilityType
{
    [Required]
    public int Id { get; set; }

    public string Description { get; set; }
}
