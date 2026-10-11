# Bounded last-bar elimination evaluation

## Result and recommendation

A bounded post-pass targeting **one fewer bar** found real savings where the earlier
single-part, fuller-gap replacement trial did not. The useful variant permits a bar to become
**temporarily emptier** during a rearrangement chain; the whole job must still finish on fewer
bars with exactly the same parts and leftovers.

Across the same 120 unlimited-stock seeded jobs, a **10,000-step additional post-pass** saved:

- **8 bars across 7 jobs after production First Fit**.
- **3 bars across 3 jobs after production Exhaustive** (its existing 1,000,000-step budget unchanged).

Increasing the post-pass to 100,000 or 1,000,000 steps found only one additional First Fit bar,
and **no additional Exhaustive bars**. Favor the 10k variant for a future integration experiment,
not a larger default search budget. This is still a test-only prototype: no Core source, engine
catalog, UI/API, default, fallback policy, or deployment changes are included.

These are **extra-budget post-processing** results, not proof that replacing part of Exhaustive's
existing budget with this pass improves an equal-total-budget engine. Production promotion needs
that comparison and an explicit engine/fallback contract.

## Behavior contract

`CutList.Core.Tests/Nesting/LastBarEliminationEngine.cs` wraps an unchanged baseline engine
(default `FirstFitEngine`, optionally `ExhaustiveSearchEngine`). It:

1. Validates the baseline against the request: exact input reference **multiset**, fit in `CutFit`
   integer units, correct stock/kerf, no empty bars, and finite-stock limit. Invalid baselines throw
   before searching, even at zero budget.
2. Copies bin contents into private lists, stably sorts by decreasing integer occupancy, and picks
   the least-used bar as the target. A total-capacity bound can reject impossible attempts cheaply.
3. Tries to redistribute that bar's parts, largest pending first, into the other bars. Search order
   is direct placements, smaller-total single/pair ejections, then (optional relaxed mode)
   equal/larger-total single/pair ejections.
4. In relaxed mode, references already placed or displaced in the current branch are locked
   against further ejection. This prevents swap-back cycles. Locks unwind transactionally;
   repeated occurrences of the same reference are conservatively locked together, not deduplicated.
5. Uses one `SearchBudget` across all nodes, examined destinations, ejection candidates and
   repeated attempts within a `Pack` call. Recursion is capped at 64 moves. Setup, sorting,
   reference validation and the underlying baseline calculation are outside that work counter;
   this is a search-work cap, not a wall-clock bound.
6. Commits only a completed, independently validated packing with exactly one fewer bar and the
   same leftover-reference multiset. Original bins and request items are never edited. Earlier
   completed improvements survive later budget exhaustion. Baseline fallback metadata is copied,
   not upgraded to a new optimality claim.
7. Repeats until the first failed least-used-bar attempt, depth exclusion or budget exhaustion.

The strict mode remains the prototype's constructor default (`allowNonMonotoneEjections=false`)
for reproducibility. It permits only receiving-bar occupancy growth and does not need branch
locks. Neither mode is registered or selected by production.

Both modes are restricted heuristics, not exact optimizers. They choose only the least-used bar,
eject at most two residents at a time, and never search beyond 64 moves. Relaxed mode additionally
prevents moving an already displaced resident again in that branch. A failed attempt is not proof
that a bar cannot be eliminated by some other rearrangement.

## Why allowing temporary slack matters

For stock 10 and the valid baseline `[6,2]; [5,4]; [3]`:

- Strict mode keeps three bars (27 work charges).
- Relaxed mode reaches `[6,4]; [5,3,2]` on two bars (268 charges).

The observed chain is: 3 ejects 2, then 2 ejects 6 (temporarily making that receiving bar
emptier), then 6 ejects 5, and 5 fits beside 3 and 2. The 4 stays put. Requiring every individual
bar to grow monotonically excludes this whole-job improvement.

