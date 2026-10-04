using CutList.Core.Formatting;

namespace CutList.Web.Data.Entities;

/// <summary>
/// Base class for material dimensions. Each shape has its own derived class with specific properties.
/// </summary>
public abstract class MaterialDimensions
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    /// <summary>
    /// Generates a display string for the size based on the dimensions.
    /// </summary>
    public abstract string GenerateSizeString();

    /// <summary>
    /// Gets the primary dimension value for sorting (in thousandths of an inch).
    /// </summary>
    public abstract int GetSortOrder();
}

/// <summary>
/// Dimensions for Round Bar: solid round stock.
/// </summary>
public class RoundBarDimensions : MaterialDimensions
{
    public decimal Diameter { get; set; }

    public override string GenerateSizeString() =>
        FormatDimension(Diameter);

    public override int GetSortOrder() => (int)(Diameter * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Round Tube: hollow round stock.
/// </summary>
public class RoundTubeDimensions : MaterialDimensions
{
    public decimal OuterDiameter { get; set; }
    public decimal Wall { get; set; }

    public override string GenerateSizeString() =>
        $"{FormatDimension(OuterDiameter)} OD x {FormatHelper.FormatExactOrFractionInches(Wall)} wall";

    public override int GetSortOrder() => (int)(OuterDiameter * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Flat Bar: rectangular solid stock.
/// </summary>
public class FlatBarDimensions : MaterialDimensions
{
    public decimal Width { get; set; }
    public decimal Thickness { get; set; }

    public override string GenerateSizeString() =>
        $"{FormatDimension(Width)} x {FormatDimension(Thickness)}";

    public override int GetSortOrder() => (int)(Width * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Square Bar: solid square stock.
/// </summary>
public class SquareBarDimensions : MaterialDimensions
{
    public decimal Size { get; set; }

    public override string GenerateSizeString() =>
        FormatDimension(Size);

    public override int GetSortOrder() => (int)(Size * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Square Tube: hollow square stock.
/// </summary>
public class SquareTubeDimensions : MaterialDimensions
{
    public decimal Size { get; set; }
    public decimal Wall { get; set; }

    public override string GenerateSizeString() =>
        $"{FormatDimension(Size)} x {FormatHelper.FormatExactOrFractionInches(Wall)} wall";

    public override int GetSortOrder() => (int)(Size * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Rectangular Tube: hollow rectangular stock.
/// </summary>
public class RectangularTubeDimensions : MaterialDimensions
{
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal Wall { get; set; }

    public override string GenerateSizeString() =>
        $"{FormatDimension(Width)} x {FormatDimension(Height)} x {FormatHelper.FormatExactOrFractionInches(Wall)} wall";

    public override int GetSortOrder() => (int)(Width * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Angle: L-shaped stock.
/// </summary>
public class AngleDimensions : MaterialDimensions
{
    public decimal Leg1 { get; set; }
    public decimal Leg2 { get; set; }
    public decimal Thickness { get; set; }

    public override string GenerateSizeString() =>
        $"{FormatDimension(Leg1)} x {FormatDimension(Leg2)} x {FormatDimension(Thickness)}";

    public override int GetSortOrder() => (int)(Leg1 * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for Channel: C-shaped stock.
/// </summary>
public class ChannelDimensions : MaterialDimensions
{
    public decimal Height { get; set; }
    public decimal Flange { get; set; }
    public decimal Web { get; set; }

    public override string GenerateSizeString() =>
        $"{FormatDimension(Height)} x {FormatDimension(Flange)} x {FormatDimension(Web)}";

    public override int GetSortOrder() => (int)(Height * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}

/// <summary>
/// Dimensions for I-Beam: wide flange beam.
/// </summary>
public class IBeamDimensions : MaterialDimensions
{
    public decimal Height { get; set; }
    public decimal WeightPerFoot { get; set; }

    public override string GenerateSizeString() =>
        $"W{Height:0.##} x {WeightPerFoot:0.##}";

    public override int GetSortOrder() => (int)(Height * 1000);
}

/// <summary>
/// Dimensions for Pipe: nominal pipe size.
/// </summary>
public class PipeDimensions : MaterialDimensions
{
    public decimal NominalSize { get; set; }
    public decimal? Wall { get; set; }
    public string? Schedule { get; set; }

    public override string GenerateSizeString() =>
        !string.IsNullOrEmpty(Schedule)
            ? $"{FormatDimension(NominalSize)} NPS Sch {Schedule}"
            : $"{FormatDimension(NominalSize)} NPS x {FormatDimension(Wall ?? 0)} wall";

    public override int GetSortOrder() => (int)(NominalSize * 1000);

    private static string FormatDimension(decimal value) =>
        CutList.Core.Formatting.ArchUnits.FormatFromInches((double)value);
}
