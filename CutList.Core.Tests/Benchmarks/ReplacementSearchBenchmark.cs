using System.Diagnostics;
using System.Text.Json;
using CutList.Core.Nesting;
using CutList.Core.Tests.Nesting;
using Xunit;
using Xunit.Abstractions;

namespace CutList.Core.Tests.Benchmarks;

public sealed class ReplacementBenchmarkFactAttribute : FactAttribute
{
    public ReplacementBenchmarkFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ReplacementSearchBenchmark.OutputVariable)))
            Skip = $"Opt-in evaluation: set {ReplacementSearchBenchmark.OutputVariable} to an output directory.";
    }
}

/// <summary>
/// Paired evaluation of the unchanged production First Fit and a test-only bounded replacement
/// search. Uses the search baseline's seeds, plus limited-stock versions of those same jobs.
/// No timing assertion; candidate quality and exact reference/capacity safety are asserted.
/// </summary>
public sealed class ReplacementSearchBenchmark(ITestOutputHelper output)
{
    public const string OutputVariable = "CUTLIST_REPLACEMENT_BENCHMARK_DIR";
    private static readonly long[] Budgets = [10_000, 100_000, ExhaustiveSearchEngine.DefaultSearchBudget];
    private const int TimedRuns = 3;
    private const double Stock = 240;
    private const double Kerf = 0.125;

    public sealed record Measurement(
        string Scenario, int Job, int MaxBars, int Parts, int DistinctLengths, long Budget,
        int BaselineBars, int CandidateBars, int BaselineUnused, int CandidateUnused,
        long BaselineUnusedLength, long CandidateUnusedLength,
        long WorkUsed, bool Exhausted, bool GuardRejected,
        double[] BaselineMs, double[] CandidateMs);

    [ReplacementBenchmarkFact]
    public void Compares_bounded_replacement_with_production_first_fit()
    {
        var directory = Environment.GetEnvironmentVariable(OutputVariable)!;
        Directory.CreateDirectory(directory);
        var rows = new List<Measurement>();
        var inputs = new List<object>();
        var baseline = new FirstFitEngine();

        // JIT warmup outside the measured corpus and timed regions.
        foreach (var budget in Budgets)
        {
            var warmup = SearchBaselineBenchmark.Scenarios[0].Generate(new Random(999));
            for (int run = 0; run < TimedRuns; run++)
            {
                Production(baseline, warmup, -1);
                Production(new PatternReplacementEngine(budget), warmup, -1);
            }
        }

        foreach (var scenario in SearchBaselineBenchmark.Scenarios)
        {
            var random = new Random(scenario.Seed);
            for (int job = 0; job < scenario.Jobs; job++)
            {
                var items = scenario.Generate(random);
                inputs.Add(new
                {
                    Scenario = scenario.Name, Job = job,
                    Lengths = items.GroupBy(i => i.Length).Select(g => new { Length = g.Key, Count = g.Count() })
                });
                int baselineBars = Production(baseline, items, -1).Bins.Count;
                foreach (int maxBars in new[] { -1, Math.Max(1, baselineBars / 2) })
                foreach (long budget in Budgets)
                {
                    var engine = new PatternReplacementEngine(budget);
                    var baselineMs = new double[TimedRuns];
                    var candidateMs = new double[TimedRuns];
                    PackResult? original = null;
                    PackResult? candidate = null;
                    for (int run = 0; run < TimedRuns; run++)
                    {
                        // Alternate pair order across jobs/runs; no validation in timed region.
                        if ((job + run) % 2 == 0)
                        {
                            (original, baselineMs[run]) = Timed(baseline, items, maxBars);
                            (candidate, candidateMs[run]) = Timed(engine, items, maxBars);
                        }
                        else
                        {
                            (candidate, candidateMs[run]) = Timed(engine, items, maxBars);
                            (original, baselineMs[run]) = Timed(baseline, items, maxBars);
                        }
                        AssertValid(items, original, maxBars);
                        AssertValid(items, candidate, maxBars);
                        Assert.True(candidate.Bins.Count <= original.Bins.Count);
                        Assert.True(candidate.ItemsNotUsed.Count <= original.ItemsNotUsed.Count);
                        Assert.True(UnusedLength(candidate) <= UnusedLength(original));
                        Assert.InRange(engine.LastBudgetUsed, 0, budget + 1); // counter's exhaustion sentinel
                    }
                    rows.Add(new Measurement(scenario.Name, job, maxBars, items.Count,
                        items.Select(i => i.Length).Distinct().Count(), budget,
                        original!.Bins.Count, candidate!.Bins.Count,
                        original.ItemsNotUsed.Count, candidate.ItemsNotUsed.Count,
                        UnusedLength(original), UnusedLength(candidate),
                        engine.LastBudgetUsed, engine.LastBudgetExhausted, engine.LastGuardRejected,
                        baselineMs, candidateMs));
                }
                // Incremental persistence: an interrupted evaluation keeps completed jobs.
                Save();
            }
        }
        Assert.Equal(SearchBaselineBenchmark.Scenarios.Sum(s => s.Jobs) * 2 * Budgets.Length, rows.Count);
        Save();
        output.WriteLine($"Wrote {rows.Count} paired rows to {Path.Combine(directory, "replacement-search.json")}");

        void Save() => File.WriteAllText(Path.Combine(directory, "replacement-search.json"),
            JsonSerializer.Serialize(new
            {
                Harness = "CutList.Core.Tests/Benchmarks/ReplacementSearchBenchmark.cs",
#if DEBUG
                Configuration = "Debug",
#else
                Configuration = "Release",
#endif
                Runtime = Environment.Version.ToString(), Environment.ProcessorCount,
                Stock, Kerf, Budgets, TimedRuns,
                Scope = "Production FirstFit vs test-only pattern replacement; no Exhaustive policy change",
                Inputs = inputs, Rows = rows
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static PackResult Production(IPackingEngine engine, List<BinItem> items, int maxBars)
    {
        var packer = new MultiBinPacker(engine) { Spacing = Kerf };
        packer.SetBins([new MultiBin(Stock, maxBars, 1)]);
        return packer.Pack(items);
    }

    private static (PackResult Result, double Ms) Timed(IPackingEngine engine, List<BinItem> items, int maxBars)
    {
        var watch = Stopwatch.StartNew();
        var result = Production(engine, items, maxBars);
        watch.Stop();
        return (result, watch.Elapsed.TotalMilliseconds);
    }

    private static long UnusedLength(PackResult result) => result.ItemsNotUsed.Sum(i => CutFit.Units(i.Length));

    private static void AssertValid(List<BinItem> items, PackResult result, int maxBars)
    {
        var unreturned = new HashSet<BinItem>(items, ReferenceEqualityComparer.Instance);
        var returned = result.Bins.SelectMany(b => b.Items).Concat(result.ItemsNotUsed).ToList();
        Assert.Equal(items.Count, returned.Count);
        Assert.All(returned, i => Assert.True(unreturned.Remove(i), "Duplicate or foreign part reference"));
        Assert.Empty(unreturned);
        Assert.All(result.Bins, b => Assert.True(CutFit.Fits(b.Items.Select(i => i.Length), b.Length, b.Spacing)));
        if (maxBars < 0) Assert.Empty(result.ItemsNotUsed);
        else Assert.True(result.Bins.Count <= maxBars);
    }
}
