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
    /// backgammon). Backgammon is tested first, because the three conditions overlap and the
    /// corpus plainly intends the larger name to win.
    /// </para>
    /// <para>
    /// The overlap is smaller than it looks, and worth stating exactly. A man up does
    /// <em>not</em> imply nothing has been borne off: a player may bear off and then be hit,
    /// which is the very manoeuvre the gap case below turns on — a loser holding
    /// <c>{bar 1, off 3}</c> is a backgammon by the corpus's words and has borne off three
    /// men. What does overlap is a loser who has borne off nothing <em>and</em> has a man up
    /// or in the winner's home table: he answers the gammon condition and the backgammon
    /// condition both, and that is the case an ordering had to be chosen for. The map's
    /// <c>note</c> records that the choice was made; its <c>ambiguity.question</c> does not
    /// name it, because the ordering the corpus "plainly intends" is not the case this rule
    /// declines. See finding 4 in <c>MAP-FINDINGS.md</c>.
    /// </para>
    /// <para>
    /// The three are not exhaustive. A loser who has borne off a man, been taken up,
    /// re-entered and run the man clear of the winner's home table satisfies none of them: he
    /// has begun to bear off, so it is not a gammon; his men are not all home, so it is not a
    /// hit; he is neither up nor in the winner's home table, so it is not a backgammon. That
    /// case returns <see cref="UnresolvedReason.RequiresInterpretation"/>, and it is the one
    /// case this rule declines: the entry is <c>clarity: ambiguous</c> with
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
    /// Who throws first in the next game.
    /// <see cref="MapEntries.NextGameOpening"/>: "Where several games are played in
    /// succession, the winner of a 'hit' throws first in the game next following. After a
    /// gammon or backgammon, the players throw again for the right to begin, as at starting."
    /// </summary>
    public static NextOpening Next(GameValue value) =>
        value == GameValue.Hit ? NextOpening.WinnerThrowsFirst : NextOpening.ThrowAgainForTheRight;
}
