using System.Diagnostics;
using System.Text.Json;
using CutList.Core.Nesting;
using CutList.Core.Nesting.Search;
using Xunit;
using Xunit.Abstractions;

namespace CutList.Core.Tests.Benchmarks;

/// <summary>A fact that runs only when <see cref="SearchBaselineBenchmark.OutputVariable"/> names an output directory.</summary>
public sealed class SearchBenchmarkFactAttribute : FactAttribute
{
    public SearchBenchmarkFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SearchBaselineBenchmark.OutputVariable)))
            Skip = $"Opt-in benchmark: set {SearchBaselineBenchmark.OutputVariable} to an output directory.";
    }
}

/// <summary>
/// Opt-in baseline for the Exhaustive engine's budget-limited pattern search (see
/// docs/search-benchmark.md). Each seeded job runs through the unchanged production path
/// (<see cref="MultiBinPacker"/> with <see cref="ExhaustiveSearchEngine"/>, unlimited stock). The same
/// <see cref="MinBarsSearch"/> call the engine makes is then mirrored to read the work counter, and must
/// reach the engine's bar count. Jobs that exhaust the budget are re-run with a larger budget to show
/// what more search finds. Writes search-baseline.json; asserts consistency only, never timing.
/// </summary>
public sealed class SearchBaselineBenchmark
{
    public const string OutputVariable = "CUTLIST_SEARCH_BENCHMARK_DIR";
    public const string ExtendedBudgetVariable = "CUTLIST_SEARCH_BENCHMARK_EXTENDED_BUDGET";
    private const long DefaultExtendedBudget = 25_000_000;
    private const int TimedRuns = 3;
    private const double Kerf = 0.125;
    private const double Stock = 240;

    private readonly ITestOutputHelper _output;

    public SearchBaselineBenchmark(ITestOutputHelper output) => _output = output;

    public sealed record Scenario(string Name, string Description, int Seed, int Jobs, Func<Random, List<BinItem>> Generate);

    /// <summary>
    /// Repeated lengths: 100-300 parts over 2-12 distinct lengths. High-distinct: 40-120 distinct
    /// lengths, 1-3 of each. Lengths are whole sixteenths from 6" to 180"; 240" stock, 1/8" kerf.
    /// </summary>
    public static readonly Scenario[] Scenarios =
    {
        new("repeated", "100-300 parts, 2-12 distinct lengths, unlimited 240in stock, 1/8in kerf", 1001, 60, rng =>
        {
            int distinct = rng.Next(2, 13);
            var lengths = DistinctLengths(rng, distinct);
            int total = rng.Next(100, 301);
            var counts = new int[distinct];
            for (int i = 0; i < distinct; i++) counts[i] = 1; // every length appears
            for (int n = distinct; n < total; n++) counts[rng.Next(distinct)]++;
            return Parts(lengths, counts);
        }),
        new("distinct", "40-120 distinct lengths x1-3, unlimited 240in stock, 1/8in kerf", 2002, 60, rng =>
        {
            int distinct = rng.Next(40, 121);
            var lengths = DistinctLengths(rng, distinct);
            return Parts(lengths, lengths.Select(_ => rng.Next(1, 4)).ToArray());
        }),
    };

    private static double[] DistinctLengths(Random rng, int count)
    {
        var set = new HashSet<int>();
        while (set.Count < count) set.Add(rng.Next(6 * 16, 180 * 16 + 1));
        return set.Select(s => s / 16.0).ToArray();
    }

    private static List<BinItem> Parts(double[] lengths, int[] counts)
    {
        var items = new List<BinItem>();
        for (int i = 0; i < lengths.Length; i++)
            for (int n = 0; n < counts[i]; n++)
                items.Add(new BinItem($"P{i}", lengths[i])); // copies share a name, as in the app
        return items;
    }

