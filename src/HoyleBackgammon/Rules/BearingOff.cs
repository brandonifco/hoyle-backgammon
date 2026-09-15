using System.Collections.Immutable;

namespace HoyleBackgammon;

/// <summary>
/// Whether a player may bear off, and the owner's rulings that answer relies on.
/// </summary>
/// <param name="Eligible">Whether every man of his is in his home table, so that he may bear off.</param>
public sealed record BearingOffEligibility(bool Eligible)
{
    /// <summary>
    /// The owner's rulings the answer relies on; empty when the corpus gives it unaided. Since ruleset
    /// version 6, <see cref="OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain"/> where
    /// <see cref="BearingOff.HasReEnteredMidBearOff"/> holds (<c>docs/decisions/0010</c>).
    /// </summary>
    public ImmutableArray<OwnerRuling> Rulings { get; init; } = [];
}

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
    /// of his stands on the bar or on any point above his six point: literally, all his men are
    /// in his home table.
    /// </para>
    /// <para>
    /// <b>What this does not decide.</b> The map records (since <c>RulesFactory.Maps.HoyleBackgammon</c>
    /// 3.0.0, blind-mapping resolution rows 12 and 13) that the corpus never says whether the
    /// stage <em>lasts</em>: a player hit after he has begun to bear off, whose man re-enters,
    /// may go on bearing off the men still at home, or may have to bring every man home again.
    /// This predicate is the literal test and nothing more. Where the question actually arises
    /// -- <see cref="HasReEnteredMidBearOff"/> -- the owner's ruling since ruleset version 6 is that he may
    /// not bear off again until every man is home, which is this test's answer; <see cref="Eligibility"/>
    /// gives it with the ruling named, and <see cref="LegalPlays.For"/> names it on the plays
    /// (<see cref="OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain"/>, <c>docs/decisions/0010</c>).
    /// Versions 2 to 5 declined there. While his man is still up the question does not arise at all:
    /// <c>enter-from-bar</c> suspends every bearing-off rule (rows 11, 14 and 17), and
    /// <see cref="MovesForDie"/> refuses on that ground first.
    /// </para>
    /// <para>
    /// Nor does it decide the question's second part, since <c>RulesFactory.Maps.HoyleBackgammon</c>
    /// 6.0.0 (rules-factory#125): whether a number left after the move that brings the last man home
    /// is played under this stage. That is a question about a throw, not a position, so this
    /// predicate cannot raise it. The corpus does not settle it; the engine's owner has ruled that it
    /// is, and <see cref="LegalPlays.For"/> applies the ruling and names it on the play
    /// (<see cref="OwnerRulings.BearingOffBeginsWithinTheThrow"/>, <c>docs/decisions/0009</c>).
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
    /// <see cref="MapEntries.BearingOffEligible"/>'s answer: <see cref="IsEligible"/>, naming
    /// <see cref="OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain"/> where
    /// <see cref="HasReEnteredMidBearOff"/> holds, the question's first part, which the corpus does not settle
    /// and the owner has ruled on (<c>docs/decisions/0010</c>).
    /// </summary>
    public static BearingOffEligibility Eligibility(Position position, Player player)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new BearingOffEligibility(IsEligible(position, player))
        {
            Rulings = HasReEnteredMidBearOff(position, player) ? [OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain] : [],
        };
    }

    /// <summary>
    /// Whether <paramref name="player"/> is in the one position the corpus does not settle for
    /// <see cref="MapEntries.BearingOffEligible"/>: he has begun to bear off, a man of his was
    /// hit and has re-entered, and he still has men in his home table to bear off. Where it holds, an
    /// answer relies on <see cref="OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain"/> (<c>docs/decisions/0010</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The map's question (<c>bearing-off-eligible</c>, <c>fate: unresolved</c>; blind-mapping
    /// resolution rows 12 and 13): "If one of his men is hit after he has begun to bear off and
    /// then re-enters, the text does not say whether he may go on bearing off the men still at
    /// home or must first bring every man home again."
    /// </para>
    /// <para>
    /// Written as a predicate on the position, and no wider than the question:
    /// </para>
    /// <list type="bullet">
    /// <item>"has begun to bear off" is at least one man borne off, the reading
    /// <see cref="Outcome.ResultOf"/> already gives the same words in <c>game-value</c>;</item>
    /// <item>"then re-enters" is no man of his on the bar (while one is up,
    /// <c>enter-from-bar</c> suspends bearing off whichever reading holds);</item>
    /// <item>"must first bring every man home again" needs a man outside his home table, or
    /// both readings agree he is bearing off;</item>
    /// <item>"the men still at home" needs a man in his home table, or there is nothing to go
    /// on bearing off and both readings agree he is simply moving.</item>
    /// </list>
    /// </remarks>
    public static bool HasReEnteredMidBearOff(Position position, Player player)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (position.BorneOff(player) == 0 || position.OnBar(player) > 0)
        {
            return false;
        }

        bool home = false, outside = false;
        foreach (int pip in position.OccupiedPoints(player))
        {
            home |= pip <= Geometry.HomeTableHighestPip;
            outside |= pip > Geometry.HomeTableHighestPip;
        }

        return home && outside;
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

        // enter-from-bar suspends this rule, bearing-off-highest and bearing-off-doublets: "Until
        // he does this, the play of his other men is suspended", bearing off included
        // (RulesFactory.Maps.HoyleBackgammon 3.0.0; blind-mapping resolution rows 11, 14, 17).
        if (Movement.MustEnterFromBar(position, player))
        {
            throw new InvalidOperationException(
                $"{player} has a man up; {MapEntries.EnterFromBar.Id} suspends bearing off until "
                + $"he enters [{MapEntries.EnterFromBar.Locator}].");
        }

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
