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

    [Fact]
    public void A_man_hit_back_out_mid_bear_off_stops_it()
    {
        var position = Board.Of(
            Board.Men().At(Geometry.BarPip, 1).At(3, 4).RestBorneOff(),
            Board.Men().RestAt(1));

        Assert.False(BearingOff.IsEligible(position, Player.White));
        Assert.Throws<InvalidOperationException>(
            () => BearingOff.MovesForDie(position, Player.White, 3));
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
