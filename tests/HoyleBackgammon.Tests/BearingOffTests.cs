using RulesKernel.Resolution;
using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

public class BearingOffEligibilityTests
{
    [Fact]
    public void All_fifteen_home_begins_bearing_off()
    {
        Assert.True(BearingOff.IsEligible(Corpus.BearingOffExample, Player.White));
    }

    [Fact]
    public void Fourteen_home_and_one_out_does_not()
    {
        var position = Board.Of(
            Board.Men().At(7, 1).At(1, 14),
            Board.Men().RestAt(1));

        Assert.False(BearingOff.IsEligible(position, Player.White));
    }

    // This was A_man_hit_back_out_mid_bear_off_stops_it, named by bearing-off-eligible in the
    // overlay. It asserted two things. That a man up stops bearing off is kept, and is now
    // enter-from-bar's suspension of the three bearing-off rules, which map 3.0.0 records
    // (blind-mapping resolution rows 11, 14 and 17): it is below, in BearingOffSuspensionTests.
    // That IsEligible is false for him was a reading of the question the same version leaves
    // unresolved -- whether the stage lasts once a man is hit (rows 12 and 13). Ruleset versions 2
    // to 5 declined there. Since version 6 it is the owner's ruling (docs/decisions/0010), and the two
    // tests after this hold the ruling and where it is named.

    [Fact]
    public void A_man_re_entered_after_bearing_off_began_bears_off_again_only_once_every_man_is_home_naming_the_owners_ruling()
    {
        // Ten off, four on his trois point, and a man hit and re-entered, now on his twenty point: he has
        // begun to bear off, a man of his has re-entered, and he has men at home. Whether he may go on bearing
        // off is bearing-off-eligible's question's first part, which the corpus does not settle. Brandon ruled
        // on 2026-09-15 that he may not until every man is back in his home table.
        var atHome = Board.Of(
            Board.Men().At(20, 1).At(3, 4).RestBorneOff(),
            Board.Men().RestAt(13));
        Assert.True(BearingOff.HasReEnteredMidBearOff(atHome, Player.White));

        // Asked directly, through the entry point: not eligible, naming the ruling.
        var asserted = new AssertedPosition(atHome, AssertedBy: nameof(BearingOffEligibilityTests));
        var answer = Assert.IsType<AssertedAnswer<BearingOffEligibility>>(Assert.IsType<Resolution<object>.Resolved>(
            EntryPoints.BearingOffEligible.Resolve(new() { Position = asserted, Player = Player.White })).Value);
        Assert.Same(asserted, answer.Position);
        Assert.False(answer.Value.Eligible);
        var ruling = Assert.Single(answer.Value.Rulings);
        Assert.Same(OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain, ruling);
        Assert.Equal(("bearing-off-eligible/1", "bearing-off-eligible", 1, "Brandon", new DateOnly(2026, 9, 15)),
            (ruling.Id, ruling.EntryId, ruling.QuestionPart, ruling.RuledBy, ruling.RuledOn));

        // Deuce-ace: every play moves men by pip and none bears off, not even 3/1 then off with the ace from
        // the ace point, which the other reading would allow. Every play names the ruling.
        var plays = Legal.Plays(atHome, Player.White, new DiceThrow(2, 1));
        Assert.Contains(plays, p => p.ToString() == "20/18(2) 18/17(1)");
        Assert.Contains(plays, p => p.ToString() == "3/1(2) 3/2(1)");
        Assert.All(plays, p =>
        {
            Assert.DoesNotContain(p.Moves, m => m.BearsOff);
            Assert.All(p.Moves, m => Assert.Equal(MapEntries.MoveByPip, m.Authority));
            Assert.Equal([OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain], p.Rulings.ToArray());
        });

        // Once every man is home again he bears off, within the same throw: the man on the eight point comes
        // home with the six and the trois bears off from the trois point. That play relies on this ruling (the
        // throw began with him re-entered) and on bearing-off-eligible/2 (docs/decisions/0009).
        var eight = Board.Of(Board.Men().At(8, 1).At(3, 4).RestBorneOff(), Board.Men().RestAt(13));
        var homeAndOff = Assert.Single(Legal.Plays(eight, Player.White, new DiceThrow(6, 3)), p => p.ToString() == "8/2(6) 3/off(3)");
        Assert.Equal(
            [OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain, OwnerRulings.BearingOffBeginsWithinTheThrow],
            homeAndOff.Rulings.ToArray());
    }

