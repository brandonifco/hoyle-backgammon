# 0009: The owner's rulings on two open questions are ruleset version five

**Status:** accepted, 2026-09-15. Supersedes [decision 0008](0008-map-6-0-0-is-ruleset-version-four.md)
where it declines the second parts of two questions. The rest of 0008 stands.

## Context

`RulesFactory.Maps.HoyleBackgammon` 6.0.0 records two questions that the corpus does not settle.
Each is the second part of an existing entry's `ambiguity.question`, with `clarity: ambiguous`,
`fate: unresolved` and `unresolvedReason: RequiresInterpretation`:

- **`must-play-whole-throw`, second part** (BACKGAMMON / Playing / p. 275): whether two orders that
  use the same numbers and reach the same position are one play or two, where the rule that permits
  a move differs between the orders.
- **`bearing-off-eligible`, second part** (BACKGAMMON / Bearing off the Men / p. 275): whether a
  number left after the move that brings the last man home is played under bearing off.

Ruleset version 4 declined both ([0008](0008-map-6-0-0-is-ruleset-version-four.md)). Most games
played to the end reach one of them: 8 of the hundred seeds from 20260900 finished with the first
play always taken, and 3 with the middle play taken.

On 2026-09-15 **Brandon, the engine's owner, ruled on both second parts**:

1. **`bearing-off-eligible`, second part: yes.** Once a player's first number brings his last man
   home, the number left bears off. With the last man on the nine point and six-trois, 9/3 then off
   with the trois is legal, and so is 9/6 then off with the six.
2. **`must-play-whole-throw`, second part: one play.** A play is the position it reaches. Two orders
   of the same numbers that reach the same position are offered once, crediting the rules of the first
   order found. This was the behaviour before 6.0.0 ([0007](0007-an-equivalent-order-is-offered-once-until-the-map-says-otherwise.md)).

**These are the owner's interpretations, not readings of the corpus.** The text still does not
settle either question, so the map does not change. Both entries stay `fate: unresolved`. Nobody
has claimed that the corpus says what the rulings say.

**The first parts are not ruled on**, and they still decline:

- `must-play-whole-throw`: either die alone playable, but not both.
- `bearing-off-eligible`: a man hit after bearing off has begun, who re-enters with men still at
  home.

### What the factory offers for this

The factory and this engine's conventions were checked for a supported way to apply a recorded
owner ruling to an unresolved question instead of declining:

