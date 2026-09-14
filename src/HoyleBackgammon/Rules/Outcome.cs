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
    /// condition both, and that is the case an ordering had to be chosen for. The map records
    /// this entry <c>clarity: clear</c> and does not record that a choice was made; see
    /// finding 4 in <c>MAP-FINDINGS.md</c>.
    /// </para>
    /// <para>
    /// The three are not exhaustive, which the map does not record. A loser who has borne off
    /// a man, been taken up, re-entered and run the man clear of the winner's home table
    /// satisfies none of them: he has begun to bear off, so it is not a gammon; his men are
    /// not all home, so it is not a hit; he is neither up nor in the winner's home table, so
    /// it is not a backgammon. That case returns
    /// <see cref="UnresolvedReason.RequiresInterpretation"/>. See finding 4 in
    /// <c>MAP-FINDINGS.md</c>: the entry is recorded <c>clarity: clear</c> and is not.
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
    /// What the result pays, as a multiple of the single stake.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.StakeMultiplier"/>: a hit is the single stake, a gammon "double
    /// the agreed stake", and a backgammon "either thrice or four times (as may have been
    /// agreed)". The corpus delegates the last figure to the players, so the engine declines
    /// rather than picking one. Callers who have agreed a figure pass it to
    /// <see cref="Pays(GameValue, int)"/>.
    /// </remarks>
    public static Resolution<int> Pays(GameValue value) => value switch
    {
        GameValue.Hit => Resolution<int>.FromValue(1),
        GameValue.Gammon => Resolution<int>.FromValue(2),
        _ => Resolution<int>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.RequiresInterpretation,
            "settle whether a backgammon pays thrice or four times the single stake",
            MapEntries.StakeMultiplier.Locator)),
    };

    /// <summary>
    /// What the result pays, given the figure the players agreed for a backgammon.
    /// </summary>
    /// <remarks>
    /// The agreement is the players', not the engine's, so it is supplied rather than
    /// inferred. The corpus does bound it: "either thrice or four times", and nothing else.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="agreedForBackgammon"/> is neither three nor four.
    /// </exception>
    public static int Pays(GameValue value, int agreedForBackgammon)
    {
        if (agreedForBackgammon is not (3 or 4))
        {
            throw new ArgumentOutOfRangeException(
                nameof(agreedForBackgammon),
                agreedForBackgammon,
                $"a backgammon pays thrice or four times the single stake and nothing else "
                + $"[{MapEntries.StakeMultiplier.Locator}].");
        }

        return value switch
        {
            GameValue.Hit => 1,
            GameValue.Gammon => 2,
            _ => agreedForBackgammon,
        };
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
