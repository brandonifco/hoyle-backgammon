# Hoyle Backgammon (1909)

A deterministic backgammon engine built from a corpus map, on
[`RulesKernel`](https://www.nuget.org/packages/RulesKernel) 0.2.0 and
[`RulesKernel.Randomness`](https://www.nuget.org/packages/RulesKernel.Randomness) 0.2.0.

The corpus is the Backgammon chapter of *Hoyle's Games Modernized* (1909), Project Gutenberg
eBook 39445 — 8 KB of a 740 KB public-domain text, pinned in `corpus/hoyle.txt` and hashed on
every validation run. The specification is `corpus-map.json`, twenty-four entries covering
that chapter.

**This text predates the doubling cube.** An engine built from a 1909 corpus is a 1909
engine, and `doubling-cube` is recorded as out of scope with that reason rather than omitted.

## What is here

| | |
|---|---|
| `src/Tabletop.Dice` | Dice vocabulary over the kernel's `UniformInt`. Ruleset-agnostic, and its own project so that claim is checkable — [decision 0001](docs/decisions/0001-the-dice-pack-is-its-own-project.md). |
| `src/HoyleBackgammon` | The engine. Board, movement, the bar, bearing off, game value. |
| `corpus-map.json` | The specification, and the only thing the code cites. This is the engine's copy: 21 entries carry `status: implemented` and `implementedIn`, where the factory's copy has them `mapped`. The entries themselves are unchanged — every correction this build found is in `MAP-FINDINGS.md`, not applied here. |
| `MAP-FINDINGS.md` | **Where the map turned out to be wrong.** Sixteen findings. |
| `corpus/hoyle.txt` | The pinned corpus, `boundaryPolicy: pin-in-repo`. |

## Every rule cites its entry and its page

`MapEntries` holds one static per map entry with the citation copied from the map, and every
rule names the entry it implements:

```csharp
/// <see cref="MapEntries.BearingOffHighest"/>: "If, however, he throws a number which he
/// cannot deal with after either of these fashions -- e.g., a six, he is entitled to bear
/// off a man from his highest occupied point."
```

A `Move` carries the entry that authorised it (`move.Authority`), and every unresolved result
carries its entry's locator. `scripts/validate.sh` checks that the code and the map agree
entry for entry — a citation in code that the map does not carry, or spells differently, is a
build failure rather than a matter of care.

Rules the corpus states that the map has **no** entry for are collected in `UnmappedRules`,
so "what did we implement without a map entry" is one file rather than a re-read.

## What the engine declines

| Asked to | Answers | Entry |
|---|---|---|
| Place the men to start | `MissingRulesData` | `starting-position` |
| Double the stake | `OutsideCurrentScope` | `doubling-cube` |
| Play an opening well | `OutsideCurrentScope` | `strategy-advice` |
| Say what a backgammon pays | `RequiresInterpretation` | `stake-multiplier` |
| Play a throw where either die alone goes but not both | `RequiresInterpretation` | `must-play-whole-throw` |
| Value a win the three named results do not cover | `RequiresInterpretation` | `game-value` (finding 4) |
| Continue with both players wholly suspended | `UnsupportedInteraction` | `full-table-suspension` (finding 11) |

The last two have no sanction in the map, which is the point: they are findings, not features.

## Starting a game

The engine cannot place the men, so it demands a position and records who asserted it —
[decision 0002](docs/decisions/0002-a-game-begins-from-an-asserted-position.md).

```csharp
var start = new AssertedPosition(
    Position.Create(
        white: new Dictionary<int, int> { [24] = 2, [13] = 5, [8] = 3, [6] = 5 },
        black: new Dictionary<int, int> { [24] = 2, [13] = 5, [8] = 3, [6] = 5 }),
    AssertedBy: "me",
    Justification: new SourceLocator("hoyle-1909", "BACKGAMMON / The Board and Men / p. 272"));

var record = Game.Play(start, Pcg32.FromSeed(20260913, stream: 1), new FirstOptionDecider());
```

A point is named by how many pips a man on it still has to travel, for the player whose man
it is: 24 at the start of his journey, 1 on his own ace point, 0 borne off, 25 the bar. The
two players' numberings mirror, so a point is `p` for one and `25 - p` for the other. That is
a representation; what the corpus fixes about it is in `Geometry`'s remarks.

## Determinism

Same seed, same ordered decisions, same game. The draws are accounted for exactly: two per
pair of dice thrown, none for a player whose play is wholly suspended (he does not throw),
none for an adopted opening throw (it re-uses the deciding pair). `DeterminismTests` asserts
the count, not only the outcome.

## Verify it

```bash
./scripts/validate.sh full
```

SDK pin, corpus baseline hash, map correspondence, restore, format, then Debug and
Release (`CI=true`) builds with zero warnings and the full test run, with the test run
asserted to have actually happened rather than inferred from an exit code.