- **`ambiguity.fate` has two values** (`rules-factory/docs/corpus-map.md`, "`ambiguity.fate`";
  [rules-factory 0005](https://github.com/brandonifco/rules-factory/blob/main/docs/decisions/0005-a-field-earns-its-place-by-being-checkable.md)).
  `decision` means "the project rules on what the passage means, records it, and implements as though
  the corpus had said so". `unresolved` means the engine declines. "There is no third value."
  `decision` is the factory's carrier for a ruling, but it lives in the map. Using it would record
  the rulings as what the passage means, which they are not. An engine also cannot set it:
  the overlay holds only `status`, `implementedIn` and `tests` (rules-factory 0015, `method.md`, "What
  an engine owes its map's source"). So the factory has **no supported carrier for an owner's
  ruling that is held by the engine and leaves the map unresolved.**
- **Nothing refuses it either.** Correspondence row 6 predicts `RequiresInterpretation` for an entry
  whose fate is `unresolved`. Since 0005 C that means "declines for at least one input", and the
  declining case ships a test. Both entries still decline their first parts, and each names that
  decline's test. The generated `CorrespondenceTests` check that an `implemented` entry has a
  handler; they do not check which inputs decline. `check-map.py --phase consumer` reads the merged
  map, not runtime behaviour. The engine's own `MapCorrespondenceTests` require that every entry
  predicting `RequiresInterpretation` has a code path returning it, and both still do. So
  `factory verify` and the gate pass an engine that answers a case the question names.
- **What that leaves unenforced.** `corpus-map.md` says that for an `implemented` entry with
  `fate: unresolved`, "the verdict covers every case except the one `ambiguity.question` names". This
  engine now answers part of what each of those two questions names. No check reads that. The
  convention holds only through this record, and through the answer naming the ruling (below).
  [rules-factory 0021](https://github.com/brandonifco/rules-factory/blob/main/docs/decisions/0021-a-gate-outside-the-slice-is-held-by-the-caller.md)
  was also checked. It concerns a gate out of scope whose state the *caller* asserts. That does not
  fit here: the owner rules for every caller, not per call.

## Decision

**The engine applies both rulings, names them wherever a result relies on one, and the ruleset is
`hoyle-1909-backgammon` version 5.**

- **`OwnerRuling`** is a public record: an id (`must-play-whole-throw/2`, `bearing-off-eligible/2`),
  the map entry, the question part (2), who ruled (`Brandon`), when (2026-09-15), the ruling in a
  sentence, and this record's path. `OwnerRulings` holds the two rulings.
- **`LegalPlays.For`** (and so `EntryPoints.MustPlayWholeThrow` and `Game.Play`) no longer declines
  either second part. Rule 4 of the enumeration contract prunes every later order that reaches a state
  already reached, as it did under version 3. Bearing off begins as soon as every man is home,
  partway through a throw included.
- **`Play.Rulings`** lists the rulings a play relies on, and is empty otherwise:
  - `must-play-whole-throw/2` where some order the search pruned reached the play's state under a
    different multiset of authorities. The search records the state where two such orders met, and
    every play reached from that state. From there the pruned order goes on exactly as the offered
    one does, because a move's authority depends only on the position it is played from. Orders under
    the same rules never needed the ruling and are not marked, e.g. bar/22 22/16 and bar/19 19/16
    with a man up.
  - `bearing-off-eligible/2` where the player was not eligible at the start of the throw and a move of
    the play is made under a bearing-off rule.

  Each move's `Authority` is still the map entry that permits it once the ruling is applied: in
  8/6 6/5, the ace is `bearing-off-move-or-remove`. The authority names a rule of the corpus. The
  play names the ruling that made that rule reach the move.
- **The canonical record names the rulings.** Each turn in `GameRecord.ToCanonicalJson` gains
  `rulings`: for each ruling, its `id`, `entry`, `questionPart`, `ruledBy`, `ruledOn` and `record`.
  The value is `[]` where the play relies on none, and `null` where no play was made, as `moves` is.
  The record's shape changes, so **the replay schema is 3**.
- **The declines that stay:** the first parts of both questions, `game-value`'s two overlaps,
  `rubber-scoring`, `inner-table-handedness`, the out-of-scope entries and the double suspension.
  None of them changes.
- **`implementedIn` moves to 5 on every implemented entry**, for the reason 0004 gives.
- **The map is unchanged.** No finding is filed, because the map is right: the corpus does not
  settle either question. If a later map version settles either one, the engine follows the map, and
  that ruling is withdrawn in a new record.

## Consequences

- **The answer is never passed off as Hoyle.** Every result that relies on a ruling says so. A caller
  of the entry point finds the ruling on `Play.Rulings`. A reader of a recorded game finds it on the
  turn, with who ruled and when. An `UnresolvedResult` has nowhere to carry a ruling, and none is
  needed: a decline relies on no ruling.
- **Games finish again as they did under version 3.** Over the hundred seeds from 20260900, 67 finish
  with the first play always taken (59 of them name a ruling on at least one turn), and 33 with the
  middle play taken and the opening throw adopted (30 name one). Under version 4 the figures were 8
  and 3. The games are move for move those of version 3: the same finish counts, lengths and results.
  The declines that remain are the first parts, and `game-value`'s overlap. A tool comparing recorded
  games must refuse to compare a version 5 record with a version 4 one, and a schema 3 record with a
  schema 2 one.
- **The game tests return to their earlier seeds.** `DeterminismTests` and `PlayerCountTests` go
  back to 20260913 (and 1 for the other seed). `SeededGameReplayTests` goes back to 20260914, 65 turns
  and a gammon for White, with one turn naming `bearing-off-eligible/2`. Its hash is re-pinned for
  ruleset 5 and schema 3. `DeterminismTests.A_game_that_finished_under_ruleset_three_declines_citing_page_275`
  becomes `A_game_version_four_declined_finishes_and_names_the_owners_rulings_it_relied_on`.
- **Two tests are replaced.** Version 4's two decline tests become
  `MustPlayWholeThrowEntryPointTests.Two_orders_reaching_the_same_position_under_different_rules_are_one_play_naming_the_owners_ruling`
  and `A_number_left_after_the_last_man_comes_home_bears_off_naming_the_owners_ruling`. Each is named
  in the overlay with the mutations that turned it red: restoring version 4's decline, never naming
  the ruling, and naming it where it is not relied on.
- **The first-part declines keep their tests unchanged:** `Either_die_alone_playable_but_not_both_declines_citing_page_275`,
  `WholeThrowTests.Where_either_die_alone_can_be_played_but_not_both_the_corpus_does_not_settle_it`
  and `The_rule_declines_in_exactly_one_shape_either_die_alone_playable_but_not_both`, and
  `BearingOffEligibilityTests.A_man_re_entered_after_bearing_off_began_is_the_case_the_corpus_does_not_settle`.
- **The factory could carry this.** It has no field for "unresolved in the corpus, ruled by the
  owner". This record and `Play.Rulings` hold that fact, and no factory check sees it. If the factory
  gains a carrier, such as an overlay or engine-side ruling that correspondence row 6 honours, these
  rulings move into it.
