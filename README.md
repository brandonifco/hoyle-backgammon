# Hoyle Backgammon (1909)

A deterministic backgammon engine built from a corpus map, on
[`RulesKernel`](https://www.nuget.org/packages/RulesKernel) 0.2.0 and
[`RulesKernel.Randomness`](https://www.nuget.org/packages/RulesKernel.Randomness) 0.2.0.

The corpus is the Backgammon chapter of *Hoyle's Games Modernized* (1909), Project Gutenberg
eBook 39445 — 8 KB of a 740 KB public-domain text, pinned in `corpus/hoyle.txt` and hashed on
every validation run. The specification is `corpus-map.json`, twenty-nine entries covering
that chapter, hashed on every run too.

**This text predates the doubling cube.** An engine built from a 1909 corpus is a 1909
engine, and `doubling-cube` is recorded as out of scope with that reason rather than omitted.

## What is here

| | |
|---|---|
| `src/Tabletop.Dice` | Dice vocabulary over the kernel's `UniformInt`. Ruleset-agnostic, and its own project so that claim is checkable — [decision 0001](docs/decisions/0001-the-dice-pack-is-its-own-project.md). |
| `src/HoyleBackgammon` | The engine. Board, movement, the bar, bearing off, game value. |
| `corpus-map.json` | The specification, and the only thing the code cites. This is the engine's copy of the factory's map at `7d13c27`: 26 entries carry `status: implemented` and `implementedIn`, where the factory's copy has them `mapped`. Nothing else differs at all -- the two engine-authored notes are gone, because decision 0005 decoupled `status` from `ambiguity.fate` and left no shortfall for them to apologise for — every correction this build found is in `MAP-FINDINGS.md`, not applied here. The divergence is spelled out in `corpus-manifest.json`, which also pins this copy by SHA-256, because the map is the oracle the gate validates the code against and the engine writes into it. |
| `MAP-FINDINGS.md` | **Where the map turned out to be wrong.** Sixteen findings; ten have since been accepted, in two rounds. |
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

There used to be an `UnmappedRules` here, collecting two rules the corpus states that the map
had no entry for. The corrected map has entries for both (`point-designations`,
`direction-of-travel`), so the class is gone rather than kept empty.

The gate also checks that each citation *resolves*: that the page it names exists in
`corpus/hoyle.txt`, that the page falls inside the section it names, and — for the entries
whose evidence quotes the corpus rather than summarising it — that the quoted sentence is on
the page cited. That last one is what the retracted map got wrong.

## What the engine declines

| Asked to | Answers | Entry |
|---|---|---|
| Say which side of a board is the inner table | `MissingRulesData` | `inner-table-handedness` |
| Double the stake | `OutsideCurrentScope` | `doubling-cube` |
| Play an opening well | `OutsideCurrentScope` | `strategy-advice` |
| Play a throw where either die alone goes but not both | `RequiresInterpretation` | `must-play-whole-throw` |
| Value a win the three named results do not cover | `RequiresInterpretation` | `game-value` |
| Continue with both players wholly suspended | `UnsupportedInteraction` | `full-table-suspension` |

**This table is now a fact rather than a claim.** The gate derives it from the map and checks
it both ways: every unresolved result in `src/` must be the one the correspondence table of
`rules-factory/docs/corpus-map.md` predicts from its entry's fields, and every entry whose
fields predict an unresolved reason must have a code path that returns it. A result the map
does not predict means something was implemented without being mapped; an entry predicting a
result no code returns means the map asserts what the code disproves. The one row it cannot
check is this table's last line: correspondence row 7 is about a *pair* of entries and about
interactions the map does not enumerate, so that line is verified only for its place in the
row order — row 7 is reached only when rows 1-6 do not match — and for its entry being
implemented, as row 7 requires. The gate prints that limit, and the rows this map gives it no
instance of, on every run.

Every one of these is now sanctioned by the map, which two of them were not. `game-value` was
recorded `clarity: clear` -- asserting the corpus determines one answer for every input --
while this line declined; it is `ambiguous` with `fate: unresolved` now, and its `question`
names the finish exactly (finding 4). The double suspension has no entry and gets none: under
`rules-factory/docs/decisions/0006` no sequence of play reaches it, so it is an answer about
what a *caller may assert* rather than about what the rules generate (finding 11).

**What the engine stopped declining.** "Say what a backgammon pays" used to be on this list.
The corpus does not fail to say: it says thrice or four times, as the players agreed. That is
a delegated standard, `agreed-backgammon-multiple`, and the engine now demands the figure,
attributes it and records it alongside the outcome instead of declining -- keeping the bound
the corpus states, which declining threw away. `Outcome.Pays(GameValue)` was removed with it.
See `rules-factory/docs/decisions/0005` and finding 9.

The first is the only fact in this corpus genuinely beyond a plain-text adapter, and **no rule
in this engine reaches it** — every position here is player-relative, so nothing ever asks
which physical compartment of a board is whose. The gate checks that claim rather than taking
it on trust.

## Starting a game

The engine derives the corpus's starting arrangement, but `Game.Play` still demands a position
and records who asserted it, because most games worth playing out do not begin at the start —
[decision 0002](docs/decisions/0002-a-game-begins-from-an-asserted-position.md), amended by
[decision 0003](docs/decisions/0003-the-engine-derives-the-starting-position.md).

```csharp
var start = new AssertedPosition(
    Setup.StartingPositionFromCorpus(),
    AssertedBy: "me",
    Justification: MapEntries.StartingPosition.Locator);

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

SDK pin, corpus and map baseline hashes, map correspondence, citations resolved against the
corpus, restore, format, then Debug and Release (`CI=true`) builds with zero warnings and the
full test run, with the test run asserted to have actually happened rather than inferred from
an exit code.
