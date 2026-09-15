using System.Collections.Immutable;
using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>What a won game is worth, in kind.</summary>
public enum GameValue
{
    /// <summary>
    /// The adversary has got all his men home and has begun to bear off.
    /// </summary>
    Hit,

    /// <summary>
    /// The winner bore off all his men before the adversary began to do the same. "The loser
    /// is said to be 'gammoned', and pays double the agreed stake."
    /// </summary>
    Gammon,

    /// <summary>
    /// The winner bore off all his men while the adversary still had a man up, or in the
    /// winner's home table.
    /// </summary>
    Backgammon,
}

/// <summary>Who throws first in the game after this one.</summary>
public enum NextOpening
{
    /// <summary>"The winner of a 'hit' throws first in the game next following."</summary>
    WinnerThrowsFirst,

    /// <summary>
    /// "After a gammon or backgammon, the players throw again for the right to begin, as at
    /// starting."
    /// </summary>
    ThrowAgainForTheRight,
}

/// <summary>A won game: who won it, what kind of win it was, and the owner's rulings its value relied on.</summary>
/// <param name="Winner">Who won the game.</param>
/// <param name="Value">Whether it was a hit, a gammon or a backgammon.</param>
public sealed record GameResult(Player Winner, GameValue Value)
{
    /// <summary>
    /// The owner's rulings <see cref="Value"/> relies on, in <see cref="OwnerRulings.All"/>'s order; empty when
    /// the corpus names the result unaided. Since ruleset version 6, <see cref="OwnerRulings.TheUncoveredFinishIsAHit"/>
    /// or <see cref="OwnerRulings.TheOverlapIsABackgammon"/> (<c>docs/decisions/0010</c>). A result built by a
    /// caller carries what the caller gives it, and <see cref="Outcome.RubberWinner"/> passes it on.
    /// </summary>
    public ImmutableArray<OwnerRuling> Rulings { get; init; } = [];
}

/// <summary>Who won a rubber, and the owner's rulings that answer relied on.</summary>
/// <param name="Winner">Who won the rubber.</param>
public sealed record RubberResult(Player Winner)
{
    /// <summary>
    /// The owner's rulings the answer relies on: every ruling the values of the games it was scored from
    /// relied on, without repeats, in <see cref="OwnerRulings.All"/>'s order. A rubber scored from a game valued
    /// by a ruling relies on that ruling as much as the game does (<c>docs/decisions/0010</c>).
    /// </summary>
    public ImmutableArray<OwnerRuling> Rulings { get; init; } = [];
}

/// <summary>Winning, and what the win is worth.</summary>
public static class Outcome
{
    /// <summary>
    /// The player who has won, if either has.
    /// <see cref="MapEntries.WinCondition"/>: "The player who first succeeds in removing all
    /// his men from the board wins the game."
    /// </summary>
    public static Player? Winner(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        foreach (var player in Players.Both)
        {
            if (BearingOff.HasWon(position, player))
            {
                return player;
            }
        }

        return null;
    }

