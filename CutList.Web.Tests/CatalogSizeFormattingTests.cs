using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CutList.Core.Formatting;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace CutList.Web.Tests;

// A class fixture gives this import test its own freshly migrated, disposable SQL Server database,
// rather than ResetAsync's unrelated job/material seed or the collection's shared catalog state.
public sealed class CatalogSizeFormattingTests(SqlServerFixture db, ITestOutputHelper output)
    : IClassFixture<SqlServerFixture>
{
    private static readonly Dictionary<string, int> OriginalGroupCounts = new()
    {
        ["angles"] = 98, ["channels"] = 65, ["flatBars"] = 177, ["iBeams"] = 71,
        ["pipes"] = 30, ["rectangularTubes"] = 70, ["roundBars"] = 30,
        ["roundTubes"] = 16, ["squareBars"] = 11, ["squareTubes"] = 48
    };

    // Only these labels were demonstrated to equal the generator at 613064b and differ from
    // the reviewed formatter. All other labels and every other JSON field remain in the hash.
    private static readonly Dictionary<string, int[]> ReviewedSizeIndices = new()
    {
        ["channels"] = [7, 20, 21, 23, 24, 25, 26, 28, 37, 40, 41, 42, 49, 51, 52],
        ["rectangularTubes"] = [0, 1, 2, 4, 5, 6, 7, 8, 11, 13, 18, 21, 22, 26, 33, 36],
        ["roundTubes"] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15],
        ["squareTubes"] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13, 16, 19, 27]
    };

    [Theory]
    [InlineData("roundTubes")]
    [InlineData("squareTubes")]
    [InlineData("rectangularTubes")]
    public void Bundled_tube_labels_use_the_exact_wall_helper(string group)
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        var rows = Rows(catalog).Where(row => row.Group == group).ToArray();
        Assert.Equal(OriginalGroupCounts[group], rows.Length);
        foreach (var row in rows)
        {
            var wall = row.Entry.GetProperty("wall").GetDecimal();
            var exactWall = FormatHelper.FormatExactOrFractionInches(wall);
            var expected = row.Dimensions switch
            {
                RoundTubeDimensions d => $"{ArchUnits.FormatFromInches((double)d.OuterDiameter)} OD x {exactWall} wall",
                SquareTubeDimensions d => $"{ArchUnits.FormatFromInches((double)d.Size)} x {exactWall} wall",
                RectangularTubeDimensions d => $"{ArchUnits.FormatFromInches((double)d.Width)} x {ArchUnits.FormatFromInches((double)d.Height)} x {exactWall} wall",
                _ => throw new InvalidOperationException(group)
            };
            Assert.Equal(expected, row.Size);
            Assert.Equal(expected, row.Dimensions.GenerateSizeString());
        }
    }

    [Fact]
    public void Reviewed_generated_channel_labels_use_current_length_rounding()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        var reviewed = Rows(catalog).Where(row => row.Group == "channels"
            && ReviewedSizeIndices[row.Group].Contains(row.Index)).ToArray();
        Assert.Equal(15, reviewed.Length);
        Assert.All(reviewed, row => Assert.Equal(row.Dimensions.GenerateSizeString(), row.Size));
    }

    [Fact]
    public void Bundled_catalog_retains_original_counts_and_unique_normalized_shape_size_keys()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        var groups = catalog.RootElement.GetProperty("materials").EnumerateObject().ToArray();
        Assert.Equal(10, groups.Length);
        foreach (var group in groups)
            Assert.Equal(OriginalGroupCounts[group.Name], group.Value.GetArrayLength());
        var rows = Rows(catalog);
        Assert.Equal(616, rows.Length);
        Assert.Equal(46, rows.Count(row => row.Shape is MaterialShape.RoundTube or MaterialShape.SquareTube
            or MaterialShape.RectangularTube && row.Entry.GetProperty("wall").GetDecimal() % 0.0625m != 0));
        Assert.Equal(rows.Length, rows.Select(row => (row.Shape, row.Size.Trim().ToUpperInvariant())).Distinct().Count());
    }

    [Fact]
    public void Every_field_except_the_61_reviewed_sizes_matches_the_pre_edit_semantic_snapshot()
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in catalog.RootElement.EnumerateObject())
            {
                if (property.Name != "materials") { property.WriteTo(writer); continue; }
                writer.WritePropertyName(property.Name);
                writer.WriteStartObject();
                foreach (var group in property.Value.EnumerateObject())
                {
                    writer.WritePropertyName(group.Name);
                    writer.WriteStartArray();
                    var index = 0;
                    foreach (var entry in group.Value.EnumerateArray())
                    {
                        writer.WriteStartObject();
                        foreach (var field in entry.EnumerateObject())
                        {
                            var reviewed = ReviewedSizeIndices.TryGetValue(group.Name, out var indices) && indices.Contains(index);
                            if (field.Name != "size" || !reviewed) field.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                        index++;
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        // Includes numeric tokens, type/grade/description, stock, legacy supplier/inventory
        // fields, export metadata, property order and array order; not a DTO round-trip.
        Assert.Equal("76C9EBCF619D83E28F27433D6856F67F8BA0BE00DF864DF9790D79EA96976E73",
            Convert.ToHexString(SHA256.HashData(stream.ToArray())));
    }

    [Fact]
    public async Task Fresh_and_repeat_HTTP_import_preserve_exact_labels_dimensions_and_row_identity()
    {
        var payload = File.ReadAllText(SeedPath());
        using var catalog = JsonDocument.Parse(payload);
        var expected = Rows(catalog);
        using var client = db.Factory.CreateClient();
        await using (var context = await db.CreateContextAsync())
        {
            Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
            Assert.Empty(await context.Materials.ToListAsync());
            Assert.Empty(await context.StockItems.ToListAsync());
        }

        var first = await ImportAsync(client, payload);
        SaveEvidence("import-first.json", first);
        Assert.Empty(first.Errors);
        Assert.Empty(first.Warnings);
        Assert.Equal(expected.Length, first.MaterialsCreated);
        Assert.Equal(0, first.MaterialsUpdated);
        var stockCount = expected.Sum(row => row.Entry.GetProperty("stockItems").GetArrayLength());
        Assert.Equal(stockCount, first.StockItemsCreated);
        Assert.Equal(0, first.StockItemsUpdated);

        var before = await ReadBackAsync(client, expected);
        SaveEvidence("persisted-first.json", before);
        var gauges = before.Where(row => row.Shape == MaterialShape.SquareTube && row.Values["Size"] == 1m
            && row.Values["Wall"] is 0.0598m or 0.0673m).OrderBy(row => row.Values["Wall"]).ToArray();
        Assert.Equal(new[] { "1\" x 0.0598\" wall", "1\" x 0.0673\" wall" }, gauges.Select(row => row.Size));
        Assert.Equal(2, gauges.Select(row => row.Id).Distinct().Count());
        await AssertExportAsync(client, expected, "export-first.json");

        var repeat = await ImportAsync(client, payload);
        SaveEvidence("import-repeat.json", repeat);
        Assert.Empty(repeat.Errors);
        Assert.Empty(repeat.Warnings);
        Assert.Equal(0, repeat.MaterialsCreated);
        Assert.Equal(expected.Length, repeat.MaterialsUpdated);
        Assert.Equal(0, repeat.StockItemsCreated);
        Assert.Equal(stockCount, repeat.StockItemsUpdated);
        var after = await ReadBackAsync(client, expected);
        SaveEvidence("persisted-repeat.json", after);
        // Captures exact material/dimension/stock IDs, labels and numbers, ignoring timestamps
        // that the existing importer intentionally updates on its second pass.
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        await AssertExportAsync(client, expected, "export-repeat.json");
        output.WriteLine($"Fresh SQL import: {first.MaterialsCreated} materials, {first.StockItemsCreated} stock rows; repeat: {repeat.MaterialsCreated} new materials, {repeat.StockItemsCreated} new stock rows; exact IDs/labels/dimensions preserved.");
    }

    private static async Task<ImportResultDto> ImportAsync(HttpClient client, string payload)
    {
        using var response = await client.PostAsync("/api/catalog/import", new StringContent(payload, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportResultDto>())!;
    }

    private async Task<PersistedRow[]> ReadBackAsync(HttpClient client, SeedRow[] expected)
    {
        var publicRows = (await client.GetFromJsonAsync<List<MaterialDto>>("/api/materials"))!;
        Assert.Equal(expected.Length, publicRows.Count);
        Assert.Equal(expected.Length, publicRows.Select(row => row.Id).Distinct().Count());
        var publicByKey = publicRows.ToDictionary(row => (row.Shape, row.Size));
        await using var context = await db.CreateContextAsync();
        var stored = await context.Materials.Include(row => row.Dimensions).Include(row => row.StockItems)
            .AsNoTracking().OrderBy(row => row.Id).ToArrayAsync();
        Assert.Equal(expected.Length, stored.Length);
        var expectedByKey = expected.ToDictionary(row => (row.Shape, row.Size));
        foreach (var row in stored)
        {
            var seed = expectedByKey[(row.Shape, row.Size)];
            Assert.True(row.Id > 0);
            Assert.True(row.IsActive);
            Assert.Equal(seed.Dimensions.GetType(), row.Dimensions!.GetType());
            Assert.True(row.Dimensions.Id > 0);
            Assert.Equal(row.Id, row.Dimensions.MaterialId);
            Assert.Equal(DimensionValues(seed.Dimensions), DimensionValues(row.Dimensions));
            Assert.Equal(seed.Dimensions.GetSortOrder(), row.SortOrder);
            Assert.Equal(seed.Entry.GetProperty("type").GetString(), row.Type.ToString());
            Assert.Equal(OptionalString(seed.Entry, "grade"), row.Grade);
            Assert.Equal(OptionalString(seed.Entry, "description"), row.Description);
            Assert.Equal(seed.Dimensions.GenerateSizeString(), row.Size);
            Assert.Equal(row.Id, publicByKey[(row.Shape.GetDisplayName(), row.Size)].Id);
            var detail = (await client.GetFromJsonAsync<MaterialDto>($"/api/materials/{row.Id}"))!;
            Assert.Equal(row.Id, detail.Id);
            Assert.Equal(row.Size, detail.Size);
            Assert.Equal(row.Shape.GetDisplayName(), detail.Shape);
            Assert.Equal(row.Type.ToString(), detail.Type);
            Assert.Equal(row.Grade, detail.Grade);
            Assert.Equal(row.Description, detail.Description);
            Assert.True(detail.IsActive);
            Assert.Equal(row.Dimensions.GetType().Name.Replace("Dimensions", ""), detail.Dimensions!.DimensionType);
            Assert.Equal(DimensionValues(seed.Dimensions), detail.Dimensions.Values.OrderBy(pair => pair.Key).ToArray());
            var stocks = seed.Entry.GetProperty("stockItems").EnumerateArray().ToArray();
            Assert.Equal(stocks.Length, row.StockItems.Count);
            foreach (var stock in stocks)
            {
                var item = Assert.Single(row.StockItems, item => item.LengthInches == stock.GetProperty("lengthInches").GetDecimal());
                Assert.True(item.IsActive);
                Assert.Equal(OptionalString(stock, "name"), item.Name);
                Assert.Equal(OptionalString(stock, "notes"), item.Notes);
            }
        }
        return stored.Select(row => new PersistedRow(row.Id, row.Shape, row.Size, row.Type, row.Grade, row.Description,
            row.SortOrder, row.Dimensions!.Id, row.Dimensions.MaterialId,
            DimensionValues(row.Dimensions).ToDictionary(pair => pair.Key, pair => pair.Value),
            row.StockItems.OrderBy(item => item.Id).Select(item => new PersistedStock(item.Id, item.MaterialId, item.LengthInches, item.Name, item.Notes, item.IsActive)).ToArray())).ToArray();
    }

    private static async Task AssertExportAsync(HttpClient client, SeedRow[] expected, string evidenceName)
    {
        var json = await client.GetStringAsync("/api/catalog/export");
        using var export = JsonDocument.Parse(json);
        SaveEvidence(evidenceName, export.RootElement);
        var actual = Rows(export);
        Assert.Equal(expected.Length, actual.Length);
        var byKey = actual.ToDictionary(row => (row.Shape, row.Size));
        foreach (var seed in expected)
        {
            var row = byKey[(seed.Shape, seed.Size)];
            Assert.Equal(seed.Dimensions.GenerateSizeString(), row.Size);
            Assert.Equal(DimensionValues(seed.Dimensions), DimensionValues(row.Dimensions));
            Assert.Equal(OptionalString(seed.Entry, "grade"), OptionalString(row.Entry, "grade"));
            Assert.Equal(OptionalString(seed.Entry, "description"), OptionalString(row.Entry, "description"));
            Assert.Equal(seed.Entry.GetProperty("type").GetString(), row.Entry.GetProperty("type").GetString());
            Assert.Equal(seed.Entry.GetProperty("stockItems").GetArrayLength(), row.Entry.GetProperty("stockItems").GetArrayLength());
        }
    }

    private static string SeedPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "CutList.Web", "Data", "SeedData", "oneals-catalog.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Cannot find the real bundled oneals-catalog.json in the checkout.");
    }

    private static SeedRow[] Rows(JsonDocument catalog) => catalog.RootElement.GetProperty("materials").EnumerateObject()
        .SelectMany(group => group.Value.EnumerateArray().Select((entry, index) =>
        {
            decimal D(string key) => entry.GetProperty(key).GetDecimal();
            MaterialDimensions dimensions = group.Name switch
            {
                "angles" => new AngleDimensions { Leg1 = D("leg1"), Leg2 = D("leg2"), Thickness = D("thickness") },
                "channels" => new ChannelDimensions { Height = D("height"), Flange = D("flange"), Web = D("web") },
                "flatBars" => new FlatBarDimensions { Width = D("width"), Thickness = D("thickness") },
                "iBeams" => new IBeamDimensions { Height = D("height"), WeightPerFoot = D("weightPerFoot") },
                "pipes" => new PipeDimensions { NominalSize = D("nominalSize"), Wall = D("wall"), Schedule = OptionalString(entry, "schedule") },
                "rectangularTubes" => new RectangularTubeDimensions { Width = D("width"), Height = D("height"), Wall = D("wall") },
                "roundBars" => new RoundBarDimensions { Diameter = D("diameter") },
                "roundTubes" => new RoundTubeDimensions { OuterDiameter = D("outerDiameter"), Wall = D("wall") },
                "squareBars" => new SquareBarDimensions { Size = D("sideLength") },
                "squareTubes" => new SquareTubeDimensions { Size = D("sideLength"), Wall = D("wall") },
                _ => throw new InvalidOperationException($"Unexpected catalog group: {group.Name}")
            };
            var shape = Enum.Parse<MaterialShape>(dimensions.GetType().Name.Replace("Dimensions", ""));
            return new SeedRow(group.Name, index, shape, entry.GetProperty("size").GetString()!, entry.Clone(), dimensions);
        })).ToArray();

    private static KeyValuePair<string, decimal>[] DimensionValues(MaterialDimensions dimensions) => dimensions.GetType().GetProperties()
        .Where(property => property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
        .OrderBy(property => property.Name)
        .Select(property => KeyValuePair.Create(property.Name, (decimal)property.GetValue(dimensions)!)).ToArray();

    private static string? OptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() : null;

    private static void SaveEvidence(string name, object value)
    {
        var directory = Environment.GetEnvironmentVariable("CUTLIST_TASK6_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record SeedRow(string Group, int Index, MaterialShape Shape, string Size, JsonElement Entry, MaterialDimensions Dimensions);
    private sealed record PersistedRow(int Id, MaterialShape Shape, string Size, MaterialType Type, string? Grade, string? Description,
        int SortOrder, int DimensionId, int DimensionMaterialId, Dictionary<string, decimal> Values, PersistedStock[] Stock);
    private sealed record PersistedStock(int Id, int MaterialId, decimal LengthInches, string? Name, string? Notes, bool IsActive);
}
