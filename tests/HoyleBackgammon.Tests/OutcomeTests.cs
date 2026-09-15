using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

public class GameValueTests
{
    private static Position WhiteHasWon(Side black) =>
        Board.Of(Board.Men().RestBorneOff(), black);

    private static GameResult ResultOf(Position position) =>
        Assert.IsType<Resolution<GameResult>.Resolved>(Outcome.ResultOf(position, Player.White)).Value;

    /// <summary>A result the corpus names unaided: its value, after checking it names no owner's ruling.</summary>
    private static GameValue ValueOf(Side black)
    {
        var result = ResultOf(WhiteHasWon(black));
        Assert.Equal(Player.White, result.Winner);
        Assert.Empty(result.Rulings);
        return result.Value;
    }

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
        // "while the adversary has still a man or men 'up' (i.e., on the bar)". The loser has
        // borne off two, so he has begun to bear off and this is not also a gammon. It used to
        // be a loser with nothing borne off, which answers the gammon condition as well; the
        // map names that overlap as unresolved since 3.0.0 (blind-mapping resolution row 52),
        // and A_man_up_before_bearing_off_is_a_backgammon_and_not_a_gammon_naming_the_owners_ruling now holds it.
        // Borne off and up, he is the corpus's own backgammon, and names no ruling (docs/decisions/0010).
        Assert.Equal(
            GameValue.Backgammon,
            ValueOf(Board.Men().At(Geometry.BarPip, 1).At(0, 2).RestAt(3)));
    }

    [Fact]
    public void A_man_up_before_bearing_off_is_a_backgammon_and_not_a_gammon_naming_the_owners_ruling()
    {
        // Nothing borne off: "before his adversary has begun to do the same" (a gammon). A man
        // on the bar: "a man or men 'up'" (a backgammon). game-value's question names this
        // loser since map 3.0.0 (blind-mapping resolution row 52), and the corpus does not say
        // which result he suffers; ruleset versions 2 to 5 declined. Brandon ruled on 2026-09-15 that
        // he loses a backgammon, and only a backgammon (docs/decisions/0010), so the result names the ruling.
        var result = ResultOf(WhiteHasWon(Board.Men().At(Geometry.BarPip, 1).RestAt(13)));

        Assert.Equal(GameValue.Backgammon, result.Value);
        var ruling = Assert.Single(result.Rulings);
        Assert.Same(OwnerRulings.TheOverlapIsABackgammon, ruling);
        Assert.Equal(("game-value/2", "game-value", 2), (ruling.Id, ruling.EntryId, ruling.QuestionPart));

        // It pays what the corpus says a backgammon pays: the agreed multiple, not a gammon's double as well.
        Assert.Equal(4, Outcome.Pays(result.Value, new AgreedBackgammonMultiple(4, "GameValueTests")).Multiple);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(22)]
    [InlineData(24)]
    public void A_man_in_the_winners_home_table_makes_it_a_backgammon(int pip)
    {
        // The winner's home table is his own pips 1-6, which the loser counts 24 down to 19.
        // The loser has borne off two, so he has begun to bear off and this is not also a gammon.
        // It used to be a loser with nothing borne off, who answers the gammon condition as well;
        // map 4.0.0 names that overlap in game-value's question (finding 17, rules-factory#102),
        // and A_man_in_the_winners_home_table_before_bearing_off_is_both_a_gammon_and_a_backgammon
        // now holds it.
        Assert.Equal(Quarter.AdversaryInner, Geometry.QuarterOf(pip));
        Assert.Equal(GameValue.Backgammon, ValueOf(Board.Men().At(pip, 1).At(0, 2).RestAt(13)));
    }

    [Theory]
    [InlineData(19)]
    [InlineData(22)]
    [InlineData(24)]
    public void A_man_in_the_winners_home_table_before_bearing_off_is_a_backgammon_and_not_a_gammon_naming_the_owners_ruling(int pip)
    {
        // Nothing borne off: "before his adversary has begun to do the same" (a gammon). A man in
        // the winner's home table: "or in his (the winner's) home table" (a backgammon). No man up,
        // so this is the arm of the overlap map 3.0.0's question left out and 4.0.0 names
        // (finding 17, rules-factory#102); the corpus does not say which result he suffers. Brandon's
        // ruling of 2026-09-15 is the same for both arms: a backgammon, naming it (docs/decisions/0010).
        var position = WhiteHasWon(Board.Men().At(pip, 1).RestAt(13));

        var result = ResultOf(position);

        Assert.Equal(0, position.OnBar(Player.Black));
        Assert.Equal(GameValue.Backgammon, result.Value);
        Assert.Equal([OwnerRulings.TheOverlapIsABackgammon], result.Rulings.ToArray());
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
    public void The_finish_no_named_result_covers_is_a_hit_naming_the_owners_ruling()
    {
        // Two men off, so it is not a gammon; a man on the eighteen point, so his men are not
        // all home and it is not a hit; not up and not in the winner's home table, so it is
        // not a backgammon. Finding 4 in MAP-FINDINGS.md; game-value's question's first part, and ruleset
        // versions 2 to 5 declined. Brandon ruled on 2026-09-15 that such a loser loses a single game, a hit
        // (docs/decisions/0010), so the result names the ruling, and pays the single stake.
        var result = ResultOf(WhiteHasWon(Board.Men().At(0, 2).At(18, 1).RestAt(3)));

        Assert.Equal(GameValue.Hit, result.Value);
        var ruling = Assert.Single(result.Rulings);
        Assert.Same(OwnerRulings.TheUncoveredFinishIsAHit, ruling);
        Assert.Equal(("game-value/1", "game-value", 1), (ruling.Id, ruling.EntryId, ruling.QuestionPart));
        Assert.Equal(1, Outcome.Pays(result.Value, new AgreedBackgammonMultiple(3, "GameValueTests")).Multiple);

        // The ruling is only for him. All home again with men off is the corpus's hit, and borne off with a man
        // still in the winner's home table the corpus's backgammon: neither names it.
        Assert.Equal(GameValue.Hit, ValueOf(Board.Men().At(0, 2).At(6, 1).RestAt(3)));
        Assert.Equal(GameValue.Backgammon, ValueOf(Board.Men().At(0, 2).At(19, 1).RestAt(3)));
    }

    [Fact]
    public void A_player_who_has_not_won_has_no_game_value()
    {
        Assert.Throws<ArgumentException>(
            () => Outcome.ResultOf(Corpus.StartingPosition.Position, Player.White));
    }

    [Fact]
    public void Nobody_has_won_at_the_start()
    {
        Assert.Null(Outcome.Winner(Corpus.StartingPosition.Position));
    }
}

