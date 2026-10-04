using System.Globalization;
using Bunit;
using CutList.Web.Components.Shared;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EditMaterialPage = CutList.Web.Components.Pages.Materials.Edit;

namespace CutList.Web.Tests;

public sealed class MaterialSizeFormattingTests
{
    public static IEnumerable<object[]> TubeWalls()
    {
        var walls = new[]
        {
            ("0.0598", "0.0598\""),
            ("0.065", "0.065\""),
            ("0.1196", "0.1196\""),
            ("0.0625", "1/16\""),
            ("0.125", "1/8\""),
            ("0.0598123456789", "0.0598123456789\""),
            ("0.0625000000000000000000000001", "0.0625000000000000000000000001\"")
        };
        foreach (var shape in new[] { MaterialShape.RoundTube, MaterialShape.SquareTube, MaterialShape.RectangularTube })
        foreach (var (wall, expected) in walls)
            yield return new object[] { shape, wall, expected };
    }

    [Theory]
    [MemberData(nameof(TubeWalls))]
    public void Tube_size_preserves_exact_wall_thickness_but_rounds_overall_dimensions(
        MaterialShape shape, string wallText, string expectedWall)
    {
        var wall = decimal.Parse(wallText, CultureInfo.InvariantCulture);
        MaterialDimensions dimensions = shape switch
        {
            MaterialShape.RoundTube => new RoundTubeDimensions { OuterDiameter = 1.999m, Wall = wall },
            MaterialShape.SquareTube => new SquareTubeDimensions { Size = 1.999m, Wall = wall },
            MaterialShape.RectangularTube => new RectangularTubeDimensions { Width = 1.999m, Height = 3.999m, Wall = wall },
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
        var prefix = shape switch
        {
            MaterialShape.RoundTube => "2\" OD x ",
            MaterialShape.SquareTube => "2\" x ",
            _ => "2\" x 4\" x "
        };

        Assert.Equal($"{prefix}{expectedWall} wall", dimensions.GenerateSizeString());
    }

    public static IEnumerable<object[]> ShapeCases()
    {
        yield return new object[] { MaterialShape.RoundBar, "", "2\"" };
        yield return new object[] { MaterialShape.RoundTube, "", "2\" OD x 0.0598\" wall" };
        yield return new object[] { MaterialShape.FlatBar, "", "2\" x 1/16\"" };
        yield return new object[] { MaterialShape.SquareBar, "", "2\"" };
        yield return new object[] { MaterialShape.SquareTube, "", "2\" x 0.0598\" wall" };
        yield return new object[] { MaterialShape.RectangularTube, "", "2\" x 4\" x 0.0598\" wall" };
        yield return new object[] { MaterialShape.Angle, "", "2\" x 4\" x 1/16\"" };
        yield return new object[] { MaterialShape.Channel, "", "2\" x 4\" x 1/16\"" };
        yield return new object[] { MaterialShape.IBeam, "", "W8.12 x 31.23" };
        yield return new object[] { MaterialShape.Pipe, "", "2\" NPS x 1/16\" wall" };
        yield return new object[] { MaterialShape.Pipe, "40", "2\" NPS Sch 40" };
        yield return new object[] { MaterialShape.Pipe, "null-wall", "2\" NPS x 0\" wall" };
    }

    [Theory]
    [MemberData(nameof(ShapeCases))]
    public void Shape_size_keeps_the_existing_non_wall_and_pipe_formats(
        MaterialShape shape, string pipeMode, string expected)
    {
        Assert.Equal(expected, CreateDimensions(shape, pipeMode).GenerateSizeString());
    }

    [Theory]
    [MemberData(nameof(ShapeCases))]
    public void Generating_a_size_does_not_mutate_any_shape_dimensions(
        MaterialShape shape, string pipeMode, string _)
    {
        var dimensions = CreateDimensions(shape, pipeMode);
        dimensions.Id = 123;
        dimensions.MaterialId = 456;
        var before = DimensionValues(dimensions);

        dimensions.GenerateSizeString();
        dimensions.GenerateSizeString();

        Assert.Equal(before, DimensionValues(dimensions));
    }

    [Theory]
    [InlineData(MaterialShape.RoundBar)]
    [InlineData(MaterialShape.RoundTube)]
    [InlineData(MaterialShape.FlatBar)]
    [InlineData(MaterialShape.SquareBar)]
    [InlineData(MaterialShape.SquareTube)]
    [InlineData(MaterialShape.RectangularTube)]
    [InlineData(MaterialShape.Angle)]
    [InlineData(MaterialShape.Channel)]
    [InlineData(MaterialShape.IBeam)]
    [InlineData(MaterialShape.Pipe)]
    public async Task Real_material_editor_opts_in_only_the_three_tube_wall_inputs(MaterialShape shape)
    {
        await using var context = new BunitContext();
        // The new-material page must render without querying any database.
        context.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new NoDatabaseFactory());
        context.Services.AddScoped<MaterialService>();
        var page = context.Render<EditMaterialPage>();
        await page.Find("select.form-select").ChangeAsync(new() { Value = shape.ToString() });
        var inputs = page.FindComponents<LengthInput>();
        Assert.Equal(shape switch
        {
            MaterialShape.RoundBar or MaterialShape.SquareBar or MaterialShape.IBeam => 1,
            MaterialShape.RectangularTube or MaterialShape.Angle or MaterialShape.Channel => 3,
            _ => 2
        }, inputs.Count);
        var tube = shape is MaterialShape.RoundTube or MaterialShape.SquareTube or MaterialShape.RectangularTube;

        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            var isTubeWall = tube && index == inputs.Count - 1;
            await input.Find("input").InputAsync(new() { Value = "0.0598" });
            Assert.Equal("0.0598", input.Find("input").GetAttribute("value"));
            AssertBoundValue(input, 0.0598m);
            await input.Find("input").BlurAsync(new());
            Assert.Equal(isTubeWall ? "0.0598\"" : "1/16\"", input.Find("input").GetAttribute("value"));
            Assert.Equal(isTubeWall, input.Instance.PreservePrecision);
            AssertBoundValue(input, 0.0598m);
        }