A separate real First Fit regression, through `MultiBinPacker`, saves **7 to 6 bars** using only
111 strict-mode charges. Stock 10, no kerf, parts:

`[7.5,3,0.5,1.5,2.25,1,7.25,0.5,8.75,7.5,3.75,1,6.25,2.5,4.5,1.5,0.5]`

It also works after a deliberately budget-limited `ExhaustiveSearchEngine(1)` fallback while
preserving that metadata. The main benchmark below uses Exhaustive's actual default budget,
not this one-step regression fixture.

## Benchmark protocol

Base: `b5d0f9402c6e3ade5612026028d4eab8f95ab66b`, independent of the prior replacement trial.
Release, .NET 10.0.12, 4 reported processors on the shared host; measured 2026-10-06.

`LastBarEliminationBenchmark` uses the unchanged `SearchBaselineBenchmark` scenarios and seeds:
60 repeated-length jobs and 60 high-distinct-length jobs, unlimited 240-inch stock, 1/8-inch kerf.
Each runs through `MultiBinPacker` with both First Fit and Exhaustive, at post-pass budgets
10k/100k/1M. There are **720 rows per mode, 1,440 across both modes**, with three alternating-order
baseline/candidate timing pairs per row. Baseline and candidate run in the same process; strict
and relaxed mode runs are separate processes, each warmed up. All correctness checks and JSON
serialization occur outside the timers. Timings include baseline packing and post-pass validation,
not just search. The candidate computes its baseline once, rather than an extra trial packing.

Every timed return passes exact reference/fit/conservation checks, non-increasing bar count,
unchanged fallback metadata and the shared budget cap. Repetitions must agree on counts, work,
attempts, exhaustion and depth diagnostics. This corpus has distinct part references; separate
unit tests cover repeated-reference multiplicity and finite/oversized leftovers. Finite-stock
performance/fulfillment is not benchmarked here; the pass deliberately leaves unplaced demand
unchanged, even if eliminating a bar frees capacity for a separate future fulfillment pass.

### Stock savings

Each table row covers 120 jobs. Numbers in parentheses are jobs improved.

| Mode | Additional budget | Bars saved after First Fit | Bars saved after Exhaustive | Exhausted: First Fit / Exhaustive |
|---|---:|---:|---:|---:|
| Strict | 10,000 | 2 (1) | 0 (0) | 3 / 0 |
| Strict | 100,000 | 2 (1) | 0 (0) | 2 / 0 |
| Strict | 1,000,000 | 2 (1) | 0 (0) | 2 / 0 |
| Relaxed | 10,000 | 8 (7) | 3 (3) | 104 / 100 |
| Relaxed | 100,000 | 9 (7) | 3 (3) | 104 / 100 |
| Relaxed | 1,000,000 | 9 (7) | 3 (3) | 104 / 100 |

At 10k, the relaxed post-pass improves these default Exhaustive results:

| Scenario/job (zero-based) | Before | After | Pass work |
|---|---:|---:|---:|
| repeated/36 | 98 | 97 | 10,001 (exhausted on a later attempt) |
| distinct/42 | 64 | 63 | 5,650 |
| distinct/48 | 91 | 90 | 4,809 |

The extra charged step is `SearchBudget`'s refusal sentinel, not an extra completed search step.
The strict-mode gain is First Fit repeated/20, 91 to 89 bars at all budgets. All strict-mode
non-timing rows on the final two-mode implementation match the initial strict-only implementation.
No measured plan worsened stock use, fit or fulfillment. Most relaxed runs hit the work cap:
these results do **not** establish optimality. At 10k none hit the depth cap; at 1M, 86 First Fit
and 82 Exhaustive jobs visited a depth-limited branch as well as their recorded budget outcome.

### Timing (relaxed mode, milliseconds)

