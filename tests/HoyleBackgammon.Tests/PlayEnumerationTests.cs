using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// The list a replay indexes into. A recorded game is a seed and a list of indices into
/// <see cref="LegalPlays.For"/>'s result, so its length and order are part of the replay
/// contract as much as the dice are. The rule that fixes them is stated on
/// <see cref="LegalPlays.For"/>; these tests pin it.
/// </summary>
public class PlayEnumerationTests
{
    [Fact]
    public void The_plays_for_six_trois_from_the_start_are_these_fourteen_in_this_order()
    {
        // Non-trivial on purpose: several orderings of the same two moves reach the same state
        // (24/21 then 24/18 is 24/18 then 24/21; 24/21 21/15 is 24/18 18/15; 13/10 10/4 is
        // 13/7 7/4), and only the one enumerated first survives. Removing the memo, or
        // enumerating the lower die first, or origins lowest-first, changes this list -- and
        // every recorded index with it.
        var plays = Legal.Plays(Corpus.StartingPosition.Position, Player.White, new DiceThrow(6, 3));

        Assert.Equal(
            new[]
            {
                "24/18(6) 24/21(3)",
                "24/18(6) 18/15(3)",
                "24/18(6) 13/10(3)",
                "24/18(6) 8/5(3)",
                "24/18(6) 6/3(3)",
                "13/7(6) 24/21(3)",
                "13/7(6) 13/10(3)",
                "13/7(6) 8/5(3)",
                "13/7(6) 7/4(3)",
                "13/7(6) 6/3(3)",
                "8/2(6) 24/21(3)",
                "8/2(6) 13/10(3)",
                "8/2(6) 8/5(3)",
                "8/2(6) 6/3(3)",
            },
            plays.Select(p => p.ToString()));
    }

    [Fact]
    public void The_plays_for_double_deuces_from_the_start_are_seventy_five_first_and_last_these()
    {
        // Doublets are where the memo collapses most: four deuces can be ordered many ways
        // into one state. The count is the memo's; the ends are the enumeration order's.
        var plays = Legal.Plays(Corpus.StartingPosition.Position, Player.White, new DiceThrow(2, 2));

        Assert.Equal(75, plays.Length);
        Assert.Equal("24/22(2) 24/22(2) 22/20(2) 22/20(2)", plays[0].ToString());
        Assert.Equal("24/22(2) 24/22(2) 22/20(2) 20/18(2)", plays[1].ToString());
        Assert.Equal("6/4(2) 6/4(2) 4/2(2) 4/2(2)", plays[^1].ToString());
    }

    [Fact]
    public void The_same_throw_in_the_same_position_gives_the_same_list_every_time()
    {
        var first = Legal.Plays(Corpus.StartingPosition.Position, Player.White, new DiceThrow(3, 6));
        var second = Legal.Plays(Corpus.StartingPosition.Position, Player.White, new DiceThrow(6, 3));

        // Throw order does not matter either: the entitlement is higher first.
        Assert.Equal(first.Select(p => p.ToString()), second.Select(p => p.ToString()));
    }
}
