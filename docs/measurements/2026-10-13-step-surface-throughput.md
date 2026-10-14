# What a turn and a playout cost through the step surface

Issue [#21](https://github.com/brandonifco/hoyle-backgammon/issues/21), acceptance criterion "Cost". This records
**measured numbers**; anything that is not a measurement is labelled inference or recommendation. It sets no
target: rules-ai sets none and will measure in its Phase 4.

## What was measured

A seeded fixed batch of **1,000 playouts** from the corpus's starting position
(`Setup.StartingPositionFromCorpus()`), each with the generator `Pcg32.FromSeed(seed, stream: 1)` and a decider
that picks a uniformly random legal play and a random answer to the opening option from its own
`Pcg32.FromSeed(seed, stream: 2)`, for `seed` = 1 to 1,000. The batch is **96,601 turns** (a turn is one player's
throw and play, or one forfeited turn); the playouts average 96.6 turns because the plays are random.

Three loops play that batch:

| Row | What it is |
|---|---|
| `Game.Play` | `Game.Play`, which is now a client of the step surface. It builds a `GameRecord`. |
| `step surface, with Turn records` | `TurnLoop.Next` and the operations on its decisions, driven as `Game.Play` drives them and building the same `Turn` list: the surface plus the bookkeeping a recorder does. |
| `step surface, lean playout` | The surface driven the way a search that wants only the result drives it: no record, no history, the play applied by index. |

All three do the same work. Each prints `check=`, a hash over every playout's turn count, winner and value; it is
the same number on every row and on `Game.Play` before the change (below). `Game.Play` before the change was
measured by running the same harness on `fda763a`, the commit this branch is cut from, with the step rows removed
(the harness's `STEP-BEGIN` to `STEP-END` regions).

The second block times each operation alone, over the 19,311 play states and 19,215 throw states that the first 200
playouts pass through.

## Procedure

```bash
dotnet run -c Release bench/StepSurfaceThroughput.cs 1000 7
```

`bench/StepSurfaceThroughput.cs` is a .NET file-based app referencing `src/HoyleBackgammon`. It is **not** in
`HoyleBackgammon.slnx`, is built and run by nobody's gate and reads a clock: it is a measuring instrument, outside
the determinism rule that governs the engine (`AGENTS.md` section 8). What it measures is deterministic (the same
playouts every run); the times are not. One untimed run of each loop first, so that JIT and tiered compilation
settle; then 7 timed runs, whose times are listed below, and the median is the figure.

## Machine

| | |
|---|---|
| CPU | 13th Gen Intel Core i7-13700K, 24 threads, scaling governor `powersave` (max 5.4 GHz; the frequency was not pinned) |
| Memory | 62 GB |
| OS | Linux 7.0.0-38-generic x86_64 |
| SDK | .NET SDK 10.0.112 (`global.json`), runtime Microsoft.NETCore.App 10.0.12; the harness targets net10.0 |
| Other load | a developer machine in use; load average 1.1 to 1.6 during the runs |
| Commit measured | `844c4ec39234595e4acf85700f381a8ffb995306` (step surface, `Game.Play` as its client, harness). Before the change: `fda763a` |
| Configuration | Release (`-c Release`) |

## Results: whole playouts

Medians of 7 timed runs of the 1,000-playout batch.

| Loop | Median | Turns per second | Playouts per second |
|---|---:|---:|---:|
| `Game.Play` before the change (`fda763a`), run 1 | 2,929.7 ms | 32,973 | 341 |
| `Game.Play` before the change (`fda763a`), run 2 | 2,879.2 ms | 33,552 | 347 |
| `Game.Play` after (`844c4ec`), run 1 | 2,921.7 ms | 33,063 | 342 |
| `Game.Play` after (`844c4ec`), run 2 | 2,890.1 ms | 33,425 | 346 |
| step surface, with `Turn` records, run 1 | 2,933.7 ms | 32,928 | 341 |
| step surface, with `Turn` records, run 2 | 2,882.0 ms | 33,519 | 347 |
| step surface, lean playout, run 1 | 2,893.3 ms | 33,388 | 346 |
| step surface, lean playout, run 2 | 2,852.8 ms | 33,862 | 351 |

Raw times of the 7 timed runs, in ms, verbatim from the harness:

```
fda763a  Game.Play run 1  [2961.4, 2930.6, 2931.6, 2929.7, 2915.7, 2923.3, 2924.3]
fda763a  Game.Play run 2  [2882.4, 2879.2, 2885.9, 2855.0, 2902.4, 2854.5, 2863.3]
844c4ec  run 1
  Game.Play (builds a GameRecord)    [2921.1, 2915.7, 2927.2, 2925.1, 2921.7, 2914.9, 2941.3]
  step surface, with Turn records    [2937.5, 2958.2, 2933.7, 2933.2, 2920.3, 2912.9, 2939.9]
  step surface, lean playout         [2881.4, 2897.0, 2893.6, 2883.6, 2884.7, 2901.6, 2893.3]
844c4ec  run 2
  Game.Play (builds a GameRecord)    [2890.1, 2883.7, 2886.3, 2907.9, 2890.2, 2875.1, 2896.1]
  step surface, with Turn records    [2900.3, 2880.5, 2882.0, 2885.7, 2878.5, 2875.8, 2898.8]
  step surface, lean playout         [2841.6, 2852.8, 2873.6, 2858.8, 2843.0, 2849.6, 2854.7]
```

All rows: `turns=96601 check=3148422241194015605`, including `Game.Play` at `fda763a`.

## Results: each operation alone

Mean over 7 timed runs of the 200-playout sample (19,311 play states, 19,215 throw states), run 2 of 2. Run 1 gave
29.6 us, 0.125 us, 0.027 us, 0.047 us and 0.046 us, in the same order.

| Operation | Calls per run | Time each | Per second |
|---|---:|---:|---:|
| `TurnLoop.Next` at a play state (this is `LegalPlays.For`) | 19,311 | 29.265 us | 34,170 |
| `PlayDecision.Apply(index)` (winner check, next state) | 19,311 | 0.121 us | 8,278,164 |
| `TurnLoop.Next` at a turn-start state (suspension check, option) | 19,311 | 0.022 us | 45,559,148 |
| `TurnLoop.Next` + `ChanceDecision.Apply(chosen throw)` | 19,215 | 0.038 us | 26,610,272 |
| `TurnLoop.Next` + `ChanceDecision.Sample` (two draws) | 19,215 | 0.039 us | 25,384,801 |

## What this shows

Measured:

- The step surface and `Game.Play` take the same time per turn to within the run-to-run spread: the medians of
  `Game.Play` before and after the change differ by less than the spread between two runs of the same code
  (2,879.2 to 2,929.7 ms before; 2,890.1 to 2,921.7 ms after), and the step surface with `Turn` records is within
  the same band. About 33,000 turns per second and about 345 playouts per second on this machine.
- The lean playout is about 1 to 2 percent faster than the one that builds `Turn` records (2,852.8 and 2,893.3 ms
  against 2,882.0 and 2,933.7 ms).
- A play state's `Next` costs about 29 to 30 us. Every other operation costs under 0.2 us. Of a 30 us turn, the
  operations other than `LegalPlays.For` account for roughly 0.2 us.

Inference (not measured):

- The step surface is not asymptotically slower than `Game.Play` per turn, which is the criterion: each operation
  does constant work beyond `LegalPlays.For`, and `Game.Play` makes the same calls. The measurement is consistent
  with that and cannot exclude a difference below the spread, about 1 to 2 percent.
- A search that calls these operations millions of times is bounded by `LegalPlays.For`, about 30 us a play state,
  not by the step surface. If rules-ai's Phase 4 finds the engine the bottleneck, `LegalPlays.For` is where it will
  be, and that is a separate issue with its own numbers.

Recommendation: none beyond the above. Nothing here asks for a change in the step surface.

## Limits of this measurement

One machine, one batch, a random policy, the start position; no other loop (a search's real access pattern, which
repeats `Next` at the same states, would be different), no memory or GC figures, and the frequency scaling was not
controlled. The numbers are evidence about this machine at this commit and nothing else.
