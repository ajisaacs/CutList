# Pattern-search benchmark

`CutList.Core.Tests/Benchmarks/SearchBaselineBenchmark.cs` is an opt-in baseline for the
Exhaustive engine's budget-limited pattern search. It is skipped unless
`CUTLIST_SEARCH_BENCHMARK_DIR` names an output directory, so ordinary test runs are unaffected.

```bash
CUTLIST_SEARCH_BENCHMARK_DIR=/path/to/evidence \
  dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~SearchBaselineBenchmark' --logger 'console;verbosity=detailed'
```

Optional: `CUTLIST_SEARCH_BENCHMARK_EXTENDED_BUDGET` (default 25,000,000 steps). Use Release;
Debug timings are not comparable. Record the source commit next to the output yourself.

## What it measures

Two seeded scenario families of 60 jobs each, on unlimited 240" stock with a 1/8" kerf and
lengths in whole sixteenths from 6" to 180":

- `repeated`: 100-300 parts over 2-12 distinct lengths.
- `distinct`: 40-120 distinct lengths, 1-3 of each.

Each job runs through the unchanged production path (`MultiBinPacker` with
`ExhaustiveSearchEngine`, three timed runs after warm-up). The harness then mirrors the engine's
`MinBarsSearch` call to read the work counter, and asserts that the mirror reaches the engine's bar
count and fallback state. It also checks that every part is placed exactly once, by reference.
Jobs that exhaust the budget are searched again with the extended budget.

`search-baseline.json` holds per-job rows and a per-scenario summary:

- `Status`: `proven-optimal` (the search finished, or the plan meets the L2 lower bound),
  `improved-unproven` (out of budget after beating First Fit), or `fallback` (out of budget
  without beating First Fit, reported as "Exhaustive (First Fit fallback)").
- `GapToLowerBoundOnExhausted`: bars above the Martello-Toth L2 bound. This is an upper limit on what
  any search change could save, not an estimate: L2 is not always reachable.
- `Extended`: what the larger budget found. When an extended search finishes, its bar count is the
  proven optimum for that job.

Non-timing fields are deterministic. Timings depend on the host, so compare them only within one
session on the same machine.

## First Fit incumbent

First Fit also tries improving each bar before opening the next: a packed part can be replaced by
remaining smaller parts that fill its space better. The existing longest-length group stays anchored,
and the final leftover swap pass remains in place for limited stock. This is a local heuristic, not a
promise of the fewest bars.

A tighter bar can strand awkward pieces later. First Fit therefore compares this candidate with its
original whole-job pipeline and retains the original if the candidate uses more bars, leaves more
parts unplaced, or leaves a greater total unplaced part length (compared in `CutFit` units). On ties,
the per-bar candidate is kept. Exhaustive uses this selected result as its incumbent/fallback; its
search budget and fallback reporting are unchanged. Repeat-layout batching is separate and is not
implemented by this pass.

Keep the `FirstFitImprovementTests` regressions alongside benchmark comparisons: they cover the
larger-piece versus smaller-combination case, kerf rejection, finite stock, displaced demand,
reference identity, stock orchestration, and whole-job non-regression.

For the test-only bounded last-bar redistribution experiment, see
[last-bar-elimination-evaluation.md](last-bar-elimination-evaluation.md). Allowing temporary
local slack found whole-job savings under a small additional post-pass budget; production
engines remain unchanged pending the documented integration gates.

## Baseline (2026-10-04)

Source `ffd5a53` plus this harness. Release, .NET 10.0.12, 4 processors (hermes.lan, shared VM).
Budget 1,000,000 steps; extended budget 25,000,000. Two further fresh-process runs reproduced every
non-timing field exactly.

| Scenario | Exhausted | Fallback | Improved, unproven | Bars saved vs First Fit | Bars above L2 (exhausted jobs) | Extra bars found at 25M | p50 / p95 / max ms |
|---|---|---|---|---|---|---|---|
| repeated | 21 / 60 | 13 | 8 | 25 (15 jobs) | 56 | 1 (1 job; 11 searches finished) | 1.0 / 49 / 55 |
| distinct | 30 / 60 | 29 | 1 | 3 (3 jobs) | 31 | 0 (all 30 still exhausted) | 5.6 / 75 / 156 |

- On repeated-length jobs the budget mostly costs proof, not bars. In the 11 exhausted jobs that the
  25M search finished, 10 already had the optimal bar count at 1M. Their L2 gap of 32 bars overstated
  the real gap of 1 bar.
- On high-distinct jobs, 29 of the 30 exhausted jobs sit exactly one bar above L2, and 25 times the
  budget changes nothing. More budget is not the lever.

## Exact optima (2026-10-04)

An external check solved all 120 jobs exactly, outside the repository: an arc-flow integer program
(lengths in whole sixteenths, CutFit capacity) in HiGHS 1.15.1. Every job was solved to proven
optimality, and every solver plan passed `CutFit.Fits` with exact part counts. The solver also agreed
with every bar count that the engine or the 25M search had already proven.

| Scenario | Bars: First Fit / engine / optimum | Jobs above optimum | Fallbacks where First Fit is optimal |
|---|---|---|---|
| repeated | 5,163 / 5,138 / 5,135 | 3 (1 bar each) | 11 of 13 |
| distinct | 3,727 / 3,724 / 3,703 | 21 (1 bar each) | 8 of 29 |

- Better search can save at most 3 bars on the repeated-length jobs and 21 on the high-distinct jobs
  (0.57%, one bar on 35% of those jobs). All 21 distinct-length losses are fallbacks, and in 20 of
  them the optimum equals L2. Finding the better plan is the gap there, not proving it.
- The other 19 fallbacks (11 + 8) already have the optimal bar count, and their optimum is above L2.
  For these, only a stronger lower bound can prove optimality and remove the fallback label. Pruning
  does not help.
- Before changing the search, compare every proposed pruning rule against brute force on small
  inputs. Then rerun this benchmark on the same seeds and budget against these tables.
