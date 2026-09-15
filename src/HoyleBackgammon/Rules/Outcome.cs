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

/// <summary>One game of a rubber: who won it, and what kind of win it was.</summary>
/// <param name="Winner">Who won the game.</param>
/// <param name="Value">Whether it was a hit, a gammon or a backgammon.</param>
public sealed record GameResult(Player Winner, GameValue Value);

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
    /// What kind of win <paramref name="winner"/> has: a hit, a gammon or a backgammon.
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
    /// the case returns <see cref="UnresolvedReason.RequiresInterpretation"/>.
    /// </para>
    /// <para>
    /// The question names a man <em>up</em>, and this declines exactly that. A loser who has
    /// borne off nothing with a man in the winner's home table answers both conditions by the
    /// same words, but the question does not name him, so he is still valued a backgammon;
    /// finding 17 in <c>MAP-FINDINGS.md</c> takes that upstream rather than widening the decline
    /// here.
    /// </para>
    /// <para>
    /// The three are not exhaustive. A loser who has borne off a man, been taken up,
    /// re-entered and run the man clear of the winner's home table satisfies none of them: he
    /// has begun to bear off, so it is not a gammon; his men are not all home, so it is not a
    /// hit; he is neither up nor in the winner's home table, so it is not a backgammon. That
    /// case returns <see cref="UnresolvedReason.RequiresInterpretation"/> too: the entry is <c>clarity: ambiguous</c> with
    /// <c>fate: unresolved</c> and its <c>question</c> names exactly this finish. It used to
    /// be recorded <c>clarity: clear</c> while this line declined — the map saying the case
    /// could not happen while the engine handled it — which is finding 4 in
    /// <c>MAP-FINDINGS.md</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">If <paramref name="winner"/> has not in fact won.</exception>
    public static Resolution<GameValue> ValueOf(Position position, Player winner)
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
        if (position.OnBar(loser) > 0 && position.BorneOff(loser) == 0)
        {
            return Resolution<GameValue>.FromUnresolved(new UnresolvedResult(
                UnresolvedReason.RequiresInterpretation,
                "value a win against a loser who has borne off nothing and has a man up, which "
                + "answers the gammon condition and the backgammon condition both",
                MapEntries.GameValue.Locator));
        }

        if (position.OnBar(loser) > 0 || LoserStandsInWinnersHome(position, loser))
        {
            return Resolution<GameValue>.FromValue(GameValue.Backgammon);
        }

        if (position.BorneOff(loser) == 0)
        {
            return Resolution<GameValue>.FromValue(GameValue.Gammon);
        }

        if (BearingOff.IsEligible(position, loser))
        {
            return Resolution<GameValue>.FromValue(GameValue.Hit);
        }

        return Resolution<GameValue>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.RequiresInterpretation,
            "value a win against a loser who has borne off a man but whose men are no longer "
            + "all home, and who is neither up nor in the winner's home table",
            MapEntries.GameValue.Locator));
    }

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
    /// delegated, and the corpus names the decider, so it is not a gap. It is
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
    /// <param name="agreed">The players' agreement. Demanded, never inferred.</param>
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
    public static Resolution<Player> RubberWinner(IReadOnlyList<GameResult> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        if (games.Count == 2 && games[0].Value == GameValue.Hit)
        {
            var first = games[0].Winner;
            var second = games[1];
            if (second.Winner == first)
            {
                return Resolution<Player>.FromValue(first);
            }

            if (second.Value == GameValue.Gammon)
            {
                return Resolution<Player>.FromValue(second.Winner);
            }
        }

        return Resolution<Player>.FromUnresolved(new UnresolvedResult(
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
