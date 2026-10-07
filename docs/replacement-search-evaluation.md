# Bounded replacement-search evaluation

## Decision

Keep the production engines unchanged. Reusing `MaxFillSearch`/`CutPatterns` does find better
local replacements than the current greedy pass, but this trial saved **no bars on any of the
120 unlimited-stock benchmark jobs**, at any tested budget. Limited-stock gains were small,
while the prototype added work. This is evidence against enabling this particular pass by
default, not proof that all replacement strategies are unhelpful.

The runnable candidate is deliberately in `CutList.Core.Tests/Nesting/PatternReplacementEngine.cs`,
not the engine catalog. The only Core change is an optional `reservedCapacity` argument to the
internal `CutDemand.From`; its default is zero, preserving every production caller. No engine
id, public packing API, Exhaustive work budget, fallback policy or deployment changes.

## Narrow contract

- Start with a normal greedy bar and retain the entire longest-length group. For each other
  distinct packed length, consider replacing one actual part with remaining parts.
- Search one bar with `MaxFillSearch.Solve(1, displacedPartLength)`, which already uses
  `CutPatterns.Maximal`. Accept only a strict gain in **part length**, not merely additional kerfs.
- Compute the available capacity exactly as
  `CutFit.Capacity(stock, kerf) - sum(CutFit.Size(retainedPart, kerf))`.
  Do not use rounded/clamped `Bin.RemainingLength`, or reinterpret capacity as new stock:
  doing that can grant a second tolerance and last-cut kerf allowance.
- Share one `SearchBudget` across every replacement attempt, bar and final pass in a prototype
  `Pack` call. After exhaustion, attempt no further searches; keep the current valid plan.
  `SearchBudget.Used` may be `limit + 1` because the failing charge is its exhaustion sentinel.
  Limit distinct lengths with the existing 200-group guard and recurse over only one bar.
- In the added exact-search helper, leave both collections untouched unless a completed search
  has produced a better, fitting replacement. Transfer original part references, including
  equal-named/value-equal copies. The inherited greedy-helper limitation is documented below.
- Compare the complete candidate against the current production `FirstFitEngine` result. Reject
  it if it uses more bars, leaves more parts, **or** leaves greater unplaced part length in
  `CutFit` units. Keep ties. The existing production whole-job safeguard remains untouched.

The candidate runs the existing greedy improvement before the stronger search, both per-bar and
in the final limited-stock pass. It does not rearrange several packed parts at once, remove the
longest-group anchor, or batch repeated layouts. Its budget is an evaluation-only replacement
budget; this experiment does not inject another budget into production Exhaustive.

## Concrete positive and safety cases

With stock 18 and parts `{10,7,4,3,2,2}`, the greedy replacement keeps `{10,7}`. Starting from
4 greedily takes 3; starting later cannot revisit 4. The pattern search finds `{10,4,2,2}`,
leaving `{7,3}`. This improves the first bar, not the unlimited job's bar count. Zero/tiny search
budgets retain the baseline on this case.

With stock 38 and parts `{30,18,18,15,14,7,4,3,2,2}`, the stronger candidate can increase the
job from three bars to four; with a three-bar limit it leaves a part unplaced. The whole-job
guard rejects both outcomes. Tests also isolate a candidate with equal bar/unplaced-part counts
but greater unplaced length, which must likewise be rejected.

`PatternReplacementTests` has 27 cases covering these outcomes, budget sharing/exhaustion,
kerf/tolerance boundaries (including one unit beyond the tolerance), longest-group retention,
reference identity, depth protection, 300 independent subset-oracle gaps and 300 seeded complete
jobs, plus the known-defect characterization below. The final Release suite passed 259 tests;
both opt-in benchmarks skipped normally and passed when explicitly enabled.

### Known inherited identity defect — not repaired here

Review exposed and execution reproduced a pre-existing production defect outside the seeded
corpus: with four distinct parts all named `copy`, lengths `{7.000005,7,4,3}`, stock 16,
kerf 0 and one available bar, the greedy helper removes by tolerant value equality. It removes
the `7.000005` anchor when trying to remove `7`, then restores the `7` reference again. Both
production First Fit and this prototype return that reference twice and lose the anchor.
The metrics-only whole-job guard cannot detect it.

The new exact-search helper transfers references safely, but the surrounding inherited greedy
calls do not on this input. Thus reference conservation is verified on the stated test/benchmark
fixtures, **not universally for the prototype**. The Pack-level characterization deliberately
asserts the observed defect, not correct conservation; invert it when the separate production
identity repair lands. This is another reason not to promote the prototype. No production fix
was silently included in this evaluation.

## Reproduce

From the repository root, with the usual shared-checkout build lock when needed:

