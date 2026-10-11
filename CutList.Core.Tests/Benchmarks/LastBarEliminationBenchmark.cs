using System.Diagnostics;
using System.Text.Json;
using CutList.Core.Nesting;
using CutList.Core.Tests.Nesting;
using Xunit;
using Xunit.Abstractions;

namespace CutList.Core.Tests.Benchmarks;

public sealed class LastBarBenchmarkFactAttribute : FactAttribute
{
    public LastBarBenchmarkFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LastBarEliminationBenchmark.OutputVariable)))
            Skip = $"Opt-in evaluation: set {LastBarEliminationBenchmark.OutputVariable} to an output directory.";
    }
}

/// <summary>
/// Paired whole-job evaluation of bounded last-bar elimination after the unchanged First Fit
/// and Exhaustive engines. The post-pass work budget is separate from Exhaustive's existing
/// search budget; this is not an equal-total-budget replacement for the production engine.
/// </summary>
public sealed class LastBarEliminationBenchmark(ITestOutputHelper output)
{
    public const string OutputVariable = "CUTLIST_LAST_BAR_BENCHMARK_DIR";
    public const string NonMonotoneVariable = "CUTLIST_LAST_BAR_ALLOW_NONMONOTONE";
    private static readonly long[] Budgets = [10_000, 100_000, 1_000_000];
    private const int TimedRuns = 3;
    private const double Stock = 240;
    private const double Kerf = 0.125;

    public sealed record Measurement(
        string Scenario, int Job, string BaselineEngine, long PassBudget,
        int BaselineBars, int CandidateBars, long WorkUsed, bool Exhausted,
        int Attempts, int EliminatedBars, bool DepthLimitReached,
        bool BaselineFallback, bool CandidateFallback,
        double[] BaselineMs, double[] CandidateMs);

    [LastBarBenchmarkFact]
    public void Compares_last_bar_elimination_with_unchanged_engines()
    {
        var directory = Environment.GetEnvironmentVariable(OutputVariable)!;
        bool allowNonMonotone = Environment.GetEnvironmentVariable(NonMonotoneVariable) == "1";
        Directory.CreateDirectory(directory);
        var rows = new List<Measurement>();
        var inputs = new List<object>();

        foreach (string id in new[] { "firstfit", "exhaustive" })
        {
            var warmup = SearchBaselineBenchmark.Scenarios[0].Generate(new Random(999));
            for (int run = 0; run < TimedRuns; run++)
            {
                Production(Create(id), warmup);
                Production(new LastBarEliminationEngine(Budgets[0], Create(id), allowNonMonotone), warmup);
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
                foreach (string id in new[] { "firstfit", "exhaustive" })
                foreach (long budget in Budgets)
                {
                    var baseline = Create(id);
                    var engine = new LastBarEliminationEngine(budget, Create(id), allowNonMonotone);
                    var baselineMs = new double[TimedRuns];
                    var candidateMs = new double[TimedRuns];
                    PackResult? original = null;
                    PackResult? candidate = null;
                    (int Bars, long Work, int Attempts, bool Exhausted, bool Depth)? first = null;
                    for (int run = 0; run < TimedRuns; run++)
                    {
                        if ((job + run) % 2 == 0)
                        {
                            (original, baselineMs[run]) = Timed(baseline, items);
                            (candidate, candidateMs[run]) = Timed(engine, items);
                        }
                        else
                        {
                            (candidate, candidateMs[run]) = Timed(engine, items);
                            (original, baselineMs[run]) = Timed(baseline, items);
                        }
                        AssertValid(items, original);
                        AssertValid(items, candidate);
                        Assert.True(candidate.Bins.Count <= original.Bins.Count);
                        Assert.Equal(original.Bins.Count - candidate.Bins.Count, engine.LastEliminatedBars);
                        Assert.InRange(engine.LastBudgetUsed, 0, budget + 1);
                        Assert.Equal(original.FallbackEngine, candidate.FallbackEngine);
                        var metrics = (candidate.Bins.Count, engine.LastBudgetUsed, engine.LastAttempts,
                            engine.LastBudgetExhausted, engine.LastDepthLimitReached);
                        if (first != null) Assert.Equal(first.Value, metrics);
                        first = metrics;
                    }
                    rows.Add(new Measurement(scenario.Name, job, id, budget,
                        original!.Bins.Count, candidate!.Bins.Count, engine.LastBudgetUsed,
                        engine.LastBudgetExhausted, engine.LastAttempts, engine.LastEliminatedBars,
                        engine.LastDepthLimitReached, original.FallbackEngine != null,
                        candidate.FallbackEngine != null, baselineMs, candidateMs));
                }
                Save(); // Preserve completed jobs if the evaluation is interrupted.
            }
        }
        Assert.Equal(SearchBaselineBenchmark.Scenarios.Sum(s => s.Jobs) * 2 * Budgets.Length, rows.Count);
        Save();
        output.WriteLine($"Wrote {rows.Count} paired rows to {Path.Combine(directory, "last-bar-elimination.json")}");

        void Save() => File.WriteAllText(Path.Combine(directory, "last-bar-elimination.json"),
            JsonSerializer.Serialize(new
            {
                Harness = "CutList.Core.Tests/Benchmarks/LastBarEliminationBenchmark.cs",
#if DEBUG
                Configuration = "Debug",
#else
                Configuration = "Release",
#endif
                Runtime = Environment.Version.ToString(), Environment.ProcessorCount,
                Stock, Kerf, PassBudgets = Budgets, TimedRuns,
                AllowNonMonotoneEjections = allowNonMonotone,
                ExhaustiveBudget = ExhaustiveSearchEngine.DefaultSearchBudget,
                Scope = "Unlimited stock; unchanged engine vs same engine plus test-only bounded post-pass",
                Inputs = inputs, Rows = rows
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IPackingEngine Create(string id) => id == "firstfit"
        ? new FirstFitEngine() : new ExhaustiveSearchEngine();

    private static PackResult Production(IPackingEngine engine, List<BinItem> items)
    {
        var packer = new MultiBinPacker(engine) { Spacing = Kerf };
        packer.SetBins([new MultiBin(Stock, -1, 1)]);
        return packer.Pack(items);
    }

    private static (PackResult Result, double Ms) Timed(IPackingEngine engine, List<BinItem> items)
    {
        var watch = Stopwatch.StartNew();
        var result = Production(engine, items);
        watch.Stop();
        return (result, watch.Elapsed.TotalMilliseconds);
    }

    private static void AssertValid(List<BinItem> items, PackResult result)
    {
        Assert.Empty(result.ItemsNotUsed);
        var outstanding = new HashSet<BinItem>(items, ReferenceEqualityComparer.Instance);
        var returned = result.Bins.SelectMany(b => b.Items).ToList();
        Assert.Equal(items.Count, returned.Count);
        Assert.All(returned, i => Assert.True(outstanding.Remove(i), "Duplicate or foreign reference"));
        Assert.Empty(outstanding);
        Assert.All(result.Bins, b => Assert.True(CutFit.Fits(b.Items.Select(i => i.Length), b.Length, b.Spacing)));
    }
}
