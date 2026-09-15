# 0010: The owner's rulings on three whole questions are ruleset version six

**Status:** accepted, 2026-09-15. Decided by Brandon, the engine's owner, on 2026-09-15. Supersedes
[decision 0009](0009-owner-rulings-are-ruleset-version-five.md) where it keeps declining the first parts of
two questions and `game-value`'s question. The rest of 0009, and its amendment, stand.

## Context

`RulesFactory.Maps.HoyleBackgammon` 6.0.0 leaves three questions open that ruleset version 5 still declined,
wholly or in part. Each entry has `clarity: ambiguous`, `fate: unresolved` and
`unresolvedReason: RequiresInterpretation`:

- **`must-play-whole-throw`, first part** (BACKGAMMON / Playing / p. 275): "An unplayable part is lost … a
  case every modern ruleset settles explicitly." What happens when either die alone can be played, but not
  both.
- **`bearing-off-eligible`, first part** (BACKGAMMON / Bearing off the Men / p. 275): "The stage begins …
  must first bring every man home again." Whether a player who has begun to bear off, and whose man is hit
  and re-enters, may go on bearing off the men still at home.
- **`game-value`, the whole question** (BACKGAMMON / Bearing off the Men / p. 276). Its first part is the
  finish none of the three named results covers: a loser who has borne off a man, been taken up, re-entered
  and run clear of the winner's home table, but is not yet home. Its second part is the two overlaps: a loser
  who has borne off nothing, with a man up or in the winner's home table, answers both the gammon and the
  backgammon condition.

