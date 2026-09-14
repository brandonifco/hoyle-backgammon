using System.Collections.Immutable;
using RulesKernel.Randomness;
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

    [Theory]
    [InlineData(6)]
    [InlineData(3)]
    public void Where_only_one_die_can_ever_be_played_that_die_is_compelled(int playable)
    {
        // One mobile man on the eight point and one stuck on the far ace point (Black holds
        // White's 18 and 21). For the six, Black also holds White's cinque point, so the trois
        // cannot be played from the eight; for the trois, he holds White's deuce point instead,
        // so the six cannot. Either way exactly one number of the six-trois can be played at
        // all -- the higher or the lower -- and it must be: neither declining the throw
        // altogether nor preferring the higher number is open to him.
        int blockedWhitePip = playable == 6 ? 5 : 2;
        var position = Board.Of(
            Board.Men().At(24, 1).At(8, 1).RestBorneOff(),
            Board.Men().At(7, 2).At(4, 2).At(Geometry.Mirror(blockedWhitePip), 2).RestAt(12));

        var play = Assert.Single(Legal.Plays(position, Player.White, new DiceThrow(6, 3)));

        Assert.Equal(playable, play.PipsUsed);
        var move = Assert.Single(play.Moves);
        Assert.Equal(8, move.From);
        Assert.Equal(8 - playable, move.To);
        Assert.Equal(1, play.Result.Men(Player.White, 8 - playable));
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

    [Fact]
    public void The_rule_declines_in_exactly_one_shape_either_die_alone_playable_but_not_both()
    {
        // A derived consequence of reading the rule as "nothing less than a maximal set": two
        // plays can be incomparable only when the throw has two different numbers, each can
        // be played alone, and neither can be followed by the other. Doublets never decline
        // -- their plays differ only in how many of one number they use, and those counts are
        // totally ordered -- and a throw with one playable die, or none, has a single maximal
        // play. So "unresolved" and that shape must coincide exactly.
        //
        // Checked both ways over every one of the twenty-one throws, in the constructed
        // position above and in every position a set of seeded random games passes through.
        var positions = new List<(Position Position, Player Player)>
        {
            (OneMobileMan(blockTheTroisLanding: false), Player.White),
            (OneMobileMan(blockTheTroisLanding: true), Player.White),

            // The same shape from the bar, which random play rarely reaches: White's last man
            // is up, both entry points for six-trois are open, and Black holds the one point
            // (White's 16) either entry would need to finish the throw.
            (Board.Of(Board.Men().At(Geometry.BarPip, 1).RestBorneOff(), Board.Men().At(9, 2).RestAt(12)), Player.White),
        };
        foreach (ulong seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            positions.AddRange(PositionsPassedThrough(seed));
        }

        int declined = 0;
        foreach (var (position, player) in positions)
        {
            foreach (var thrown in TheTwentyOneThrows())
            {
                bool shape = EitherDieAlonePlayableButNotBoth(position, player, thrown);
                bool unresolved = LegalPlays.For(position, player, Movement.Entitlement(thrown))
                    is Resolution<ImmutableArray<Play>>.Unresolved;

                Assert.True(
                    shape == unresolved,
                    $"{thrown} for {player} in\n{position}\nshape={shape} unresolved={unresolved}");
                declined += unresolved ? 1 : 0;
            }
        }

        // The constructed position declines 6-3 at least; without a decline the check above
        // would hold vacuously for the "unresolved implies the shape" direction.
        Assert.True(declined > 0);
    }

    private static IEnumerable<DiceThrow> TheTwentyOneThrows()
    {
        for (int higher = 1; higher <= 6; higher++)
        {
            for (int lower = 1; lower <= higher; lower++)
            {
                yield return new DiceThrow(higher, lower);
            }
        }
    }

    /// <summary>The shape, computed from single-die moves alone, without LegalPlays.</summary>
    private static bool EitherDieAlonePlayableButNotBoth(Position position, Player player, DiceThrow thrown)
    {
        if (thrown.IsDoublets)
        {
            return false;
        }

        var higher = Movement.MovesForDie(position, player, thrown.Higher);
        var lower = Movement.MovesForDie(position, player, thrown.Lower);
        if (higher.IsEmpty || lower.IsEmpty)
        {
            return false;
        }

        bool both =
            higher.Any(m => !Movement.MovesForDie(position.Apply(player, m.From, m.To), player, thrown.Lower).IsEmpty)
            || lower.Any(m => !Movement.MovesForDie(position.Apply(player, m.From, m.To), player, thrown.Higher).IsEmpty);
        return !both;
    }

    /// <summary>
    /// Every position a random game passes through, with the player to move. Where the corpus
    /// does not settle a throw the walk simply throws again, so a decline does not end it.
    /// </summary>
    private static IEnumerable<(Position Position, Player Player)> PositionsPassedThrough(ulong seed)
    {
        var source = Pcg32.FromSeed(seed, stream: 3);
        var position = Corpus.StartingPosition.Position;
        var player = Player.White;
        for (int turn = 0; turn < 2_000 && Outcome.Winner(position) is null; turn++)
        {
            if (Movement.IsWhollySuspended(position, player))
            {
                player = player.Adversary();
                continue;
            }

            yield return (position, player);

            while (true)
            {
                var legal = LegalPlays.For(position, player, Movement.Entitlement(Opening.Throw(source)));
                if (legal is Resolution<ImmutableArray<Play>>.Resolved resolved)
                {
                    var plays = resolved.Value;
                    position = plays[(int)(source.NextUInt32() % (uint)plays.Length)].Result;
                    break;
                }
            }

            player = player.Adversary();
        }
    }
}
