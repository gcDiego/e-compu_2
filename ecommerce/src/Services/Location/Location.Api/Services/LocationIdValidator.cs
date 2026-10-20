using System.Text.RegularExpressions;

namespace Location.Api.Services;

public static partial class LocationIdValidator
{
    public static bool IsValid(string? id) =>
        !string.IsNullOrEmpty(id) && Pattern().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