Version 5 declined all of these. 0009 ruled on the second parts of the first two questions and left these
parts open. Since rules-factory 0.6.0 ([its decision 0027](https://github.com/brandonifco/rules-factory/blob/main/docs/decisions/0027-an-owners-ruling-is-held-by-the-engine-and-checked-by-the-factory.md))
a ruling is held in the engine's overlay, quoting the span of the question it answers, and the factory
checks that the rulings' and declines' spans cover each question.

On 2026-09-15 **Brandon ruled on all three**:

1. **`must-play-whole-throw`, first part: the higher number.** When either number alone can be played but not
   both, the player must play the higher number. If only one number can be played at all, it must be played.
   The corpus already settles that case, and no ruling is involved.
2. **`bearing-off-eligible`, first part: no.** A man hit after bearing off has begun, who then re-enters,
   stops the stage. The player may not bear off again until every man is back in his home table.
3. **`game-value`, the whole question.**
   - **The uncovered finish is a hit** (`game-value/1`). A loser who has borne off a man, has none on the
     bar or in the winner's home table, and is not all home, loses a single game.
   - **The overlaps are a backgammon** (`game-value/2`). A loser who has borne off nothing, and has a man
     on the bar or in the winner's home table, loses a backgammon, and only a backgammon, never a gammon as
     well.
   - **The corpus's own cases stand.** A loser all home with a man borne off loses a hit. A loser with
     nothing borne off, none up and none in the winner's home table loses a gammon. A loser who *has* borne off
     and has a man up or in the winner's home table loses a backgammon. None of these relies on a ruling.
   - **The multiples are the corpus's.** A hit pays the single stake (`hit-pays-single-stake`), a gammon
     double (`stake-multiplier`), and a backgammon thrice or four times as agreed
     (`agreed-backgammon-multiple`). The rulings add no value of their own.

**The `game-value` ruling was first worded too broadly.** As first relayed, it said that "a loser who has
borne off at least one man loses a single game (a hit)". Read literally, that also covers a loser who has
borne off and still has a man up or in the winner's home table. The corpus names that loser a backgammon,
and his case is not part of the question. So a ruling in those words would have overruled the corpus,
which a ruling may not do (rules-factory 0027 § 1). The engine raised this before implementing anything.
**Brandon confirmed the narrow version above on 2026-09-15**: the uncovered finish only, and the corpus's
own cases stand.

**These are the owner's interpretations, not readings of the corpus.** The map does not change, all three
entries stay `fate: unresolved`, and no finding is filed. If a later map version settles any of the
questions, the engine follows the map and withdraws that ruling in a new record.

### Where a ruling reaches beyond its own result

`rubber-scoring` is implemented. It answers the two sequences p. 278 states: a first hit, then a second game
won by the same player, or a gammon won by the other. Every other rubber is declined. A rubber is scored
from game results. Once a game's value can rest on a ruling, a rubber scored from that game rests on the
ruling too. Take a first game that is the uncovered finish, a hit only by `game-value/1`: it now starts a
rubber the corpus's sentence scores. 0027 § 4 asks every result that relies on a ruling to name it, and does
not say whether a result *derived* from ruled results is one. Brandon ruled on 2026-09-15 that it is, and
that rulings carry through. The rubber's length, winning total and what a backgammon reckons are not ruled
on, and `rubber-scoring` keeps declining them.

## Decision

**The engine applies the three rulings, names them on every result that relies on one, carries them into a
rubber scored from ruled games, and the ruleset is `hoyle-1909-backgammon` version 6.**

- **The overlay holds them** (0027). `must-play-whole-throw` has `must-play-whole-throw/1` and
  `must-play-whole-throw/2`, `bearing-off-eligible` has `bearing-off-eligible/1` and
  `bearing-off-eligible/2`, and `game-value` has `game-value/1` and `game-value/2`. Each quotes its part
  of the question, and together the spans cover each question. All three items carry **`declines: []`**,
  declaring each question fully ruled. `Generated/Rulings.g.cs` declares the six. `OwnerRulings.cs` names
  the new ones `TheHigherNumberIsCompelled`, `BearingOffStopsUntilEveryManIsHomeAgain`,
  `TheUncoveredFinishIsAHit` and `TheOverlapIsABackgammon`, and adds `OwnerRulings.InOrder`: the rulings
  without repeats, in overlay order, which is the one order every result lists them in.
- **`LegalPlays.For` never declines.**
  - Where the maximal uses of a throw are incomparable, the plays that use the higher number are compelled,
    and each names `must-play-whole-throw/1`.
  - Where a line reaches a state in which `BearingOff.HasReEnteredMidBearOff` holds with a number still to
    play, the line goes on under ordinary movement: not eligible while a man is outside, which is what
    `BearingOff.IsEligible` already gives. Every play reached from that state names
    `bearing-off-eligible/1`, marked the way 0009 marks `must-play-whole-throw/2`. A play that brings the
    last man home and then bears off names `bearing-off-eligible/2` as well.
- **`bearing-off-eligible`'s entry point answers `BearingOffEligibility`**, from `BearingOff.Eligibility`:
  `Eligible`, and `Rulings`, which names `bearing-off-eligible/1` where the question arises. It used to
  answer a bare `bool` and decline there. `BearingOff.IsEligible` stays the literal predicate, for movement.
- **`Outcome.ValueOf` is replaced by `Outcome.ResultOf`, which answers a `GameResult`.** It holds `Winner`,
  `Value` and `Rulings`: `game-value/1` or `game-value/2` where the value relies on one, and empty for the
  corpus's own cases. `game-value`'s entry point answers `AssertedAnswer<GameResult>`. The method still
  returns a `Resolution` because the question stays open in the map, as `LegalPlays.For` does.
- **`GameRecord.ValueRulings`** holds the rulings the game's value relies on, and `GameRecord.Result` gives
  the game as a `GameResult` for scoring a rubber.
- **`Outcome.RubberWinner` answers a `RubberResult`**: `Winner`, and `Rulings`, the union of the two games'
  `GameResult.Rulings` in overlay order. A decline names none. `rubber-scoring`'s overlay item carries no
  `rulings` or `declines`: it has no ruling of its own, and it declines what it declined before.
- **The canonical record gains `valueRulings`**, at the top level beside `value`. It is a list in the same
  shape as each turn's `rulings` (`id`, `entry`, `questionPart`, `ruledBy`, `ruledOn`, `record`), and `[]`
  where the value relies on none. That is a new member, so the record's shape changes and **the replay
  schema is 4**. New rulings on each turn's existing `rulings` would not have changed the shape by
  themselves.
- **`MapCorrespondenceTests` honour `declines: []`.** Row 6 still predicts `RequiresInterpretation` for
  the three entries, because the map is unchanged. For an entry the overlay declares fully ruled, the test
  now requires the opposite: that no hand-written code path declines citing it. A pinned test holds the set
  of fully ruled entries to these three.
- **`implementedIn` moves to 6 on every implemented entry**, for the reason 0004 gives.

## Consequences

- **Every game played from the corpus's start finishes.** Over the hundred seeds from 20260900
  (`Pcg32.FromSeed(seed, stream: 1)`):

  | Decider | Ruleset 5 | Ruleset 6 | Name a ruling (turn or value) | Value by a ruling |
  |---|---:|---:|---:|---:|
  | First play, opening throw not adopted | 67 | 100 | 92 | 0 |
  | Middle play, opening throw adopted | 33 | 100 | 97 | 43 (`game-value/2`) |

  Of the six rulings, `must-play-whole-throw/1` is named in 33 and 35 games, and `bearing-off-eligible/1`
  in 0 and 20. `game-value/1` reached none of these 200 games. Its case needs a loser hit after bearing off
  who runs clear and is not home again by the end, and the tests hold it with asserted positions. A game
  that finished under version 5 is move for move the same under version 6. Each of the new rulings answers
  only a case version 5 declined, and a declined throw or finish ended the game.
- **What still declines:** `inner-table-handedness` (`MissingRulesData`); `doubling-cube`,
  `calling-the-throw` and `strategy-advice` (`OutsideCurrentScope`); every rubber but the two p. 278 states
  (`rubber-scoring`, `RequiresInterpretation`); and two players wholly suspended against each other, which
  only an asserted start reaches (`UnsupportedInteraction`). `LegalPlays.For`, `Outcome.ResultOf` and
  `BearingOff.Eligibility` decline nothing.
- **The seeded replay is re-pinned.** `SeededGameReplayTests` (seed 20260914) plays the same 65 turns and
  the same gammon for White. Its bytes differ in the ruleset and schema versions and in `"valueRulings":[]`.
  With those three set back, the bytes hash to version 5's pin. A tool comparing recorded games must refuse
  to compare a version 6 record with a version 5 one, and a schema 4 record with a schema 3 one.
- **The first-part decline tests became ruling tests.** Each is named in the overlay with the mutations
  that turned it red: restoring version 5's decline, the ruling's other reading, never naming the ruling,
  and naming it where it is not relied on.
  - `WholeThrowTests.Where_either_die_alone_can_be_played_but_not_both_the_higher_is_compelled_naming_the_owners_ruling`
    and `The_owners_ruling_is_named_in_exactly_one_shape_either_die_alone_playable_but_not_both`
  - `MustPlayWholeThrowEntryPointTests.Either_die_alone_playable_but_not_both_plays_the_higher_naming_the_owners_ruling`
  - `BearingOffEligibilityTests.A_man_re_entered_after_bearing_off_began_bears_off_again_only_once_every_man_is_home_naming_the_owners_ruling`
    and `The_re_entry_ruling_is_named_only_where_the_question_arises`
  - `GameValueTests.The_finish_no_named_result_covers_is_a_hit_naming_the_owners_ruling` and the two
    `…before_bearing_off_is_a_backgammon_and_not_a_gammon_naming_the_owners_ruling`
  - `GameValueEntryPointTests.The_finish_no_named_result_covers_resolves_to_a_hit_naming_the_owners_ruling`
    and `Nothing_borne_off_with_a_man_up_or_in_the_winners_home_table_is_a_backgammon_naming_the_owners_ruling`
  - `RubberScoringTests.A_rubber_scored_from_a_game_valued_by_an_owners_ruling_names_that_ruling`
  - `DeterminismTests.A_game_version_five_declined_finishes_and_names_the_owners_rulings_it_relied_on`
    (seed 20260907, middle play), which names `bearing-off-eligible/1` on two turns and `game-value/2` on
    its value
- **Source-breaking changes:**
  - `Outcome.ValueOf` is gone; `Outcome.ResultOf` answers `Resolution<GameResult>`.
  - `Outcome.RubberWinner` answers `Resolution<RubberResult>`.
  - `bearing-off-eligible`'s and `game-value`'s entry points answer `BearingOffEligibility` and
    `AssertedAnswer<GameResult>`.
- **Where a ruling is not carried.** `Outcome.Pays` and `Outcome.Next` take a bare `GameValue`, as the
  `stake-multiplier` and `next-game-opening` requests do. A caller who passes a value that rests on a ruling
  holds that ruling in the `GameResult` or `GameRecord` it came from, and the stake or opener computed from
  it does not repeat it. `GameRecord.Next` is recorded beside `valueRulings`, which covers it. The same holds
  for `BearingOff.IsEligible`: it is the literal predicate, and the entry point's answer is
  `BearingOff.Eligibility`.