    [Fact]
    public void The_re_entry_ruling_is_named_only_where_the_question_arises()
    {
        // Nothing borne off: he has not begun to bear off, so he is simply not home yet.
        var notBegun = Board.Of(Board.Men().At(20, 1).RestAt(3), Board.Men().RestAt(13));
        Assert.False(BearingOff.HasReEnteredMidBearOff(notBegun, Player.White));
        Assert.Empty(BearingOff.Eligibility(notBegun, Player.White).Rulings);
        Assert.All(Legal.Plays(notBegun, Player.White, new DiceThrow(2, 1)), p => Assert.Empty(p.Rulings));

        // All home again: both readings agree he is bearing off.
        var home = Board.Of(Board.Men().At(3, 4).RestBorneOff(), Board.Men().RestAt(13));
        Assert.False(BearingOff.HasReEnteredMidBearOff(home, Player.White));
        Assert.True(BearingOff.Eligibility(home, Player.White).Eligible);
        Assert.Empty(BearingOff.Eligibility(home, Player.White).Rulings);
        Assert.All(Legal.Plays(home, Player.White, new DiceThrow(2, 1)), p => Assert.Empty(p.Rulings));

        // No man left at home: nothing to go on bearing off, so both readings agree he moves.
        var noneHome = Board.Of(Board.Men().At(20, 1).RestBorneOff(), Board.Men().RestAt(13));
        Assert.False(BearingOff.HasReEnteredMidBearOff(noneHome, Player.White));
        Assert.Empty(BearingOff.Eligibility(noneHome, Player.White).Rulings);
        Assert.All(Legal.Plays(noneHome, Player.White, new DiceThrow(2, 1)), p => Assert.Empty(p.Rulings));

        // Still up: enter-from-bar suspends bearing off whichever reading holds, so the position names nothing.
        // Entering with a number left brings the question about, and every play of that throw names the ruling.
        var up = Board.Of(Board.Men().At(Geometry.BarPip, 1).At(3, 4).RestBorneOff(), Board.Men().RestAt(13));
        Assert.False(BearingOff.HasReEnteredMidBearOff(up, Player.White));
        Assert.Empty(BearingOff.Eligibility(up, Player.White).Rulings);
        Assert.All(Legal.Plays(up, Player.White, new DiceThrow(2, 1)), p =>
        {
            Assert.Equal(MoveKind.Entry, p.Moves[0].Kind);
            Assert.Equal([OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain], p.Rulings.ToArray());
        });
    }

    [Fact]
    public void Men_already_off_the_board_are_not_outstanding()
    {
        var position = Board.Of(
            Board.Men().At(6, 1).RestBorneOff(),
            Board.Men().RestAt(1));

        Assert.True(BearingOff.IsEligible(position, Player.White));
    }
}

