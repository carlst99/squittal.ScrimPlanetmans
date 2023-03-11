using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace squittal.ScrimPlanetmans.App.Models.Forms;

public class OutfitAlias
{
    [StringLength(4, MinimumLength = 1)]
    [CustomValidation(typeof(OutfitAliasValidation), nameof(OutfitAliasValidation.OutfitAliasValidate))]
    public string Alias { get; set; }

    public OutfitAlias()
    {
        Alias = string.Empty;
    }

    public OutfitAlias(string alias)
    {
        Alias = alias;
    }
}

public partial class OutfitAliasValidation
{
    private static readonly Regex Regex = AliasRegex();

    public static ValidationResult? OutfitAliasValidate(string alias)
    {
        return Regex.Match(alias).Success
            ? ValidationResult.Success
            : new ValidationResult("Invalid outfit alias");
    }

    [GeneratedRegex("[A-Za-z0-9]{1,4}", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-NZ")]
    private static partial Regex AliasRegex();
}
