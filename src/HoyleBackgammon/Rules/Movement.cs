using System.Collections.Immutable;
using Tabletop.Dice;

namespace HoyleBackgammon;

/// <summary>
/// Moving men: what a die entitles, where a man may be played, and the bar.
/// </summary>
public static class Movement
{
    /// <summary>
    /// What the throw entitles the player to move, as a list of numbers.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.MoveByPip"/>: "The number uppermost on each die entitles the
    /// player to move one man forward a corresponding number of points."
    /// <see cref="MapEntries.Doublets"/>: "In the event of his throwing the same points with
    /// both dice (known as 'doublets'), he is entitled to play the throw twice over" — two
    /// aces move an aggregate of four points, double deuces eight, double threes twelve, so
    /// the throw yields four numbers, not two.
    /// <para>
    /// Ordered higher first, which is the order Hoyle calls a throw in ("six deuce", "cinque
    /// trois", "quatre ace"). For doublets the order is immaterial; for the rest it fixes the
    /// order legal plays are enumerated in, which replay depends on.
    /// </para>
    /// </remarks>
    public static ImmutableArray<int> Entitlement(DiceThrow thrown) =>
        thrown.IsDoublets
            ? [thrown.First, thrown.First, thrown.First, thrown.First]
            : [thrown.Higher, thrown.Lower];

    /// <summary>
    /// Whether <paramref name="player"/> may play a man to his pip <paramref name="pip"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.LegalDestination"/>: "a man can only be played to a point which
    /// is either vacant or occupied by one or more men of the player, or by one man only of
    /// the adversary." Two or more adversary men — a point the adversary has made,
    /// <see cref="MapEntries.MadePoint"/> — refuses the move.
    /// <para>
    /// This governs entry from the bar as well, which is a <em>decision</em> and not a reading
    /// the corpus forces. Three sentences later <see cref="MapEntries.EnterFromBar"/> gives an
    /// entering man only "a vacant point or blot", dropping this rule's second arm — a point
    /// the player's own men hold. The corpus says it twice, differently, and
    /// <c>rules-factory/docs/decisions/0006</c> rules that the general qualification governs
    /// and the entry sentence is shorthand for it. Both entries are <c>clarity: ambiguous</c>
    /// with <c>fate: decision</c> naming that record.
    /// </para>
    /// </remarks>
    public static bool IsPermittedDestination(Position position, Player player, int pip)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Geometry.IsPoint(pip) && position.AdversaryMen(player, pip) <= 1;
    }

    /// <summary>
    /// Whether <paramref name="player"/> has a man on the bar, which suspends every other man.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.EnterFromBar"/>: a man taken up "has to begin its journey anew
    /// from the inner table of the adversary. Nor can such man again start on its journey
    /// until its owner is fortunate enough to make a throw corresponding with a vacant point
    /// or blot in such table. Until he does this, the play of his other men is suspended."
    /// This is the <c>gatedBy</c> relation the map records on <c>move-by-pip</c>,
    /// <c>doublets</c> and <c>must-play-whole-throw</c>.
    /// </remarks>
    public static bool MustEnterFromBar(Position position, Player player)
    {
        ArgumentNullException.ThrowIfNull(position);
        return position.OnBar(player) > 0;
    }

    /// <summary>
    /// Whether <paramref name="player"/>'s play is wholly suspended: he has a man up and the
    /// adversary's home table is full, so he does not throw at all.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.FullTableSuspension"/>: "If the adverse player's home table is
    /// completely full — i.e., each point occupied by two or more men, his play is altogether
    /// suspended, the adversary continuing to throw and move until the course of play again
    /// throws open one or more points in his table."
    /// <para>
    /// "Altogether suspended" is stronger than "has no legal play", and the difference is
    /// observable: a suspended player does not throw, so the dice are not consumed and the
    /// next throw from a seeded generator belongs to his adversary. Treating suspension as an
    /// empty turn would change every subsequent throw in the game.
    /// </para>
    /// <para>
    /// <b>"Full" means full of the <em>adversary's</em> men</b>, which the corpus's "each
    /// point occupied by two or more men" does not say — read literally, two of the entering
    /// player's own men would shut the table against him. That is a decision, not the only
    /// reading: <c>rules-factory/docs/decisions/0006</c>, the same record that rules the
    /// general destination rule governs entry, and necessarily so, since a point a man may
    /// enter on cannot be one that shuts him out. Hence
    /// <see cref="Position.HasMadePoint"/> against the <em>adversary</em> below, and not a
    /// count of whoever's men stand there.
    /// </para>
    /// </remarks>
    public static bool IsWhollySuspended(Position position, Player player)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!MustEnterFromBar(position, player))
        {
            return false;
        }

        var adversary = player.Adversary();
        for (int pip = 1; pip <= Geometry.HomeTableHighestPip; pip++)
        {
            if (!position.HasMadePoint(adversary, pip))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Every move <paramref name="player"/> may make with a single die of
    /// <paramref name="die"/>, in a fixed order: highest origin first.
    /// </summary>
    /// <remarks>
    /// Dispatches between the three regimes the corpus describes — entry from the bar, which
    /// suspends the rest (<see cref="MapEntries.EnterFromBar"/>); ordinary play
    /// (<see cref="MapEntries.MoveByPip"/>); and bearing off, once every man is home
    /// (<see cref="MapEntries.BearingOffEligible"/>).
    /// </remarks>
    public static ImmutableArray<Move> MovesForDie(Position position, Player player, int die)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentOutOfRangeException.ThrowIfLessThan(die, 1);

        if (MustEnterFromBar(position, player))
        {
            int entry = Geometry.BarPip - die;
            return IsPermittedDestination(position, player, entry)
                ? [Take(position, player, Geometry.BarPip, entry, die, MoveKind.Entry)]
                : [];
        }

        if (BearingOff.IsEligible(position, player))
        {
            return BearingOff.MovesForDie(position, player, die);
        }

        var moves = ImmutableArray.CreateBuilder<Move>();
        foreach (int from in position.OccupiedPoints(player))
        {
            int to = from - die;

            // A man may only be played off the board while bearing off, and this branch is
            // only reached when the player is not yet eligible, so a move that would run past
            // the ace point is simply not available.
            if (to >= 1 && IsPermittedDestination(position, player, to))
            {
                moves.Add(Take(position, player, from, to, die, MoveKind.Ordinary));
            }
        }

        return moves.ToImmutable();
    }

    internal static Move Take(
        Position position, Player player, int from, int to, int die, MoveKind kind)
    {
        bool takesUp = Geometry.IsPoint(to) && position.AdversaryMen(player, to) == 1;
        return new Move(from, to, die, kind, takesUp);
    }
}