/// <summary>
/// stake-multiplier and the assertion it depends on. The corpus names the players as the
/// decider for a backgammon, so the engine demands the figure rather than declining it —
/// rules-factory/docs/decisions/0005, and finding 9 in MAP-FINDINGS.md.
/// </summary>
public class StakeTests
{
    private static AgreedBackgammonMultiple Agreed(int multiple) =>
        new(multiple, AgreedBy: "StakeTests, standing in for the players");

    [Fact]
    public void A_hit_pays_the_single_stake() =>
        Assert.Equal(1, Outcome.Pays(GameValue.Hit, Agreed(4)).Multiple);

    [Fact]
    public void A_gammon_pays_double() =>
        Assert.Equal(2, Outcome.Pays(GameValue.Gammon, Agreed(4)).Multiple);

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void A_backgammon_pays_the_multiple_the_players_agreed(int agreed) =>
        Assert.Equal(agreed, Outcome.Pays(GameValue.Backgammon, Agreed(agreed)).Multiple);

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(-3)]
    public void The_corpus_bounds_the_agreement_to_thrice_or_four_times(int agreed)
    {
        // "Either thrice or four times (as may have been agreed)" delegates the figure and
        // bounds it in the same breath. The bound is a rule the corpus states, so it survives
        // into the type rather than being discarded with the delegation.
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AgreedBackgammonMultiple(agreed, AgreedBy: "StakeTests"));

        Assert.Contains(
            MapEntries.AgreedBackgammonMultiple.Locator.Citation,
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void An_agreement_does_not_change_what_a_hit_or_a_gammon_pays()
    {
        Assert.Equal(1, Outcome.Pays(GameValue.Hit, Agreed(3)).Multiple);
        Assert.Equal(1, Outcome.Pays(GameValue.Hit, Agreed(4)).Multiple);
        Assert.Equal(2, Outcome.Pays(GameValue.Gammon, Agreed(3)).Multiple);
        Assert.Equal(2, Outcome.Pays(GameValue.Gammon, Agreed(4)).Multiple);
    }

    [Theory]
    [InlineData(GameValue.Hit)]
    [InlineData(GameValue.Gammon)]
    [InlineData(GameValue.Backgammon)]
    public void The_agreement_is_recorded_alongside_the_outcome(GameValue value)
    {
        // Demand, attribute, record alongside the outcome, never infer. The first three are
        // the three assertions below; the fourth is that there is no overload without it.
        var agreement = new AgreedBackgammonMultiple(
            AgreedBackgammonMultiple.FourTimes,
            AgreedBy: "the two players, before the first game of the rubber",
            Justification: MapEntries.AgreedBackgammonMultiple.Locator);

        var due = Outcome.Pays(value, agreement);

        Assert.Equal(value, due.Value);
        Assert.Same(agreement, due.Agreement);
        Assert.Equal(
            "the two players, before the first game of the rubber", due.Agreement.AgreedBy);
        Assert.Equal(MapEntries.AgreedBackgammonMultiple.Locator, due.Agreement.Justification);
    }

    [Fact]
    public void The_map_leaves_who_agreed_to_the_caller()
    {
        // "(as may have been agreed)" names nobody, so map 5.0.0's assertedBy is caller
        // (rules-factory decision 0025), and the engine takes whatever party the caller names.
        Assert.Equal(new[] { "caller" }, MapEntries.AgreedBackgammonMultiple.AssertedBy.AsEnumerable());
        Assert.Equal(new[] { "caller" }, Registry.Entry("agreed-backgammon-multiple").AssertedBy.AsEnumerable());
        Assert.Equal("a club's standing terms", new AgreedBackgammonMultiple(3, "a club's standing terms").AgreedBy);
    }

    [Fact]
    public void An_agreement_nobody_is_answerable_for_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new AgreedBackgammonMultiple(3, null!));
        Assert.Throws<ArgumentException>(() => new AgreedBackgammonMultiple(3, "   "));
    }

    [Fact]
    public void The_figure_is_demanded_and_never_inferred() =>
        Assert.Throws<ArgumentNullException>(() => Outcome.Pays(GameValue.Backgammon, null!));

    [Fact]
    public void An_uncited_agreement_says_so_rather_than_implying_a_source()
    {
        // Two people at a board have nothing to cite, and the record should not read as
        // though they did.
        Assert.Contains(
            "uncited",
            new AgreedBackgammonMultiple(3, "two players").ToString(),
            StringComparison.Ordinal);
    }
}