        // Force a real parent re-render: all dimension values must still be bound to the
        // exact model rather than to the formatted text, including optional pipe wall.
        await page.FindAll("select.form-select")[1].ChangeAsync(new() { Value = MaterialType.Aluminum.ToString() });
        Assert.All(page.FindComponents<LengthInput>(), input => AssertBoundValue(input, 0.0598m));
        if (tube)
            Assert.EndsWith("x 0.0598\" wall", page.FindAll("dd").Last().TextContent.Trim());
    }

    private static void AssertBoundValue(IRenderedComponent<LengthInput> input, decimal expected)
    {
        if (input.Instance.NullableValueChanged.HasDelegate)
            Assert.Equal(expected, input.Instance.NullableValue);
        else
            Assert.Equal(expected, input.Instance.Value);
    }

    private static MaterialDimensions CreateDimensions(MaterialShape shape, string pipeMode) => shape switch
    {
        MaterialShape.RoundBar => new RoundBarDimensions { Diameter = 1.999m },
        MaterialShape.RoundTube => new RoundTubeDimensions { OuterDiameter = 1.999m, Wall = 0.0598m },
        MaterialShape.FlatBar => new FlatBarDimensions { Width = 1.999m, Thickness = 0.0598m },
        MaterialShape.SquareBar => new SquareBarDimensions { Size = 1.999m },
        MaterialShape.SquareTube => new SquareTubeDimensions { Size = 1.999m, Wall = 0.0598m },
        MaterialShape.RectangularTube => new RectangularTubeDimensions { Width = 1.999m, Height = 3.999m, Wall = 0.0598m },
        MaterialShape.Angle => new AngleDimensions { Leg1 = 1.999m, Leg2 = 3.999m, Thickness = 0.0598m },
        MaterialShape.Channel => new ChannelDimensions { Height = 1.999m, Flange = 3.999m, Web = 0.0598m },
        MaterialShape.IBeam => new IBeamDimensions { Height = 8.12345m, WeightPerFoot = 31.23456m },
        MaterialShape.Pipe => new PipeDimensions
        {
            NominalSize = 1.999m,
            Wall = pipeMode == "null-wall" ? null : 0.0598m,
            Schedule = pipeMode is "" or "null-wall" ? null : pipeMode
        },
        _ => throw new ArgumentOutOfRangeException(nameof(shape))
    };

    private static KeyValuePair<string, object?>[] DimensionValues(MaterialDimensions dimensions) =>
        dimensions.GetType().GetProperties()
            .OrderBy(property => property.Name)
            .Select(property => KeyValuePair.Create(property.Name, property.GetValue(dimensions)))
            .ToArray();

    private sealed class NoDatabaseFactory : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            throw new InvalidOperationException("The new-material display test must not use a database.");
    }
}
