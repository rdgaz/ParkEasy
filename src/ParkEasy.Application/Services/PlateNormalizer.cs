using System.Text.RegularExpressions;

namespace ParkEasy.Application.Services;

public static partial class PlateNormalizer
{
    /// <summary>
    /// Normalizes a license plate by removing hyphens, spaces, and converting to uppercase.
    /// Supports both old Brazilian format (ABC1234) and Mercosul format (ABC1D23).
    /// </summary>
    public static string Normalize(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return string.Empty;

        // Remove hyphens, spaces, and any non-alphanumeric characters
        var normalized = AlphanumericOnly().Replace(plate, "").ToUpperInvariant().Trim();

        return normalized;
    }

    /// <summary>
    /// Validates that a plate follows either the old Brazilian format or Mercosul format.
    /// Old: ABC1234 (3 letters + 4 digits)
    /// Mercosul: ABC1D23 (3 letters + 1 digit + 1 letter + 2 digits)
    /// </summary>
    public static bool IsValid(string normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate) || normalizedPlate.Length != 7)
            return false;

        // Old format: AAA9999
        if (OldFormat().IsMatch(normalizedPlate))
            return true;

        // Mercosul format: AAA9A99
        if (MercosulFormat().IsMatch(normalizedPlate))
            return true;

        return false;
    }

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex AlphanumericOnly();

    [GeneratedRegex("^[A-Z]{3}[0-9]{4}$")]
    private static partial Regex OldFormat();

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z][0-9]{2}$")]
    private static partial Regex MercosulFormat();
}
