using System.Collections.Immutable;

namespace HoyleBackgammon;

/// <summary>
/// Bearing off: the last stage, where a throw may move a man within the home table or take
/// one off the board.
/// </summary>
public static class BearingOff
{
    /// <summary>
    /// Whether <paramref name="player"/> has got all his men into his home table and may
    /// begin to bear off.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.BearingOffEligible"/>: "When either player has succeeded in
    /// getting all his men into his home table, he proceeds to 'bear them off'."
    /// <para>
    /// Men already borne off are off the board, not outstanding, so the test is that nothing
    /// of his stands on the bar or on any point above his six point. A man taken up mid-bear-off
    /// puts him back on the bar and this goes false again, which is why no bearing-off rule
    /// needs <c>enter-from-bar</c> as a separate gate.
    /// </para>
    /// </remarks>
    public static bool IsEligible(Position position, Player player)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (position.OnBar(player) > 0)
        {
            return false;
        }

        for (int pip = Geometry.HomeTableHighestPip + 1; pip <= Geometry.PointCount; pip++)
        {
            if (position.Men(player, pip) > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Every move a single die of <paramref name="die"/> allows a player who is bearing off,
    /// highest origin first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MapEntries.BearingOffMoveOrRemove"/>: "each throw entitles the player either
    /// to move forward a man or men (to the extent indicated by the throw) within the limits
    /// of his own table, or to remove men from the corresponding points." A forward move is
    /// still subject to <see cref="MapEntries.LegalDestination"/> — the bearing-off section
    /// does not repeat the qualification and does not withdraw it, and an adversary man can be
    /// standing in the winner's home table, which <c>game-value</c>'s definition of a
    /// backgammon says in so many words.
    /// </para>
    /// <para>
    /// <see cref="MapEntries.BearingOffHighest"/>: "If, however, he throws a number which he
    /// cannot deal with after either of these fashions — e.g., a six, he is entitled to bear
    /// off a man from his highest occupied point." Read literally: the highest-point removal
    /// becomes available exactly when the two other fashions yield nothing.
    /// </para>
    /// <para>
    /// <see cref="MapEntries.BearingOffDoublets"/> needs nothing here: "Doublets have, as in
    /// the earlier stage of the game, a twofold value, and may be played either wholly by
    /// moving men forward, wholly by bearing off, or partly by the one method and partly by
    /// the other." That is four independent applications of this method, which is what
    /// <see cref="Movement.Entitlement"/> already produces.
    /// </para>
    /// </remarks>
    public static ImmutableArray<Move> MovesForDie(Position position, Player player, int die)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentOutOfRangeException.ThrowIfLessThan(die, 1);

        if (!IsEligible(position, player))
        {
            throw new InvalidOperationException(
                $"{player} is not yet bearing off; {MapEntries.BearingOffEligible.Id} gates this "
                + $"rule [{MapEntries.BearingOffEligible.Locator}].");
        }

        var moves = ImmutableArray.CreateBuilder<Move>();
        for (int from = Geometry.HomeTableHighestPip; from >= 1; from--)
        {
            if (position.Men(player, from) == 0)
            {
                continue;
            }

            if (from == die)
            {
                moves.Add(Movement.Take(
                    position, player, from, Geometry.BorneOffPip, die, MoveKind.BearingOffRemove));
            }
            else if (from > die && Movement.IsPermittedDestination(position, player, from - die))
            {
                moves.Add(Movement.Take(
                    position, player, from, from - die, die, MoveKind.BearingOffMove));
            }
        }

        if (moves.Count > 0)
        {
            return moves.ToImmutable();
        }

        foreach (int highest in position.OccupiedPoints(player))
        {
            return
            [
                Movement.Take(
                    position, player, highest, Geometry.BorneOffPip, die, MoveKind.BearingOffHighest),
            ];
        }

        // No man on any point: every man is already off, so the game is over and this throw
        // never happens. Reached only if a caller asks about a finished position.
        return [];
    }

    /// <summary>
    /// Whether <paramref name="player"/> has removed all his men and so has won.
    /// <see cref="MapEntries.WinCondition"/>: "The player who first succeeds in removing all
    /// his men from the board wins the game."
    /// </summary>
    public static bool HasWon(Position position, Player player)
    {
        ArgumentNullException.ThrowIfNull(position);
        return position.BorneOff(player) == Position.MenPerPlayer;
    }
}
