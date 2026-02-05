namespace CutList.Web.Data.Entities;

/// <summary>
/// Enumeration of supported material shapes.
/// </summary>
public enum MaterialShape
{
    RoundBar,
    RoundTube,
    FlatBar,
    SquareBar,
    SquareTube,
    RectangularTube,
    Angle,
    Channel,
    IBeam,
    Pipe
}

/// <summary>
/// Extension methods for MaterialShape enum.
/// </summary>
public static class MaterialShapeExtensions
{
    /// <summary>
    /// Gets the display name for a material shape.
    /// </summary>
    public static string GetDisplayName(this MaterialShape shape) => shape switch
    {
        MaterialShape.RoundBar => "Round Bar",
        MaterialShape.RoundTube => "Round Tube",
        MaterialShape.FlatBar => "Flat Bar",
        MaterialShape.SquareBar => "Square Bar",
        MaterialShape.SquareTube => "Square Tube",
        MaterialShape.RectangularTube => "Rectangular Tube",
        MaterialShape.Angle => "Angle",
        MaterialShape.Channel => "Channel",
        MaterialShape.IBeam => "I-Beam",
        MaterialShape.Pipe => "Pipe",
        _ => shape.ToString()
    };

    /// <summary>
    /// Parses a display name or enum value string to a MaterialShape.
    /// </summary>
    public static MaterialShape? ParseShape(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Try exact enum parse first
        if (Enum.TryParse<MaterialShape>(input, ignoreCase: true, out var result))
            return result;

        // Try display name matching
        return input.Trim().ToLowerInvariant() switch
        {
            "round bar" => MaterialShape.RoundBar,
            "round tube" => MaterialShape.RoundTube,
            "flat bar" => MaterialShape.FlatBar,
            "square bar" => MaterialShape.SquareBar,
            "square tube" => MaterialShape.SquareTube,
            "rectangular tube" or "rect tube" => MaterialShape.RectangularTube,
            "angle" => MaterialShape.Angle,
            "channel" => MaterialShape.Channel,
            "i-beam" or "ibeam" or "i beam" => MaterialShape.IBeam,
            "pipe" => MaterialShape.Pipe,
            _ => null
        };
    }

    /// <summary>
    /// Gets the dimension field names used by a given shape.
    /// </summary>
    public static string[] GetDimensionFields(this MaterialShape shape) => shape switch
    {
        MaterialShape.RoundBar => new[] { "Diameter" },
        MaterialShape.RoundTube => new[] { "OuterDiameter", "Wall" },
        MaterialShape.FlatBar => new[] { "Width", "Thickness" },
        MaterialShape.SquareBar => new[] { "Size" },
        MaterialShape.SquareTube => new[] { "Size", "Wall" },
        MaterialShape.RectangularTube => new[] { "Width", "Height", "Wall" },
        MaterialShape.Angle => new[] { "Leg1", "Leg2", "Thickness" },
        MaterialShape.Channel => new[] { "Height", "Flange", "Web" },
        MaterialShape.IBeam => new[] { "Height", "WeightPerFoot" },
        MaterialShape.Pipe => new[] { "NominalSize", "Wall", "Schedule" },
        _ => Array.Empty<string>()
    };
}