```bash
CUTLIST_REPLACEMENT_BENCHMARK_DIR=/path/to/evidence \
  dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~ReplacementSearchBenchmark'
```

The harness uses `SearchBaselineBenchmark`'s unchanged seeds: 60 repeated-length jobs and
60 high-distinct-length jobs. Each runs through `MultiBinPacker` with unlimited stock and with
finite stock equal to half the production First Fit bar count (integer division, at least one).
Budgets are 10,000, 100,000 and 1,000,000 steps: **720 paired rows**, each with three timed
runs per engine. Pair order alternates; warmup and assertions are outside timed regions.
`replacement-search.json` saves actual input length/count groups and every raw timing and
quality row incrementally. Tests assert reference conservation, exact fit, finite stock limits,
budget cap and non-regression, never elapsed time.

The comparison is the unchanged production First Fit versus a test-only candidate in the same
process. The candidate includes calculating that baseline plus its own trial pipeline; timing
is the cost of this guarded prototype, not an isolated measurement of `MaxFillSearch` and not
a prediction of an optimized production integration. It is not an Exhaustive-candidate comparison.

## Measured results (2026-10-06)

Base: `b5d0f9402c6e3ade5612026028d4eab8f95ab66b`; Release, .NET 10.0.12,
4 reported processors on a shared host. Each row below contains 60 jobs. Time columns are
nearest-rank p50/p95 over each job's median of its three runs, in milliseconds.

At the largest budget:

| Jobs / stock | Bars baseline / trial | Jobs with fewer unplaced parts | Total reduction in unplaced length | Guard rejections | Baseline p50 / p95 ms | Trial p50 / p95 ms |
|---|---:|---:|---:|---:|---:|---:|
| Repeated / unlimited | 5161 / 5161 | 0 | 0 | 14 | 0.395 / 2.111 | 1.113 / 6.011 |
| Distinct / unlimited | 3727 / 3727 | 0 | 0 | 28 | 0.386 / 1.093 | 1.417 / 4.712 |
| Repeated / finite | 2565 / 2565 | 0 | 0 | 13 | 0.586 / 2.828 | 1.263 / 6.261 |
| Distinct / finite | 1846 / 1846 | 2 | 0.6875 inches | 55 | 0.554 / 1.574 | 1.711 / 4.855 |

The two improved finite jobs are `distinct/7` (99 to 98 unplaced parts; 0.6875 inches less
unplaced length) and `distinct/59` (51 to 49 unplaced parts, unchanged unplaced length).
No returned plan worsened any guarded metric. Guard counts are all rejected candidate
pipelines, not a count proven to be caused solely by the added pattern search; some differences
can originate in the inherited greedy per-bar path.

Budget sensitivity:

| Budget | Unlimited jobs saving bars | Finite jobs with fewer unplaced parts | Finite total length gain (inches) | Exhausted: repeated unlimited / finite | Exhausted: distinct unlimited / finite |
|---|---:|---:|---:|---:|---:|
| 10,000 | 0 | 1 | 0.5 | 0 / 0 | 23 / 26 |
| 100,000 | 0 | 2 | 0.6875 | 0 / 0 | 1 / 1 |
| 1,000,000 | 0 | 2 | 0.6875 | 0 / 0 | 0 / 0 |

More budget completes more searches, without improving unlimited bar counts. Raw timings are
retained; shared-host timing distributions are not a formal performance bound.

The existing production search benchmark also passed at its unchanged 1M/25M budgets. All
non-timing fields of its 120 rows matched the retained post-PR-6 baseline exactly, including
First Fit and Exhaustive bar counts, work, fallback and extended-search results. Thus the new
optional capacity argument did not change the measured production results.

Local raw evidence is retained under
`/home/aj/src/CutList/.hermes/progress/pattern-replacement-evaluation/`:
`benchmark/replacement-search.json`, `summary.json`, `measured-hashes.json`, `core-final.trx`,
`production-regression/search-baseline.json`, and `production-comparison.json`.
The measured prototype and harness hashes are recorded there for exact-tree checks.

## Next gate, not implemented scope

Before promotion, demonstrate meaningful whole-job savings on an agreed corpus, then decide how
to integrate the replacement work into a single explicit engine budget without multiplying it per
bar or incumbent/leftover calculation. Keep the baseline guard and prove behavior through the
real Exhaustive orchestrator/fallback path as well as First Fit. Profile the prototype before
removing baseline work. Consider a streaming best-pattern search only if allocation/work evidence
justifies it: current `CutPatterns.Maximal` materializes and sorts the full list, so an interrupted
enumeration cannot yield a partial winning pattern to this adapter. Do not raise the default budget
or weaken the safeguard to manufacture a gain.
