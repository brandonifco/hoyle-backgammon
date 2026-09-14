# Where the map turned out to be wrong

A running log, kept while building this engine from
`rules-factory/examples/hoyle-backgammon/corpus-map.json`. One section per finding: what the
map says, what implementing it revealed, and whether the map or the code is at fault.

The map was worth having. Twenty-one in-scope entries went into code in the order
`dependsOn` gives, and not one of them turned out to be the wrong *unit* of work — no entry
was really two, and no two entries were really one. Every finding below is about a field,
not about the decomposition. That is a better result than the headline count suggests.

Counts: **16 findings. 11 where the map is at fault, 3 where it is right and something about
it is still worth recording, 2 where the method has no field for what was found.** Of the 11:
one is serious (`starting-position`, finding 1); two are entries the map does not have
(2, 3); four are an incomplete `dependsOn` or `gatedBy` (5, 6, 7, 8); and the rest are one
wrong `clarity` (4), one self-contradicting classification (9), one wrong `evidence` (10) and
one uncovered interaction (11).

**Four have since been accepted and the map corrected** (factory commit `de59930`): 1, 2, 3
and 5. Each is marked below with what the corrected map says and what the engine now does.
This repository's copy of the map is the corrected one; see
[decision 0003](docs/decisions/0003-the-engine-derives-the-starting-position.md). The findings
are kept as written rather than rewritten into the present tense, because a log of what a
build found is worth less if it is edited to agree with the outcome.

**Six more were accepted in a second round**, driven by the factory's decisions
`0005-a-field-earns-its-place-by-being-checkable` and
`0006-the-general-rule-governs-entry-and-full-means-adversely-full`: 4, 9, 11, 12, 13 and 14.
Two of those — 11 and 14 — were findings where the map was *right*, and what the second round
added is a record rather than a correction. Same convention: the finding stands as written,
with what was accepted appended.

---

## 1. `starting-position` is not beyond the adapter. It is stated in prose. — **map at fault**

**What the map says.** `status: declined`, `beyondAdapter: { adapter: "plain-text", modality:
"illustration" }`, note: "The rule is fully determined in the corpus, in Fig. 1. The declared
plain-text adapter cannot read a figure."

**What implementing it revealed.** The corpus states the arrangement twice. The second
statement is prose, on p. 272, and a plain-text adapter reads it perfectly:

> The men are arranged at starting as shown in {273} Fig. 1--viz., two of White's men are
> placed on the ace point in Black's inner table, five are placed on the six point in Black's
> outer table, three on the deuce point in White's outer table, and five on the six point in
> White's inner table. Black's men are placed in like manner on the points immediately facing
> these.

