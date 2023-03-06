﻿using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimMatch
{
    [Required]
    public string Id { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public int RulesetId { get; set; }

    public string Title { get; set; }

    #region Navigation Properties
    public Ruleset Ruleset { get; set; }
    #endregion Navigation Properties
}