    /// <summary>
    /// What kind of win <paramref name="winner"/> has: a hit, a gammon or a backgammon, with the owner's
    /// rulings the answer relies on (<see cref="GameResult.Rulings"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MapEntries.GameValue"/> gives three conditions on the loser's state: he "has
    /// got all his men into his own home table, and has begun to bear off" (a hit); the winner
    /// finished "before his adversary has begun to do the same" (a gammon); the adversary
    /// "has still a man or men 'up' (i.e., on the bar) or in his (the winner's) home table" (a
    /// backgammon).
    /// </para>
    /// <para>
    /// <b>The overlap.</b> A man up does <em>not</em> imply nothing has been borne off: a player
    /// may bear off and then be hit, which is the very manoeuvre the gap case below turns on —
    /// a loser holding <c>{bar 1, off 3}</c> is a backgammon by the corpus's words, and nothing
    /// else. What does overlap is a loser who has borne off nothing <em>and</em> has a man up:
    /// he answers the gammon condition and the backgammon condition both. This engine used to
    /// test backgammon first, on the ground that the corpus plainly intends the larger name to
    /// win, and the map's note recorded that an ordering had been chosen. Since
    /// <c>RulesFactory.Maps.HoyleBackgammon</c> 3.0.0 the map's <c>ambiguity.question</c> names
    /// that overlap (blind-mapping resolution row 52) and the ordering is gone from the note, so
    /// the case returned <see cref="UnresolvedReason.RequiresInterpretation"/> from ruleset version 2 to 5.
    /// </para>
    /// <para>
    /// The backgammon condition has two arms, a man up <em>or</em> a man in the winner's home
    /// table, and a loser who has borne off nothing answers the gammon condition beside either.
    /// Map 3.0.0's question named only the man up, so this declined only him and still valued
    /// the man in the winner's home table a backgammon. Finding 17 in <c>MAP-FINDINGS.md</c> took
    /// that upstream, and since <c>RulesFactory.Maps.HoyleBackgammon</c> 4.0.0 (rules-factory#102)
    /// the question names both, so both declined.
    /// </para>
    /// <para>
    /// <b>Since ruleset version 6 both are the owner's ruling</b>, <see cref="OwnerRulings.TheOverlapIsABackgammon"/>
    /// (<c>docs/decisions/0010</c>): such a loser is backgammoned, and never gammoned as well, and the result names
    /// the ruling. A loser with a man up or in the winner's home table who <em>has</em> borne off is the corpus's own
    /// backgammon and names nothing.
    /// </para>
    /// <para>
    /// The three are not exhaustive. A loser who has borne off a man, been taken up,
    /// re-entered and run the man clear of the winner's home table satisfies none of them: he
    /// has begun to bear off, so it is not a gammon; his men are not all home, so it is not a
    /// hit; he is neither up nor in the winner's home table, so it is not a backgammon. That
    /// case returned <see cref="UnresolvedReason.RequiresInterpretation"/> too until ruleset version 6, and since then
    /// it is a hit by the owner's ruling, <see cref="OwnerRulings.TheUncoveredFinishIsAHit"/>, which the result names
    /// (<c>docs/decisions/0010</c>). The entry is <c>clarity: ambiguous</c> with
    /// <c>fate: unresolved</c> and its <c>question</c> names exactly this finish. It used to
    /// be recorded <c>clarity: clear</c> while this line declined — the map saying the case
    /// could not happen while the engine handled it — which is finding 4 in
    /// <c>MAP-FINDINGS.md</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">If <paramref name="winner"/> has not in fact won.</exception>
    /// <returns>The result. It no longer declines since ruleset version 6, and stays a resolution because the question it answers is open in the map.</returns>
    public static Resolution<GameResult> ResultOf(Position position, Player winner)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!BearingOff.HasWon(position, winner))
        {
            throw new ArgumentException(
                $"{winner} has not borne off all {Position.MenPerPlayer} men and has not won.",
                nameof(winner));
        }

        var loser = winner.Adversary();

        // "still a man or men up (i.e., on the bar) or in his (the winner's) home table". The
        // winner's home table is his own pips 1-6, which the loser counts as 24 down to 19.
        bool backgammon = position.OnBar(loser) > 0 || LoserStandsInWinnersHome(position, loser);
        bool begunToBearOff = position.BorneOff(loser) > 0;

        if (backgammon)
        {
            // Nothing borne off as well is the overlap, a gammon too by the corpus's words: the owner's ruling.
            return Result(winner, GameValue.Backgammon, begunToBearOff ? null : OwnerRulings.TheOverlapIsABackgammon);
        }

        if (!begunToBearOff)
        {
            return Result(winner, GameValue.Gammon, null);
        }

        // All home and begun to bear off is the corpus's hit; begun and not all home again is the finish none of
        // the three names: the owner's ruling.
        return Result(winner, GameValue.Hit, BearingOff.IsEligible(position, loser) ? null : OwnerRulings.TheUncoveredFinishIsAHit);
    }

    private static Resolution<GameResult> Result(Player winner, GameValue value, OwnerRuling? ruling) =>
        Resolution<GameResult>.FromValue(new GameResult(winner, value) { Rulings = ruling is null ? [] : [ruling] });

    private static bool LoserStandsInWinnersHome(Position position, Player loser)
    {
        for (int pip = Geometry.PointCount;
             pip > Geometry.PointCount - Geometry.HomeTableHighestPip;
             pip--)
        {
            if (position.Men(loser, pip) > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What the result pays, as a multiple of the single stake, under the multiple the players
    /// agreed for a backgammon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MapEntries.StakeMultiplier"/> states two figures: a gammon "double the agreed
    /// stake", and a backgammon "either thrice or four times (as may have been agreed) the
    /// amount of the single stake". That a hit pays the single stake is stated nowhere; it is
    /// <see cref="MapEntries.HitPaysSingleStake"/>, derived from those two. Only the last is
    /// delegated, to an agreement, so it is not a gap. It is
    /// <see cref="MapEntries.AgreedBackgammonMultiple"/>, an assertion of its own, and this
    /// rule depends on it — which is why the parameter is required rather than optional and
    /// why there is no overload that does without it. An engine that returned
    /// <see cref="UnresolvedReason.RequiresInterpretation"/> here would be declining a job the
    /// corpus gave it the means to do.
    /// </para>
    /// <para>
    /// The result carries the agreement back out (<see cref="StakeDue.Agreement"/>) for the
    /// same reason a <see cref="GameRecord"/> carries the position it was played from: an
    /// asserted input that vanishes into a number cannot be attributed afterwards. It carries
    /// it for a hit and a gammon too, which the agreement does not change. There used to be a
    /// one-argument overload that declined a backgammon; it was removed with the map's
    /// reclassification — a source-breaking change, recorded in
    /// <c>rules-factory/docs/decisions/0005</c>.
    /// </para>
    /// </remarks>
    /// <param name="value">The kind of win.</param>
    /// <param name="agreed">The agreement, attributed to whoever the caller names (the map's <c>assertedBy</c> is <c>caller</c>). Demanded, never inferred.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="agreed"/> is null.</exception>
    public static StakeDue Pays(GameValue value, AgreedBackgammonMultiple agreed)
    {
        ArgumentNullException.ThrowIfNull(agreed);

        int multiple = value switch
        {
            GameValue.Hit => 1,
            GameValue.Gammon => 2,
            _ => agreed.Multiple,
        };

        return new StakeDue(value, multiple, agreed);
    }

    /// <summary>
    /// Who has won the rubber, from its games in the order they were played.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MapEntries.RubberScoring"/> (<c>RulesFactory.Maps.HoyleBackgammon</c> 3.0.0,
    /// blind-mapping resolution rows 114 and 115): "This case often arises where the player has
    /// already lost the first hit of a rubber, in which case, if he loses the next game, he has
    /// lost the rubber also; but if he can secure a gammon (reckoning as a double game), he
    /// becomes the winner of the rubber."
    /// </para>
    /// <para>
    /// That sentence settles two sequences, and this answers those two and nothing else:
    /// </para>
    /// <list type="bullet">
    /// <item>a hit, then a second game won by the same player, whatever its value: he wins the
    /// rubber;</item>
    /// <item>a hit, then a gammon won by the other player: the other player wins it.</item>
    /// </list>
    /// <para>
    /// The answer names every owner's ruling the two games' values relied on (<see cref="RubberResult.Rulings"/>):
    /// since ruleset version 6 a game can be a hit or a backgammon by a ruling (<see cref="Outcome.ResultOf"/>), and a
    /// rubber scored from it relies on the ruling too. So a rubber whose first game is the uncovered finish, a hit by
    /// <see cref="OwnerRulings.TheUncoveredFinishIsAHit"/>, now scores, and says so (<c>docs/decisions/0010</c>).
    /// </para>
    /// <para>
    /// Every other sequence returns <see cref="UnresolvedReason.RequiresInterpretation"/>. The
    /// map's question is why: the chapter "never defines a rubber's length or winning total,
    /// and does not say how a backgammon reckons". So a first game that was not a hit, a second
    /// game the loser of the first wins by a hit or a backgammon, a rubber of one game, and a
    /// rubber of three or more all decline — the last because nothing says a third game
    /// belongs to the rubber rather than to the next one.
    /// </para>
    /// </remarks>
    /// <param name="games">The rubber's games, first first.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="games"/> is null.</exception>
    public static Resolution<RubberResult> RubberWinner(IReadOnlyList<GameResult> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        if (games.Count == 2 && games[0].Value == GameValue.Hit)
        {
            var first = games[0].Winner;
            var second = games[1];
            var rulings = OwnerRulings.InOrder(games[0].Rulings.Concat(second.Rulings));
            if (second.Winner == first)
            {
                return Resolution<RubberResult>.FromValue(new RubberResult(first) { Rulings = rulings });
            }

            if (second.Value == GameValue.Gammon)
            {
                return Resolution<RubberResult>.FromValue(new RubberResult(second.Winner) { Rulings = rulings });
            }
        }

        return Resolution<RubberResult>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.RequiresInterpretation,
            "score a rubber the corpus's one sentence about rubbers does not settle: its length, "
            + "its winning total and what a backgammon reckons are never stated",
            MapEntries.RubberScoring.Locator));
    }

    /// <summary>
    /// Who throws first in the next game.
    /// <see cref="MapEntries.NextGameOpening"/>: "Where several games are played in
    /// succession, the winner of a 'hit' throws first in the game next following. After a
    /// gammon or backgammon, the players throw again for the right to begin, as at starting."
    /// </summary>
    public static NextOpening Next(GameValue value) =>
        value == GameValue.Hit ? NextOpening.WinnerThrowsFirst : NextOpening.ThrowAgainForTheRight;
}