/// <summary>
/// rubber-scoring, new in map 3.0.0 (blind-mapping resolution rows 114 and 115): the two
/// sequences the corpus's one sentence about a rubber settles, and a decline for the rest.
/// </summary>
public class RubberScoringTests
{
    private static Resolution<RubberResult> Rubber(params GameResult[] games) => Outcome.RubberWinner(games);

    /// <summary>The winner of a rubber the corpus scores unaided, after checking it names no owner's ruling.</summary>
    private static Player Winner(Resolution<RubberResult> rubber)
    {
        var result = Assert.IsType<Resolution<RubberResult>.Resolved>(rubber).Value;
        Assert.Empty(result.Rulings);
        return result.Winner;
    }

    [Theory]
    [InlineData(GameValue.Hit)]
    [InlineData(GameValue.Gammon)]
    [InlineData(GameValue.Backgammon)]
    public void Having_lost_the_first_hit_he_who_loses_the_next_game_has_lost_the_rubber(GameValue next)
    {
        // "if he loses the next game, he has lost the rubber also": however the next game is won.
        Assert.Equal(Player.White, Winner(Rubber(new(Player.White, GameValue.Hit), new(Player.White, next))));
    }

    [Fact]
    public void Having_lost_the_first_hit_he_who_secures_a_gammon_wins_the_rubber()
    {
        // "but if he can secure a gammon (reckoning as a double game), he becomes the winner of
        // the rubber."
        Assert.Equal(Player.Black, Winner(Rubber(new(Player.White, GameValue.Hit), new(Player.Black, GameValue.Gammon))));
    }

