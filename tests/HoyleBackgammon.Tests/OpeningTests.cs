using Xunit;

namespace HoyleBackgammon.Tests;

public class OpeningRollTests
{
    [Fact]
    public void The_higher_single_die_gives_the_right_to_begin()
    {
        var source = ScriptedSource.Faces(5, 2);

        var roll = Opening.RollForTheRight(source);

        Assert.Equal(Player.White, roll.Opener);
        Assert.Single(roll.Attempts);
        Assert.Equal(2, source.Drawn);
    }

    [Fact]
    public void The_other_player_begins_when_his_is_the_higher()
    {
        var roll = Opening.RollForTheRight(ScriptedSource.Faces(2, 5));

        Assert.Equal(Player.Black, roll.Opener);
    }

    [Fact]
    public void In_the_event_of_a_tie_the_players_throw_again()
    {
        var source = ScriptedSource.Faces(3, 3, 6, 6, 1, 4);

        var roll = Opening.RollForTheRight(source);

        Assert.Equal(3, roll.Attempts.Length);
        Assert.Equal(Player.Black, roll.Opener);
        Assert.Equal(6, source.Drawn);
    }

    [Fact]
    public void The_deciding_pair_can_never_be_doublets()
    {
        // Because a tie throws again. This is why the opener's adopted first throw is never
        // played twice over.
        var roll = Opening.RollForTheRight(ScriptedSource.Faces(4, 4, 6, 1));

        Assert.False(roll.Deciding.IsDoublets);
        Assert.Equal(6, roll.Deciding.First);
        Assert.Equal(1, roll.Deciding.Second);
    }

    [Fact]
    public void An_adopted_opening_throw_is_never_doublets_so_it_never_entitles_four_numbers()
    {
        // The derived consequence, for every way the dice can fall rather than one scripted
        // case: whatever pair is thrown first -- all thirty-six, ties included -- followed if
        // need be by every non-tie that could settle it, the throw the opener adopts is never
        // a pair, and so the doublets branch of Movement.Entitlement is unreachable through
        // it. If the opening rule ever stops re-throwing ties, this goes red.
        int checkedRolls = 0;
        for (int first = 1; first <= 6; first++)
        {
            for (int second = 1; second <= 6; second++)
            {
                for (int settleHigh = 1; settleHigh <= 6; settleHigh++)
                {
                    for (int settleLow = 1; settleLow <= 6; settleLow++)
                    {
                        if (settleHigh == settleLow)
                        {
                            continue;
                        }

                        var source = ScriptedSource.Faces(first, second, settleHigh, settleLow);
                        var roll = Opening.RollForTheRight(source);
                        var adopted = Opening.OpeningThrow(roll, adopt: true, source);

                        Assert.False(adopted.IsDoublets, $"adopted {adopted} after {first}-{second}");
                        Assert.Equal(2, Movement.Entitlement(adopted).Length);
                        checkedRolls++;
                    }
                }
            }
        }

        Assert.Equal(36 * 30, checkedRolls);
    }
}

public class OpeningThrowerOptionTests
{
    [Fact]
    public void He_may_adopt_the_points_shown_by_the_two_dice()
    {
        var source = ScriptedSource.Faces(6, 2);
        var roll = Opening.RollForTheRight(source);

        var thrown = Opening.OpeningThrow(roll, adopt: true, source);

        Assert.Equal(roll.Deciding, thrown);
        Assert.Equal(2, source.Drawn);
    }

    [Fact]
    public void Or_he_may_throw_again_with_both_dice()
    {
        var source = ScriptedSource.Faces(6, 2, 5, 5);
        var roll = Opening.RollForTheRight(source);

        var thrown = Opening.OpeningThrow(roll, adopt: false, source);

        Assert.Equal(5, thrown.First);
        Assert.Equal(5, thrown.Second);
        Assert.True(thrown.IsDoublets);
        Assert.Equal(4, source.Drawn);
    }

    [Fact]
    public void An_adopted_opening_throw_entitles_two_numbers_and_a_re_thrown_doublet_four()
    {
        var source = ScriptedSource.Faces(6, 2, 5, 5);
        var roll = Opening.RollForTheRight(source);

        Assert.Equal(2, Movement.Entitlement(Opening.OpeningThrow(roll, true, source)).Length);
        Assert.Equal(4, Movement.Entitlement(Opening.OpeningThrow(roll, false, source)).Length);
    }

    [Fact]
    public void Every_subsequent_throw_is_with_both_dice()
    {
        var source = ScriptedSource.Faces(4, 1);

        var thrown = Opening.Throw(source);

        Assert.Equal(4, thrown.First);
        Assert.Equal(1, thrown.Second);
        Assert.Equal(2, source.Drawn);
    }
}

public class DieFacesTests
{
    [Fact]
    public void A_throw_shows_the_twenty_one_throws_the_corpus_names_and_no_other()
    {
        // die-faces: "all the possible throws", then twenty-one of them, from ACES to SIXES.
        // Every raw word each die can be handed from 0 to 11 covers each face at least twice
        // over, so a seventh face would appear here if the dice admitted one.
        var faces = new SortedSet<int>();
        var throws = new HashSet<(int Higher, int Lower)>();
        for (uint first = 0; first < 12; first++)
        {
            for (uint second = 0; second < 12; second++)
            {
                var thrown = Opening.Throw(new ScriptedSource(first, second));
                faces.Add(thrown.First);
                faces.Add(thrown.Second);
                throws.Add((thrown.Higher, thrown.Lower));
            }
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, faces);
        Assert.Equal(21, throws.Count);
    }
}