    public sealed record JobResult(
        string Scenario, int Job, int Parts, int DistinctLengths, int FirstFitBars, int LowerBound,
        int Bars, bool Fallback, long BudgetUsed, bool BudgetExhausted, string Status,
        double MedianMs, double[] RunMs,
        long? ExtendedBudget, int? ExtendedBars, bool? ExtendedExhausted, long? ExtendedUsed, double? ExtendedMs);

    [SearchBenchmarkFact]
    public void Records_the_budget_limited_search_baseline()
    {
        var outDir = Environment.GetEnvironmentVariable(OutputVariable)!;
        Directory.CreateDirectory(outDir);
        long extendedBudget = long.TryParse(Environment.GetEnvironmentVariable(ExtendedBudgetVariable), out var e)
            ? e : DefaultExtendedBudget;

        // Warm up JIT and tiering on a job outside the measured set.
        for (int w = 0; w < 3; w++) Production(Scenarios[0].Generate(new Random(999)));

        var jobs = new List<JobResult>();
        foreach (var scenario in Scenarios)
        {
            var rng = new Random(scenario.Seed);
            for (int j = 0; j < scenario.Jobs; j++)
                jobs.Add(Measure(scenario.Name, j, scenario.Generate(rng), extendedBudget));
        }

        var summary = Scenarios.Select(s => Summarize(s, jobs.Where(r => r.Scenario == s.Name).ToList())).ToList();
        var report = new
        {
            Harness = "CutList.Core.Tests/Benchmarks/SearchBaselineBenchmark.cs",
#if DEBUG
            Configuration = "Debug",
#else
            Configuration = "Release",
#endif
            Runtime = Environment.Version.ToString(),
            Environment.ProcessorCount,
            Budget = ExhaustiveSearchEngine.DefaultSearchBudget,
            ExtendedBudget = extendedBudget,
            TimedRuns,
            Kerf,
            Stock,
            Scenarios = Scenarios.Select(s => new { s.Name, s.Description, s.Seed, s.Jobs }),
            Summary = summary,
            Jobs = jobs,
        };
        var path = Path.Combine(outDir, "search-baseline.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        _output.WriteLine($"Wrote {path}");
        foreach (var s in summary) _output.WriteLine(JsonSerializer.Serialize(s));
    }

    private static PackResult Production(List<BinItem> items)
    {
        var packer = new MultiBinPacker(new ExhaustiveSearchEngine()) { Spacing = Kerf };
        packer.SetBins(new[] { new MultiBin(Stock, -1, 1) });
        return packer.Pack(items);
    }

    private static JobResult Measure(string scenario, int job, List<BinItem> items, long extendedBudget)
    {
        var runMs = new double[TimedRuns];
        PackResult? result = null;
        for (int r = 0; r < TimedRuns; r++)
        {
            var sw = Stopwatch.StartNew();
            result = Production(items);
            sw.Stop();
            runMs[r] = sw.Elapsed.TotalMilliseconds;
        }
        AssertConserved(items, result!);

        int firstFitBars = new FirstFitEngine().Pack(new PackingRequest(items, Stock, Kerf)).Bins.Count;
        var demand = CutDemand.From(items, Stock, Kerf);
        int lower = demand.LowerBound(demand.Counts);

        // Mirror the engine's unlimited-stock search (First Fit placed every part).
        var (mirrorBars, budget) = MirrorMinBars(demand, firstFitBars, ExhaustiveSearchEngine.DefaultSearchBudget);
        bool fallback = result!.FallbackEngine != null;
        Assert.Equal(result.Bins.Count, mirrorBars);
        Assert.Equal(fallback, mirrorBars == firstFitBars && budget.Exhausted);

        string status = !budget.Exhausted || result.Bins.Count == lower ? "proven-optimal"
            : fallback ? "fallback" : "improved-unproven";

        long? extBudget = null; int? extBars = null; bool? extExhausted = null; long? extUsed = null; double? extMs = null;
        if (budget.Exhausted)
        {
            var sw = Stopwatch.StartNew();
            var (bars, ext) = MirrorMinBars(demand, firstFitBars, extendedBudget);
            sw.Stop();
            (extBudget, extBars, extExhausted, extUsed, extMs) =
                (extendedBudget, bars, ext.Exhausted, Math.Min(ext.Used, extendedBudget), sw.Elapsed.TotalMilliseconds);
        }

        return new JobResult(scenario, job, items.Count, demand.GroupCount, firstFitBars, lower,
            result.Bins.Count, fallback, Math.Min(budget.Used, ExhaustiveSearchEngine.DefaultSearchBudget),
            budget.Exhausted, status, Median(runMs), runMs, extBudget, extBars, extExhausted, extUsed, extMs);
    }

    private static (int Bars, SearchBudget Budget) MirrorMinBars(CutDemand demand, int firstFitBars, long limit)
    {
        var budget = new SearchBudget(limit);
        var patterns = new MinBarsSearch(demand, budget, ExhaustiveSearchEngine.MaxSearchBars)
            .Solve(firstFitBars, int.MaxValue);
        return (patterns?.Count ?? firstFitBars, budget);
    }

    private static void AssertConserved(List<BinItem> items, PackResult result)
    {
        Assert.Empty(result.ItemsNotUsed);
        var placed = result.Bins.SelectMany(b => b.Items).ToList();
        Assert.Equal(items.Count, placed.Count);
        var unplaced = new HashSet<object>(items, ReferenceEqualityComparer.Instance);
        Assert.All(placed, p => Assert.True(unplaced.Remove(p), "a part was placed twice or is not from the job"));
        Assert.Empty(unplaced);
    }

    private static object Summarize(Scenario scenario, List<JobResult> jobs)
    {
        var exhausted = jobs.Where(j => j.BudgetExhausted).ToList();
        var ms = jobs.Select(j => j.MedianMs).OrderBy(x => x).ToArray();
        var used = jobs.Select(j => (double)j.BudgetUsed).OrderBy(x => x).ToArray();
        return new
        {
            scenario.Name,
            Jobs = jobs.Count,
            Parts = new { Min = jobs.Min(j => j.Parts), Max = jobs.Max(j => j.Parts) },
            DistinctLengths = new { Min = jobs.Min(j => j.DistinctLengths), Max = jobs.Max(j => j.DistinctLengths) },
            ProvenOptimal = jobs.Count(j => j.Status == "proven-optimal"),
            ImprovedUnproven = jobs.Count(j => j.Status == "improved-unproven"),
            Fallback = jobs.Count(j => j.Fallback),
            BudgetExhausted = exhausted.Count,
            FirstFitBars = jobs.Sum(j => j.FirstFitBars),
            Bars = jobs.Sum(j => j.Bars),
            BarsSavedVsFirstFit = jobs.Sum(j => j.FirstFitBars - j.Bars),
            JobsBetterThanFirstFit = jobs.Count(j => j.Bars < j.FirstFitBars),
            GapToLowerBoundOnExhausted = exhausted.Sum(j => j.Bars - j.LowerBound),
            ExhaustedJobsAboveLowerBound = exhausted.Count(j => j.Bars > j.LowerBound),
            Extended = new
            {
                Jobs = exhausted.Count,
                BarsGained = exhausted.Sum(j => j.Bars - j.ExtendedBars!.Value),
                JobsImproved = exhausted.Count(j => j.ExtendedBars < j.Bars),
                StillExhausted = exhausted.Count(j => j.ExtendedExhausted == true),
                MaxMs = exhausted.Count == 0 ? 0 : exhausted.Max(j => j.ExtendedMs!.Value),
            },
            MedianMs = new { P50 = Percentile(ms, 0.50), P95 = Percentile(ms, 0.95), Max = ms[^1] },
            BudgetUsed = new { P50 = Percentile(used, 0.50), P95 = Percentile(used, 0.95), Max = used[^1] },
        };
    }

    private static double Median(double[] values) => Percentile(values.OrderBy(x => x).ToArray(), 0.5);

    /// <summary>Nearest-rank percentile of an ascending array.</summary>
    private static double Percentile(double[] sorted, double p) =>
        sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)];
}
