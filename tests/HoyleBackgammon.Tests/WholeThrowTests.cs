using RulesKernel.Resolution;
using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// "Any part of a throw which cannot be played is lost to the thrower, but every player is
/// compelled to play the whole of his throw if it is possible to do so."
/// </summary>
public class WholeThrowTests
{
    /// <summary>
    /// One mobile man on the eight point and one stuck on the far ace point, with thirteen
    /// already off the board. Black holds White's 18 and 21, so the stuck man can play
    /// neither a six nor a trois.
    /// </summary>
    private static Position OneMobileMan(bool blockTheTroisLanding) => Board.Of(
        Board.Men().At(24, 1).At(8, 1).RestBorneOff(),
        blockTheTroisLanding
            ? Board.Men().At(7, 2).At(4, 2).At(20, 2).RestAt(12)
            : Board.Men().At(7, 2).At(4, 2).RestAt(12));

    [Fact]
    public void A_throw_that_can_be_played_whole_must_be()
    {
        var plays = Legal.Plays(Corpus.StartingPosition.Position, Player.White, new DiceThrow(6, 3));

        Assert.NotEmpty(plays);
        Assert.All(plays, play => Assert.Equal(9, play.PipsUsed));
    }

    [Fact]
    public void Where_only_one_die_can_ever_be_played_that_die_is_compelled()
    {
        // The trois landing is blocked as well, so the six on the eight point is the only
        // number that can be played at all, and it must be.
        var position = OneMobileMan(blockTheTroisLanding: true);

        var play = Assert.Single(Legal.Plays(position, Player.White, new DiceThrow(6, 3)));

        Assert.Equal(6, play.PipsUsed);
        Assert.Equal(2, Assert.Single(play.Moves).To);
    }

    [Fact]
    public void Where_either_die_alone_can_be_played_but_not_both_the_corpus_does_not_settle_it()
    {
        // Playing the six leaves the man on the deuce point, where a trois cannot be played;
        // playing the trois leaves him on the cinque, where a six cannot. Neither line can be
        // extended, and the text gives no rule for choosing between them.
        var position = OneMobileMan(blockTheTroisLanding: false);

        var unresolved = Legal.Unresolved(position, Player.White, new DiceThrow(6, 3));

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Reason);
        Assert.Equal(MapEntries.MustPlayWholeThrow.Locator, unresolved.Locator);
    }

    [Fact]
    public void Each_die_alone_really_is_playable_in_that_position()
    {
        // Without this the test above would pass for the wrong reason -- an unresolved result
        // proves nothing unless both branches exist.
        var position = OneMobileMan(blockTheTroisLanding: false);

        Assert.Equal(new[] { 8 }, Movement.MovesForDie(position, Player.White, 6).Select(m => m.From));
        Assert.Equal(new[] { 8 }, Movement.MovesForDie(position, Player.White, 3).Select(m => m.From));
    }

    [Fact]
    public void A_throw_with_nothing_playable_is_lost_entirely()
    {
        // Both of White's men are shut in: Black holds every landing an ace or a deuce could
        // reach from either of them.
        var position = Board.Of(
            Board.Men().At(24, 1).At(8, 1).RestBorneOff(),
            Board.Men().At(2, 2).At(3, 2).At(18, 2).At(19, 2).RestAt(12));

        var play = Assert.Single(Legal.Plays(position, Player.White, new DiceThrow(2, 1)));

        Assert.Empty(play.Moves);
        Assert.Equal(0, play.PipsUsed);
        Assert.Equal(position, play.Result);
    }

    [Fact]
    public void Doublets_that_cannot_be_played_four_times_are_played_as_far_as_they_go()
    {
        // One mobile man on the eight point walking down by deuces: 8, 6, 4, 2, and then the
        // fourth deuce would take him off the board, which he may not do -- his other man is
        // still on the far ace point. Three of the four numbers, and nothing less.
        var position = Board.Of(
            Board.Men().At(24, 1).At(8, 1).RestBorneOff(),
            Board.Men().At(3, 2).RestAt(12));

        var plays = Legal.Plays(position, Player.White, new DiceThrow(2, 2));

        Assert.All(plays, play => Assert.Equal(6, play.PipsUsed));
        var single = Assert.Single(plays);
        Assert.Equal(1, single.Result.Men(Player.White, 2));
    }
}