These are nearest-rank p50/p95 across the 60 job medians in each family, each median from three
runs. Baseline and candidate columns belong to the same paired run. Shared-host noise prevents
fine-grained speed claims; overlapping timings are inconclusive, not evidence of zero overhead.

| Underlying engine / family | Pass budget | Baseline p50 / p95 | Candidate p50 / p95 |
|---|---:|---:|---:|
| First Fit / repeated | 10k | 0.338 / 1.313 | 0.429 / 1.620 |
| First Fit / distinct | 10k | 0.387 / 1.049 | 0.496 / 1.127 |
| Exhaustive / repeated | 10k | 1.650 / 51.423 | 1.706 / 52.381 |
| Exhaustive / distinct | 10k | 5.989 / 75.411 | 6.295 / 72.951 |
| First Fit / repeated | 1M | 0.335 / 1.163 | 9.349 / 19.981 |
| First Fit / distinct | 1M | 0.390 / 1.045 | 6.898 / 10.870 |
| Exhaustive / repeated | 1M | 1.615 / 50.519 | 13.545 / 62.547 |
| Exhaustive / distinct | 1M | 6.379 / 78.775 | 9.818 / 86.731 |

The larger search spends substantially more time with no additional Exhaustive stock savings.
No timing threshold is asserted by tests.

## Verification and known baseline defect

- 58 focused last-bar cases pass, including direct/single/pair moves, strict-versus-relaxed
  fixtures, depth boundaries, every-charge interruption rollback, late exhaustion, branch-lock
  cleanup, repeated instances, finite stock, exact tolerance and kerf, and generated small jobs.
- Final Core Release: **290 passed, 2 opt-in benchmarks skipped**, zero failures.
- Both mode benchmarks passed separately. The original production search benchmark also passed
  at its unchanged 1M/25M budgets; all non-timing fields on its 120 rows matched the retained
  pre-experiment reference exactly, including work, fallback and extended-search results.
- Existing production First Fit can duplicate a same-named near-equal part reference on
  `[7.000005,7,4,3]`, stock 16, kerf 0, one available bar. The prototype explicitly rejects this
  invalid baseline, including with zero budget. No production identity repair is hidden here.
  That defect still needs a separate fix before production integration.

### Reproduce

```bash
# Strict mode; use the usual shared-checkout build lock if other .NET jobs are active.
CUTLIST_LAST_BAR_BENCHMARK_DIR=/path/to/strict \
  dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~LastBarEliminationBenchmark'

# Relaxed mode, identical harness and corpus.
CUTLIST_LAST_BAR_ALLOW_NONMONOTONE=1 CUTLIST_LAST_BAR_BENCHMARK_DIR=/path/to/relaxed \
  dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~LastBarEliminationBenchmark'
```

The benchmark is skipped unless the output variable is set. Only the exact value `1` enables
relaxed mode. Output `last-bar-elimination.json` records mode, budgets, input length/count groups,
raw timings and every per-job metric incrementally.

Retained local evidence:
`/home/aj/src/CutList/.hermes/progress/last-bar-elimination/`

- `strict-final/last-bar-elimination.json`, `relaxed-final/last-bar-elimination.json`
- `final-summary.json`, `final-measured-hashes.json`, `core-final.trx`
- `production-regression/search-baseline.json`, `production-comparison.json`

## Next integration gate (not implemented)

Use the measured 10k relaxed pass as the candidate. Fix the separately tracked baseline identity
bug, define whether to reserve these steps inside the existing Exhaustive budget or intentionally
add a separately reported small post-pass budget, and test the actual engine/fallback contract.
Retain transactional validation and the whole-job guard. Validate on additional/held-out jobs
before selecting a production default; these development seeds informed this experiment.
Do not expand the depth cap or default work limit merely because many restricted searches exhaust.

The shared `CLAUDE.md` evaluation-pointer update remains unapplied because the earlier protected-file
approval timed out; maintainer documentation is updated here without bypassing that approval.