    [Fact]
    public void A_rubber_scored_from_a_game_valued_by_an_owners_ruling_names_that_ruling()
    {
        // Since ruleset version 6 a game can be a hit or a backgammon by the owner's ruling on game-value's
        // question (docs/decisions/0010), and a rubber scored from it relies on the ruling as much as the game
        // does. White's first game is the finish no named result covers, a hit only by game-value/1: the rubber
        // scores, as the corpus's sentence scores a first hit, and that ruling is named. Before version 6 the
        // game itself declined, so this rubber could not be scored at all.
        var ruledHit = Assert.IsType<Resolution<GameResult>.Resolved>(Outcome.ResultOf(
            Board.Of(Board.Men().RestBorneOff(), Board.Men().At(0, 2).At(18, 1).RestAt(3)), Player.White)).Value;
        Assert.Equal([OwnerRulings.TheUncoveredFinishIsAHit], ruledHit.Rulings.ToArray());

        var lost = Assert.IsType<Resolution<RubberResult>.Resolved>(Rubber(ruledHit, new(Player.White, GameValue.Gammon))).Value;
        Assert.Equal(Player.White, lost.Winner);
        Assert.Equal([OwnerRulings.TheUncoveredFinishIsAHit], lost.Rulings.ToArray());

        var won = Assert.IsType<Resolution<RubberResult>.Resolved>(Rubber(ruledHit, new(Player.Black, GameValue.Gammon))).Value;
        Assert.Equal(Player.Black, won.Winner);
        Assert.Equal([OwnerRulings.TheUncoveredFinishIsAHit], won.Rulings.ToArray());

        // Both games ruled: every ruling once, in OwnerRulings.All's order, whichever game came first.
        var ruledBackgammon = new GameResult(Player.White, GameValue.Backgammon) { Rulings = [OwnerRulings.TheOverlapIsABackgammon] };
        var both = Assert.IsType<Resolution<RubberResult>.Resolved>(Rubber(ruledHit, ruledBackgammon)).Value;
        Assert.Equal([OwnerRulings.TheUncoveredFinishIsAHit, OwnerRulings.TheOverlapIsABackgammon], both.Rulings.ToArray());
        var twice = new GameResult(Player.White, GameValue.Hit) { Rulings = [OwnerRulings.TheUncoveredFinishIsAHit] };
        Assert.Equal([OwnerRulings.TheUncoveredFinishIsAHit],
            Assert.IsType<Resolution<RubberResult>.Resolved>(Rubber(ruledHit, twice)).Value.Rulings.ToArray());

        // Through the entry point, from the results of played games: the same answer and the same ruling.
        var answer = Assert.IsType<RubberResult>(Assert.IsType<Resolution<object>.Resolved>(EntryPoints.RubberScoring.Resolve(
            new HoyleBackgammon.Requests.RubberScoringRequest { Games = [ruledHit, new(Player.Black, GameValue.Gammon)] })).Value);
        Assert.Equal(Player.Black, answer.Winner);
        Assert.Equal([OwnerRulings.TheUncoveredFinishIsAHit], answer.Rulings.ToArray());

        // A rubber the corpus leaves open still declines, ruled games or not, and a decline names no ruling.
        Assert.IsType<Resolution<RubberResult>.Unresolved>(Rubber(ruledHit, new(Player.Black, GameValue.Hit)));
    }

    public static TheoryData<GameResult[]> Unsettled => new()
    {
        // A hit each: the rubber goes on, and nothing says for how long or to what total.
        new GameResult[] { new(Player.White, GameValue.Hit), new(Player.Black, GameValue.Hit) },

        // "does not say how a backgammon reckons".
        new GameResult[] { new(Player.White, GameValue.Hit), new(Player.Black, GameValue.Backgammon) },

        // A first game that was not a hit is not the case the sentence states.
        new GameResult[] { new(Player.White, GameValue.Gammon), new(Player.Black, GameValue.Gammon) },

        // One game, or none: whether the rubber is over turns on its length.
        new GameResult[] { new(Player.White, GameValue.Hit) },
        System.Array.Empty<GameResult>(),

        // A third game: nothing says it belongs to this rubber rather than the next, even after
        // two games the sentence would have settled.
        new GameResult[]
        {
            new(Player.White, GameValue.Hit), new(Player.Black, GameValue.Hit), new(Player.Black, GameValue.Hit),
        },
        new GameResult[]
        {
            new(Player.White, GameValue.Hit), new(Player.White, GameValue.Hit), new(Player.Black, GameValue.Hit),
        },
    };

    [Theory]
    [MemberData(nameof(Unsettled))]
    public void Every_other_rubber_is_the_question_the_corpus_leaves_open(GameResult[] games)
    {
        var unresolved = Assert.IsType<Resolution<RubberResult>.Unresolved>(Rubber(games));

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Result.Reason);
        Assert.Equal(MapEntries.RubberScoring.Locator, unresolved.Result.Locator);
        Assert.Equal("BACKGAMMON / Hints for Play / p. 278", unresolved.Result.Locator.Citation);
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