/// <summary>
/// enter-from-bar suspends bearing-off-move-or-remove, bearing-off-highest and
/// bearing-off-doublets since map 3.0.0 (blind-mapping resolution rows 11, 14 and 17): "Until
/// he does this, the play of his other men is suspended", bearing off included.
/// </summary>
public class BearingOffSuspensionTests
{
    [Fact]
    public void A_man_up_mid_bear_off_suspends_bearing_off_until_he_enters()
    {
        // Ten off, four on his trois point, one up. Black's men are all on his own thirteen
        // point, so every number enters. Every die is one his home men could otherwise remove
        // or move with -- a trois from the trois point, a six from the highest point.
        var position = Board.Of(
            Board.Men().At(Geometry.BarPip, 1).At(3, 4).RestBorneOff(),
            Board.Men().RestAt(13));

        for (int die = 1; die <= 6; die++)
        {
            var move = Assert.Single(Movement.MovesForDie(position, Player.White, die));
            Assert.Equal(MoveKind.Entry, move.Kind);
        }

        var refused = Assert.Throws<InvalidOperationException>(
            () => BearingOff.MovesForDie(position, Player.White, 3));
        Assert.Contains(MapEntries.EnterFromBar.Locator.Citation, refused.Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// The worked distribution: "five men on the cinque point, three on the quatre point, three
/// on the deuce, and four on the ace point, the trois and six points being unoccupied."
/// </summary>
public class BearingOffWorkedExampleTests
{
    [Fact]
    public void For_the_quatre_he_may_remove_from_the_quatre_point_or_advance_cinque_to_ace()
    {
        var moves = BearingOff.MovesForDie(Corpus.BearingOffExample, Player.White, 4);

        Assert.Equal(2, moves.Length);
        Assert.Contains(moves, m => m is { From: 5, To: 1, Kind: MoveKind.BearingOffMove });
        Assert.Contains(moves, m => m is { From: 4, To: 0, Kind: MoveKind.BearingOffRemove });
    }

    [Fact]
    public void For_the_trois_he_has_no_man_on_that_point_and_must_play_forward()
    {
        var moves = BearingOff.MovesForDie(Corpus.BearingOffExample, Player.White, 3);

        Assert.Equal(2, moves.Length);
        Assert.Contains(moves, m => m is { From: 5, To: 2, Kind: MoveKind.BearingOffMove });
        Assert.Contains(moves, m => m is { From: 4, To: 1, Kind: MoveKind.BearingOffMove });
        Assert.DoesNotContain(moves, m => m.BearsOff);
    }

    [Fact]
    public void A_six_cannot_be_dealt_with_and_bears_off_from_the_cinque_point()
    {
        var move = Assert.Single(BearingOff.MovesForDie(Corpus.BearingOffExample, Player.White, 6));

        Assert.Equal(5, move.From);
        Assert.True(move.BearsOff);
        Assert.Equal(MoveKind.BearingOffHighest, move.Kind);
        Assert.Equal(MapEntries.BearingOffHighest, move.Authority);
    }

    [Fact]
    public void Deuces_bear_off_three_and_the_fourth_man_is_played_forward()
    {
        // "having only three men on the deuce point, he can only bear off that number; the
        // fourth man must be played forward, either from the cinque or quatre point."
        var plays = Legal.Plays(Corpus.BearingOffExample, Player.White, new DiceThrow(2, 2));

        Assert.All(plays, play => Assert.Equal(8, play.PipsUsed));
        Assert.All(plays, play => Assert.True(play.Result.BorneOff(Player.White) <= 3));

        var three = plays.Where(p => p.Result.BorneOff(Player.White) == 3).ToList();
        Assert.NotEmpty(three);
        Assert.Contains(three, p => p.Moves.Count(m => m.From == 5 && !m.BearsOff) == 1);
        Assert.Contains(three, p => p.Moves.Count(m => m.From == 4 && !m.BearsOff) == 1);
    }

    [Fact]
    public void A_usable_number_must_be_played_forward_rather_than_off_the_highest_point()
    {
        // The trois is usable -- two forward moves exist -- so nothing bears off from the
        // cinque point, which is what the highest-point rule would otherwise reach for.
        var moves = BearingOff.MovesForDie(Corpus.BearingOffExample, Player.White, 3);

        Assert.DoesNotContain(moves, m => m.Kind == MoveKind.BearingOffHighest);
    }
}

public class BearingOffDetailTests
{
    [Fact]
    public void A_forward_move_within_the_table_is_still_subject_to_legal_destination()
    {
        // Black holds White's deuce point with two men, so the cinque cannot advance three.
        // The quatre still can, so this is not the highest-point case.
        var position = Board.Of(
            Board.Men().At(5, 8).At(4, 7),
            Board.Men().At(23, 2).RestAt(13));

        var moves = BearingOff.MovesForDie(position, Player.White, 3);

        Assert.Equal(new[] { 4 }, moves.Select(m => m.From));
    }

    [Fact]
    public void A_blocked_forward_move_bears_off_from_the_highest_point_men_still_above_it()
    {
        // White's men stand on his cinque and ace points; Black holds White's deuce point with
        // two men. A trois can do neither of the two fashions: he has no man on his trois
        // point to remove, and the only forward move a trois could make -- cinque to deuce --
        // is refused by legal-destination. So the number "cannot be dealt with after either of
        // these fashions" and bears a man off the highest occupied point, which is the cinque.
        //
        // The modern rule would refuse it: a man stands above the trois point, so nothing may
        // be borne off from below one. Hoyle's rule as written does not say that, and finding
        // 14 in MAP-FINDINGS.md records that "clear" is not "unsurprising". The difference is
        // observable only here -- every other bearing-off case in this file leaves some
        // forward move playable, which is what kept the two readings indistinguishable.
        var position = Board.Of(
            Board.Men().At(5, 2).At(1, 13),
            Board.Men().At(23, 2).RestAt(1));

        var move = Assert.Single(BearingOff.MovesForDie(position, Player.White, 3));

        Assert.Equal(5, move.From);
        Assert.Equal(Geometry.BorneOffPip, move.To);
        Assert.True(move.BearsOff);
        Assert.Equal(MoveKind.BearingOffHighest, move.Kind);
        Assert.Equal(MapEntries.BearingOffHighest, move.Authority);
        Assert.Equal("5/off(3)", move.ToString());

        // And it is the cinque man that goes, not an ace man: the removal names the highest
        // occupied point and not the point the die names.
        Assert.Equal(1, position.Apply(Player.White, 5, Geometry.BorneOffPip)
            .Men(Player.White, 5));
        Assert.Equal(13, position.Men(Player.White, 1));
    }

    [Fact]
    public void A_forward_move_within_the_table_can_take_up_a_blot()
    {
        var position = Board.Of(
            Board.Men().At(5, 8).At(4, 7),
            Board.Men().At(23, 1).RestAt(13));

        var move = Assert.Single(BearingOff.MovesForDie(position, Player.White, 3), m => m.From == 5);

        Assert.True(move.TakesUpBlot);
        Assert.Equal(2, move.To);
        Assert.Equal(1, position.Apply(Player.White, 5, 2).OnBar(Player.Black));
    }

    [Fact]
    public void Doublets_may_be_played_wholly_by_bearing_off()
    {
        var position = Board.Of(
            Board.Men().At(3, 5).RestBorneOff(),
            Board.Men().RestAt(1));

        var play = Assert.Single(Legal.Plays(position, Player.White, new DiceThrow(3, 3)));

        Assert.Equal(4, play.Moves.Length);
        Assert.All(play.Moves, m => Assert.True(m.BearsOff));
        Assert.Equal(14, play.Result.BorneOff(Player.White));
    }

    [Fact]
    public void The_last_man_off_wins_the_game()
    {
        var position = Board.Of(
            Board.Men().At(2, 1).RestBorneOff(),
            Board.Men().RestAt(1));

        var plays = Legal.Plays(position, Player.White, new DiceThrow(2, 1));
        var play = Assert.Single(plays, p => p.Result.BorneOff(Player.White) == 15);

        Assert.True(BearingOff.HasWon(play.Result, Player.White));
        Assert.Equal(Player.White, Outcome.Winner(play.Result));
    }
}
