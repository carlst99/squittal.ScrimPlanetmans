using System.ComponentModel.DataAnnotations;

namespace squittal.ScrimPlanetmans.App.Models.Planetside;

public class Profile
{
    [Required]
    public int Id { get; set; }

    public int ProfileTypeId { get; set; }
    public int FactionId { get; set; }
    public string Name { get; set; }
    public int ImageId { get; set; }
}
