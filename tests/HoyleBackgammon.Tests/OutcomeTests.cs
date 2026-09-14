using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

public class GameValueTests
{
    private static Position WhiteHasWon(Side black) =>
        Board.Of(Board.Men().RestBorneOff(), black);

    private static GameValue ValueOf(Side black) =>
        Assert.IsType<Resolution<GameValue>.Resolved>(
            Outcome.ValueOf(WhiteHasWon(black), Player.White)).Value;

    [Fact]
    public void The_adversary_bearing_off_makes_it_a_hit()
    {
        // "If the adversary has got all his men into his own home table, and has begun to bear
        // off, the game of the winner is known as a 'hit'."
        Assert.Equal(GameValue.Hit, ValueOf(Board.Men().At(0, 2).At(4, 6).RestAt(3)));
    }

    [Fact]
    public void The_adversary_not_yet_bearing_off_makes_it_a_gammon()
    {
        // "If the winner has borne off all his men before his adversary has begun to do the
        // same, the game is known as a 'gammon'."
        Assert.Equal(GameValue.Gammon, ValueOf(Board.Men().RestAt(13)));
    }

    [Fact]
    public void All_the_adversarys_men_home_but_none_off_is_still_a_gammon()
    {
        // He has not begun to bear off, which is the condition the corpus names.
        Assert.Equal(GameValue.Gammon, ValueOf(Board.Men().At(6, 5).RestAt(3)));
    }

    [Fact]
    public void A_man_up_makes_it_a_backgammon()
    {
        // "while the adversary has still a man or men 'up' (i.e., on the bar)".
        Assert.Equal(GameValue.Backgammon, ValueOf(Board.Men().At(Geometry.BarPip, 1).RestAt(13)));
    }

    [Theory]
    [InlineData(19)]
    [InlineData(22)]
    [InlineData(24)]
    public void A_man_in_the_winners_home_table_makes_it_a_backgammon(int pip)
    {
        // The winner's home table is his own pips 1-6, which the loser counts 24 down to 19.
        Assert.Equal(Quarter.AdversaryInner, Geometry.QuarterOf(pip));
        Assert.Equal(GameValue.Backgammon, ValueOf(Board.Men().At(pip, 1).RestAt(13)));
    }

    [Fact]
    public void A_man_one_point_short_of_the_winners_home_table_is_only_a_gammon()
    {
        // Pip 18 is the adversary's outer table, not the winner's home table. The boundary is
        // load-bearing: one point further and the loser pays three or four times instead of two.
        Assert.Equal(Quarter.AdversaryOuter, Geometry.QuarterOf(18));
        Assert.Equal(GameValue.Gammon, ValueOf(Board.Men().At(18, 1).RestAt(13)));
    }

    [Fact]
    public void The_three_named_results_do_not_cover_every_finish()
    {
        // Two men off, so it is not a gammon; a man on the eighteen point, so his men are not
        // all home and it is not a hit; not up and not in the winner's home table, so it is
        // not a backgammon. Finding 4 in MAP-FINDINGS.md: game-value is recorded clear.
        var position = WhiteHasWon(Board.Men().At(0, 2).At(18, 1).RestAt(3));

        var unresolved = Assert.IsType<Resolution<GameValue>.Unresolved>(
            Outcome.ValueOf(position, Player.White));

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Result.Reason);
        Assert.Equal(MapEntries.GameValue.Locator, unresolved.Result.Locator);
    }

    [Fact]
    public void A_player_who_has_not_won_has_no_game_value()
    {
        Assert.Throws<ArgumentException>(
            () => Outcome.ValueOf(Corpus.StartingPosition.Position, Player.White));
    }

    [Fact]
    public void Nobody_has_won_at_the_start()
    {
        Assert.Null(Outcome.Winner(Corpus.StartingPosition.Position));
    }
}

public class StakeTests
{
    [Fact]
    public void A_hit_pays_the_single_stake() =>
        Assert.Equal(1, Assert.IsType<Resolution<int>.Resolved>(Outcome.Pays(GameValue.Hit)).Value);

    [Fact]
    public void A_gammon_pays_double() =>
        Assert.Equal(2, Assert.IsType<Resolution<int>.Resolved>(Outcome.Pays(GameValue.Gammon)).Value);

    [Fact]
    public void A_backgammon_is_whatever_the_players_agreed_and_the_engine_declines_to_guess()
    {
        var unresolved = Assert.IsType<Resolution<int>.Unresolved>(Outcome.Pays(GameValue.Backgammon));

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Result.Reason);
        Assert.Equal(MapEntries.StakeMultiplier.Locator, unresolved.Result.Locator);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Given_the_agreement_a_backgammon_pays_it(int agreed) =>
        Assert.Equal(agreed, Outcome.Pays(GameValue.Backgammon, agreed));

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(0)]
    public void The_corpus_bounds_the_agreement_to_thrice_or_four_times(int agreed) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Outcome.Pays(GameValue.Backgammon, agreed));

    [Fact]
    public void An_agreement_does_not_change_what_a_hit_or_a_gammon_pays()
    {
        Assert.Equal(1, Outcome.Pays(GameValue.Hit, 4));
        Assert.Equal(2, Outcome.Pays(GameValue.Gammon, 4));
    }
}

public class NextGameTests
{
    [Fact]
    public void The_winner_of_a_hit_throws_first_in_the_game_next_following() =>
        Assert.Equal(NextOpening.WinnerThrowsFirst, Outcome.Next(GameValue.Hit));

    [Theory]
    [InlineData(GameValue.Gammon)]
    [InlineData(GameValue.Backgammon)]
    public void After_a_gammon_or_backgammon_the_players_throw_again_for_the_right(GameValue value) =>
        Assert.Equal(NextOpening.ThrowAgainForTheRight, Outcome.Next(value));
}