"Viz." introduces the figure's content in words. Two plus five plus three plus five is
fifteen, the four points are named unambiguously in the corpus's own designations, and
"immediately facing" fixes Black's men by mirror. Read through those designations it gives
pips 24, 13, 8 and 6 — which is what `tests/HoyleBackgammon.Tests/Corpus.cs` asserted, citing
p. 272, and what every test in this repository plays from. (That fixture is the production
rule now: `Setup.StartingPositionFromCorpus` derives those four numbers and
`StartingPositionTests` reads them back through the corpus's own designations.)

**Why it matters more than the other fifteen.** This is the entry the trial report leads
with, and the one that motivated a schema change (`beyondAdapter`, decision 0004). The
example that justified the field is not an example of the field. The field may still be
right — a rule genuinely locked in an illustration is easy to imagine — but it now has zero
confirmed instances, not one, and the open question "does an adapter silently dropping a
table produce an entry nobody writes" is joined by its opposite: **an entry written as
unreadable when the corpus said it twice.** A mapper who found the figure stopped reading.

**What the engine did.** Implemented the map as written: `Setup.StartingPositionFromCorpus`
returned `MissingRulesData`, and `Game.Play` demanded an `AssertedPosition` from the caller.
Correcting the map from inside the engine would have hidden the finding, which is the more
valuable half of this run. See `docs/decisions/0002`.

**Accepted; the map is corrected.** `starting-position` is now an ordinary `status: mapped`
entry cited to p. 273, with the prose as its evidence and a note recording that it is
over-determined — footnote 67 needs exactly three men on the outer deuce point for its blot to
exist, and nineteen opening lines in *Hints for Play* fit this arrangement and no other. The
engine derives it, `beyondAdapter` passes to the new `inner-table-handedness` entry, and
`docs/decisions/0003` records what that changed here.

---

## 2. There is no entry for the point designations — **map at fault, missing entry**

**What the map says.** `board-tables` covers "The board is two tables, inner and outer", cited
to p. 271. Nothing else covers the board.

**What implementing it revealed.** Between that sentence and the rules of play the corpus
spends a page establishing that each table is marked with twelve points, six at either end;
that they are named ace, deuce, trois, quatre, cinque and six; that the inner tables number
from the far end inward and the outer tables from the bar outward; and that the outer ace
point is called the bar point.

Without all of that, three other entries cannot be read at all. `starting-position`'s prose
names four points by designation. The `bearing-off-move-or-remove` worked example names six.
`enter-from-bar` says a man re-enters on "a vacant point or blot in such table", which
presupposes points to enter on. And nothing in the map says there are twenty-four of them,
which is the single number the whole engine is built around.

`Geometry` implemented it and cited `UnmappedRules.PointDesignations`, a locator with no entry
behind it. `GeometryTests` evidences the complete table of twenty-four names.

**Accepted; the map is corrected.** `point-designations` is an entry, cited to p. 272, and
`Geometry` cites it as `MapEntries.PointDesignations`. `UnmappedRules` is deleted.

---

## 3. There is no entry for the direction of travel — **map at fault, missing entry**

**What the map says.** `move-by-pip`: "Each die moves one man that many points."

**What implementing it revealed.** That is the distance, not the direction. The corpus states
the direction in its own sentence — "The movement of the men of each player is from the ace
point in his opponent's home table towards the like point in his own" — and that sentence is
also the only authority for treating the twenty-four points as a single course with two ends.
It is what makes "his own table" mean anything in the bearing-off section, and what makes the
two players' numberings mirror.

Smaller than finding 2 and the same shape: a load-bearing sentence with no entry. Was cited in
code as `UnmappedRules.DirectionOfTravel`.

**Accepted; the map is corrected.** `direction-of-travel` is an entry, cited to p. 273, and
`Geometry` cites it as `MapEntries.DirectionOfTravel`.

---

## 4. `game-value` is `clarity: clear` and is not — **map at fault**

**What the map says.** `clarity: clear`, which the schema defines as asserting that the
corpus determines exactly one answer for every valid input. Evidence: "All three: adversary
bearing off; not yet bearing off; a man up or in the winner's home table."

**What implementing it revealed.** The three named results do not cover every finish.
Writing them as predicates on the loser:

- hit — all his men home **and** he has begun to bear off
- gammon — he has **not** begun to bear off
- backgammon — he has a man up, or in the winner's home table

A loser who has borne off a man, then been taken up, re-entered, and run that man clear of
the winner's home table satisfies none of them. He has begun to bear off, so it is not a
gammon. His men are not all home, so it is not a hit. He is neither up nor in the winner's
home table, so it is not a backgammon.

It is reachable, not a curiosity: the winner hits a bearing-off opponent's blot from outside
— which he can only do while he is still running, which is exactly when a gammon is in
play — and then finishes first. `GameValueTests.The_three_named_results_do_not_cover_every_finish`
constructs it.

**What the engine does.** Returns `RequiresInterpretation` from `Outcome.ValueOf`, citing
`game-value`. That is an unresolved result whose map entry says it cannot happen. The entry
should be `clarity: ambiguous` with `fate: unresolved`.

**A second, smaller thing in the same entry.** The three are also not disjoint as written. An
earlier draft of this section, and of the comment in `Outcome.ValueOf` that pointed at it,
said the reason was that a man up implies nothing has been borne off, so every backgammon is
also a gammon. **That was false, and it was false in a way this very finding should have
caught:** a player can bear off and then be hit, which is precisely the manoeuvre the gap case
above depends on — the loser `{bar 1, off 3, 3:11}` is a backgammon with three men already
off. The real overlap is narrower: a loser who has borne off nothing *and* has a man up or in
the winner's home table answers the gammon condition and the backgammon condition both. The
corpus plainly means the larger name to win and the engine tests backgammon first, which was
right all along; only the argument for it was wrong. The entry's `clear` still does not record
that an ordering had to be chosen.

**Accepted; the map is corrected.** `game-value` is `clarity: ambiguous` with
`fate: unresolved` and `unresolvedReason: RequiresInterpretation`, and its `question` names the
uncovered finish exactly. It was the live violation of the correspondence table — an entry
asserting the corpus determines one answer for every input, while `Outcome.ValueOf` returned
`RequiresInterpretation` citing it — and the first entry a check of that table would have
failed on. The declining case ships
`GameValueTests.The_three_named_results_do_not_cover_every_finish`, which this section already
named. The second, smaller thing is recorded in the entry's `note` rather than in its
`question`: the ordering is a choice the corpus makes plainly enough to bake in, so it is not
what the entry declines.

---

## 5. `board-tables` depends on `starting-position`, which is declined — **map at fault**

**What the map says.** `board-tables`, `dependsOn: []`, evidence: "Which table is inner is set
by the starting arrangement, not fixed."

**What implementing it revealed.** The evidence the entry demands cannot be produced. Which
physical compartment is the inner table is governed by the arrangement of the men at
starting, and by Fig. 1 for the left-right question — and `starting-position` is the declined
entry. By the correspondence table, an operation whose `value` dependency is unimplemented
returns `MissingRulesData`, so half of `board-tables` inherits the decline through a
dependency the map does not record.

The entry is really two facts: the player-relative structure (each player has an inner and an
outer table, and his home table is where he bears off from), which the engine uses
everywhere; and the identification of those with the physical compartments of a particular
board, which the engine never needs and cannot derive. Only the second depends on
`starting-position`. `Geometry`'s remarks say so explicitly.

**Accepted; the map is corrected.** `board-tables` is narrowed to the player-relative
structure, and the identification with a physical board's compartments is its own entry,
`inner-table-handedness` — which carries the `beyondAdapter` that `starting-position` used to,
and carries it correctly, the left/right identification being stated only in Fig. 1. It is now
this engine's one `MissingRulesData` decline, reachable through `BeyondAdapter` and reached by
no rule here.

---

## 6. No entry is `gatedBy: ["full-table-suspension"]` — **map at fault, incomplete `gatedBy`**

**What the map says.** Six entries carry `gatedBy`: three gated by `enter-from-bar`, three by
`bearing-off-eligible`. `full-table-suspension` gates nothing.

**What implementing it revealed.** It gates more than either of them. "His play is altogether
suspended, the adversary continuing to throw and move" — a wholly suspended player does not
throw, so `throw-two-dice` is unreachable on his turn, and so is everything downstream of it:
`move-by-pip`, `doublets`, `must-play-whole-throw`, `enter-from-bar` itself.

This is the one gate whose absence is *observable*, and not only in a game log. A suspended
player who does not throw does not consume the generator, so the next throw from a seeded
run belongs to his adversary. An engine that modelled suspension as an empty turn — throw,
find nothing playable, pass — would produce a different game from the same seed, from that
point to the end. It would also pass every test about legality, because the set of legal
plays is empty either way. `DeterminismTests.Every_draw_is_accounted_for_by_a_throw_of_two_dice`
is the test that separates them.

---

## 7. `move-by-pip`'s `gatedBy` omits `bearing-off-eligible` — **map at fault, incomplete `gatedBy`**

Once every man is home, `bearing-off-move-or-remove` governs movement within the table and
`move-by-pip` no longer applies on its own terms: a forward move is a bearing-off move, and a
number that cannot be used forward bears a man off instead of being lost. `move-by-pip` names
only `enter-from-bar` as its gate.

Same shape as finding 6, less serious, and it makes the point that `gatedBy` says nothing
about direction: `enter-from-bar` suspends `move-by-pip`, `bearing-off-eligible` supersedes
it, and `full-table-suspension` suspends it — three gates, three different relationships, one
undirected field.

---

## 8. Five `dependsOn` lists are incomplete — **map at fault**

Each of these is a rule the engine could not be written without, missing from the list that
is supposed to order the work.

| Entry | Its `dependsOn` | What it also needs | Why |
|---|---|---|---|
| `full-table-suspension` | `enter-from-bar` | `made-point` | "Each point occupied by two or more men" is exactly `made-point`. |
| `win-condition` | `bearing-off-eligible` | `bearing-off-move-or-remove` | "First to remove all his men" cannot be implemented before a man can be removed. Eligibility is not removal. |
| `game-value` | `win-condition` | `bearing-off-eligible`, `enter-from-bar` | It asks whether the loser has all his men home and has begun to bear off, and whether he has a man up. |
| `bearing-off-move-or-remove` | `bearing-off-eligible` | `legal-destination` | A forward move within the table is still refused by a point the adversary has made, and still takes up a blot. The bearing-off section neither repeats the qualification nor withdraws it — and the corpus's own definition of a backgammon says an adverse man can be standing in the winner's home table. |
| `bearing-off-doublets` | `doublets`, `bearing-off-move-or-remove` | `bearing-off-highest` | Four numbers, any of which may be one "he cannot deal with". |
| `next-game-opening` | `game-value` | `opening-roll` | "After a gammon or backgammon, the players throw again for the right to begin, **as at starting**" is a reference to `opening-roll`. |

Six rows, counted here as one finding because they are one mistake made six times: the list
was filled in from the passage's neighbours rather than from what the rule actually consumes.

---

## 9. `stake-multiplier` contradicts itself — **map at fault, classification**

**What the map says.** `kind: value`, `clarity: ambiguous`, `ambiguity.fate: unresolved`,
`unresolvedReason: RequiresInterpretation` — and then a note: "Delegated to the players, not
to a referee -- the same shape as a regulation delegating judgement to a pilot. **An
assertion, not a gap.**"

**What implementing it revealed.** The note is right and the fields are wrong, and they
cannot both stand. `clarity: ambiguous` asserts the corpus fails to determine an answer. The
corpus determines it completely: a backgammon pays thrice or four times, as the players
agreed. Nothing is unclear; a fact the engine does not have is missing, and the person who
has it is named.

The schema's own open questions say no entry in any of the three maps uses `kind: assertion`
and that either the maps are behind the schema or the category is narrower than claimed. This
entry settles it in the first direction: it is the strongest assertion candidate in either
map, its own note says so, and it is typed `value`.

**What the engine does.** Both, deliberately. `Outcome.Pays(GameValue)` implements the map as
written and returns `RequiresInterpretation` for a backgammon. `Outcome.Pays(GameValue, int)`
implements the note, taking the agreed figure as demanded, attributed input and bounding it to
three or four — which is itself a rule the corpus states and which the `RequiresInterpretation`
reading throws away.

**Accepted; the map is corrected, and this is the finding that moved the schema.** Factory
decision `0005` rules that a judgement the corpus deliberately delegates is `kind: assertion`
— and that the standard is an **entry of its own**, with the rule that consumes it depending
on it, because `kind` is entry-level and this entry is not only the delegation. It states two
figures the corpus fixes outright. So it split: `agreed-backgammon-multiple` (`kind:
assertion`, `clarity: clear`) is the delegated standard, and `stake-multiplier` is
`clarity: clear` with no `ambiguity` block, depending on it.

The engine changed with it, further than "an overload already anticipates it" suggested.
`Outcome.Pays(GameValue)` is **gone** — a source-breaking removal from a shipped public type
with tests on it, and the right outcome: under the correspondence table's row 8 an assertion is
a parameter, not a failure to resolve, so an overload that declines what the corpus delegated
is not a second option but a wrong one. `Pays(GameValue, int)` is gone too. A bare `int`
satisfied *demand* and *never infer* and neither of the other two obligations an assertion
carries: `AgreedBackgammonMultiple` attributes the figure and bounds it, and `StakeDue` records
it alongside the outcome. The shape is `AssertedPosition`'s, which had already answered the
same question for a position.

---

## 10. `throw-two-dice`'s evidence is wrong — **map at fault, evidence**

**What the map says.** Evidence: "That the opening throw is one die and every later throw
two."

**What implementing it revealed.** There is no throw of one die in a game of backgammon. The
roll for the right to begin is one die each, and it is not a throw of the game — it is
`opening-roll`. The opener's first *played* throw is two dice either way: he may "adopt the
points shown by the two dice" (the two singles just thrown, one per player) or throw both
again. So `throw-two-dice`'s "all subsequent throws" never has to reach back past a one-die
turn, because there is not one.

An implementer who took the evidence literally would build a first turn that moves one man
by one number. The rule is right; the evidence describes a game nobody plays.

---

## 11. Two players suspended at once — **map at fault, missing interaction**

Fifteen men a side permits both players to hold all six points of their own home tables with
two men each (twelve) and still have a man on the bar. Then each is wholly suspended against
the other, neither can ever enter, neither can ever move, and the game cannot end. The corpus
only ever describes one player suspended while "the adversary continues to throw and move".

By the correspondence table this is two implemented entries with no entry for their
combination, so `Game.Play` returns `UnsupportedInteraction` citing `full-table-suspension`.
Recorded as a map finding rather than only an engine behaviour because the correspondence
table makes the map answerable for it: an engine's honest answer about what it cannot do
should be derivable from its map, and this one is not.

**Accepted; recorded rather than given an entry, and the reason is decision `0006`.** Whether
the position is reachable *in play* turns on the corpus's conflict about entering from the bar
— factory issue #19, settled by `0006` — and under the reading `0006` adopts it is not: a player's own move never bars his own man, a move never adds
adversary men to the adversary's home table, and an already-suspended player does not move. So
there is no sequence of play for an entry to describe, and the corpus describing no such case
is the right answer rather than an omission. What keeps the branch live is that `Game.Play`
demands an `AssertedPosition`: a caller may assert the deadlock, and
`DeterminismTests.Two_players_suspended_against_each_other_is_an_interaction_no_entry_covers`
does. `UnsupportedInteraction` is therefore an answer about what a caller may assert, which
`Game.Play`'s comment now says in place of the bare "which fifteen men a side does permit".
This is also the third bullet of factory issue #13, resolved from `0006` rather than
independently.

---

## 12. `status` has no value for an entry that is half declined — **the method has no field**

**What the map says.** `must-play-whole-throw` and `stake-multiplier` are `status: mapped`.
The schema's definition of `declined` is "scope is out, the ambiguity's fate is unresolved, or
the entry is beyond the adapter's reach" — and both have `fate: unresolved`, so both are
`declined` by that definition, and the map does not say so.

**What implementing it revealed.** Neither word is true of either entry. `must-play-whole-throw`
is implemented for every throw except one shape and declines that one. `stake-multiplier`
resolves a hit and a gammon and declines a backgammon. Calling them `declined` says the engine
cannot play a throw or pay a stake, which is false and would be read as a much bigger gap than
exists. Calling them `implemented` claims a conformance verdict over a case that has none.

This repository's copy of the map records them as `implemented`, with a note. The honest
shape would be a status that says "implemented, and declines a stated case" — or a rule that
an entry whose ambiguity is partial is two entries.

**Accepted; the method is corrected, and this was the only thing 0005 found genuinely
missing.** `status` and `ambiguity.fate` are **decoupled**: `status` answers *has the engine
built this entry*, `fate` answers *what happens at runtime when the declining case is reached*,
and `implemented` with `fate: unresolved` is now legal and means exactly "built, and declines
the stated case". `declined` is reserved for an entry with no implemented path at all. So the
note apologising for a status that had no value is gone from both entries, and what the map
records is a fact rather than a shortfall.

Two riders came with it, both of which this repository now satisfies. Where `fate: unresolved`
accompanies `implemented`, the conformance verdict covers every case **except** the one
`ambiguity.question` names, and **the declining case ships a test** —
`WholeThrowTests.Where_either_die_alone_can_be_played_but_not_both_the_corpus_does_not_settle_it`,
named in the entry. And the cost, stated where 0005 states it: after the decoupling
`fate: unresolved` means the engine declines for *at least one* input, not for every input, so
nothing in the map distinguishes `must-play-whole-throw`, which declines one shape of throw,
from an entry that declines everything. The totality claim is weaker than it was.

The other half of the finding went the other way. `stake-multiplier` is not a partially
declining entry at all — see finding 9: it was a delegated standard misfiled as an ambiguity,
and it split rather than being accommodated.

---

## 13. `must-play-whole-throw` is ambiguous, and its ambiguity is bounded — **map right, entry under-specified**

**What the map says.** The ambiguity is real: "the text does not say what happens when either
die alone is playable but not both."

**What implementing it revealed.** The map is right, and the entry does not say how much of
the rule the ambiguity costs — which matters, because an implementer reading only the entry
can reasonably conclude the whole rule is unresolved and decline on every throw that cannot
be played whole. That would be far too much declining.

Reading the compulsion as an obligation to play a set of numbers that **cannot be extended**
settles everything except the map's case, and does so from the text rather than from modern
practice. If the whole throw can be played it must be, because nothing else is maximal. A
doublets line playing three numbers strictly contains one playing two, so the three-number
line is compelled and doublets are never ambiguous. A throw where only one die can ever be
played has one maximal set and that die is compelled. Two incomparable maximal sets exist in
exactly one shape: either die alone, and not both. That is the map's case, and the only one.

The engine implements exactly that and declines exactly there. Worth recording because the
entry as written does not let a reader tell that the decline is rare.

**Accepted; the map is corrected.** The entry's `note` now carries the reading — an obligation
to play a set of numbers that cannot be extended — and says that it settles every other throw
from the text, so a reader learns from the entry alone that the decline is one shape of throw
and not a class of them. It names the test, which is the other half: a reader who doubts the
boundary can run it.

---

## 14. `bearing-off-highest` is clear, and "clear" is not "unsurprising" — **map right**

The entry is `clarity: clear` and the text is determinate: a number "which he cannot deal with
after either of these fashions" bears a man off the highest occupied point. Taken literally,
that includes a case the corpus plainly never contemplated — an adverse man standing on a
point inside the player's own home table, blocking the only forward move the number could
make, with no man on the point the number names. The two fashions yield nothing, so the
highest-point removal becomes available, and a modern ruleset would say the number is simply
unplayable.

The map is right: the corpus determines one answer. What the run showed is that `clear`
guarantees determinacy and nothing else — not that the answer matches the game as it is played
now, and not that a reviewer reading the entry would predict it. Recorded because "clear" is
easy to read as "uncontroversial".

**Accepted; the entry gains a rail rather than a field.** 0005 considered and rejected a
`surprising: true` flag — a flag saying "this is surprising" is unfalsifiable — and settled on
the artifact that is not: an entry whose correct reading diverges from what a competent reader
would assume **names the test that fails under the assumed reading**. The test existed;
`bearing-off-highest`'s `note` now names it,
`BearingOffDetailTests.A_blocked_forward_move_bears_off_from_the_highest_point_men_still_above_it`.
That is the whole of the change here, and it is worth exactly what it is worth: nothing can
detect a surprising entry whose author never noticed it was surprising, which is the same class
of failure as a mapper who stopped reading.

---

## 15. The adopted opening throw can never be doublets — **map right, consequence worth stating**

`opening-thrower-option` lets the opener adopt the deciding pair. A tie in `opening-roll`
throws again, so the deciding pair is never a pair of equal numbers, so the adopted throw is
never doublets and `doublets` is unreachable through the adopt branch. Nothing is wrong with
either entry; it is recorded because the interaction is invisible from either one, an
implementer will wire the adopt branch into the doublets path, and the resulting code is dead
in a way no test will report.

---

## 16. Where the corpus lives when the map moves into the engine — **method gap**

`docs/corpus-map.md` says the map lives in the engine's repository, under version control,
beside the code it describes. It says nothing about the corpus, and the manifest says
`boundaryPolicy: pin-in-repo` — which was decided about the factory's repository.

This engine's `SourceBaselineId` claims a 64-hex digest under a named derivation. That claim
is only checkable where the bytes are, so `corpus/hoyle.txt` is committed here too and
`scripts/validate.sh` re-derives the hash on every run and compares it with both the map and
the constant in `MapEntries`. It cost 740 KB to make 8 KB of rules checkable, which is the
ratio the trial report already flagged, now paid twice.

A policy that said only "pin-in-repo" left it open whether the second repository should
duplicate, reference, or trust. Duplicating is what makes the gate hermetic; it is not
obviously right for a commercial corpus, where `never-commit` would put the check behind an
environment variable and the engine could not verify itself at all.
