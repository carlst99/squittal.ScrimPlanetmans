using System;
using System.ComponentModel.DataAnnotations;

namespace squittal.ScrimPlanetmans.App.Models.Planetside.Events;

public class PlayerLogout
{
    [Required]
    public ulong CharacterId { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    public int WorldId { get; set; }
}
