using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

/// <summary>
/// Behavior every packing engine must satisfy when driven the way callers use it (through
/// MultiBinPacker). Every engine is covered; a new engine is checked automatically.
/// </summary>
public class EngineContractTests
{
    private const double Kerf = 0.125;

    public sealed record Scenario(
        string Name,
        (double Length, int Quantity, int Priority)[] Stock,
        (double Length, int Count)[] Parts,
        int ExpectedNotPlaced,
        bool SharedNames = false);

    // ExpectedNotPlaced is algorithm-independent for these inputs.
    private static readonly Scenario[] Scenarios =
    {
        new("unlimited stock", new[] { (96.0, -1, 1) }, new[] { (30.0, 5) }, 0),
        new("exactly enough finite stock", new[] { (96.0, 2, 1) }, new[] { (30.0, 5) }, 0),
        new("not enough finite stock", new[] { (96.0, 1, 1) }, new[] { (30.0, 5) }, 2),
        new("oversized parts are reported", new[] { (96.0, -1, 1) }, new[] { (100.0, 2), (30.0, 3) }, 2),
        new("priority order across stock lengths", new[] { (48.0, 2, 1), (120.0, -1, 2) }, new[] { (40.0, 4), (100.0, 2) }, 0),
        new("no parts", new[] { (96.0, -1, 1) }, Array.Empty<(double, int)>(), 0),
        // The app names every copy of a part the same; identity must survive value-equal parts.
        new("same-named copies on limited stock",
            new[] { (240.0, 3, 1), (288.0, -1, 2) },
            new[] { (79.5625, 5), (32.3125, 2), (34.3125, 3), (74.3125, 4), (21.125, 3) },
            0, SharedNames: true),
    };

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var engineId in EngineIds())
            foreach (var scenario in Scenarios)
                data.Add(engineId, scenario.Name);
        return data;
    }

    private static readonly PackingEngineCatalog Catalog = PackingEngineCatalog.CreateDefault();

    // Every registered engine is checked; adding an engine to BuiltInPackingEngines.All adds its cases.
    private static IEnumerable<string> EngineIds() => Catalog.Engines.Select(e => e.Id);

    private static MultiBinPacker CreatePacker(string engineId) =>
        new(Catalog.Create(engineId)) { Spacing = Kerf };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Engine_satisfies_packing_contract(string engineId, string scenarioName)
    {
        var scenario = Scenarios.Single(s => s.Name == scenarioName);
        var items = scenario.Parts
            .SelectMany((part, index) => Enumerable.Range(1, part.Count)
                .Select(n => new BinItem(scenario.SharedNames ? $"P{index}" : $"P{index}-{n}", part.Length)))
            .ToList();
        var packer = CreatePacker(engineId);
        packer.SetBins(scenario.Stock.Select(s => new MultiBin(s.Length, s.Quantity, s.Priority)));

        var result = packer.Pack(items);

        // Every input part is in exactly one bar or reported as not placed; nothing is invented.
        var returned = result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed).ToList();
        var input = new HashSet<object>(items, ReferenceEqualityComparer.Instance);
        Assert.Equal(items.Count, returned.Count);
        Assert.Equal(items.Count, returned.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.True(returned.All(input.Contains), $"{engineId} returned a part that was not in the input");

        Assert.Equal(scenario.ExpectedNotPlaced, result.ItemsNotUsed.Count);

        foreach (var bin in result.Bins)
        {
            Assert.NotEmpty(bin.Items);
            Assert.Equal(Kerf, bin.Spacing);
            Assert.Contains(scenario.Stock, s => s.Length == bin.Length);
            // CutFit's rule: the kerf after the last cut may run off the end, plus Tolerance.Epsilon.
            Assert.True(CutFit.Fits(bin.Items.Select(i => i.Length), bin.Length, Kerf),
                $"{engineId} overfilled a {bin.Length}\" bar ({string.Join(", ", bin.Items.Select(i => i.Length))})");
        }

        foreach (var finite in scenario.Stock.Where(s => s.Quantity > 0).GroupBy(s => s.Length))
        {
            var used = result.Bins.Count(b => b.Length == finite.Key);
            Assert.True(used <= finite.Sum(s => s.Quantity),
                $"{engineId} used {used} bars of {finite.Key}\" stock");
        }
    }
}
