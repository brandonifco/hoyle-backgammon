using System.Collections.Immutable;

namespace HoyleBackgammon;

/// <summary>Which rule authorises a move.</summary>
public enum MoveKind
{
    /// <summary>
    /// A man re-entering from the bar. <see cref="MapEntries.EnterFromBar"/>.
    /// </summary>
    Entry,

    /// <summary>
    /// A man played forward by the number on a die. <see cref="MapEntries.MoveByPip"/>.
    /// </summary>
    Ordinary,

    /// <summary>
    /// A man moved forward within the home table during bearing off.
    /// <see cref="MapEntries.BearingOffMoveOrRemove"/>.
    /// </summary>
    BearingOffMove,

    /// <summary>
    /// A man removed from the point the die names.
    /// <see cref="MapEntries.BearingOffMoveOrRemove"/>.
    /// </summary>
    BearingOffRemove,

    /// <summary>
    /// A man removed from the highest occupied point because the number could not be dealt
    /// with in either other fashion. <see cref="MapEntries.BearingOffHighest"/>.
    /// </summary>
    BearingOffHighest,
}

/// <summary>
/// One man moved by one die.
/// </summary>
/// <param name="From">The pip the man leaves, in the mover's own numbering; 25 is the bar.</param>
/// <param name="To">The pip the man reaches; 0 means borne off.</param>
/// <param name="Die">The number on the die that authorised it.</param>
/// <param name="Kind">Which rule authorised it.</param>
/// <param name="TakesUpBlot">
/// Whether the destination held a lone adversary man, who is taken up onto the bar.
/// <see cref="MapEntries.BlotHit"/>.
/// </param>
public readonly record struct Move(int From, int To, int Die, MoveKind Kind, bool TakesUpBlot)
{
    /// <summary>True when this move bears a man off the board.</summary>
    public bool BearsOff => To == Geometry.BorneOffPip;

    /// <summary>The map entry that authorises this move.</summary>
    public MapEntry Authority => Kind switch
    {
        MoveKind.Entry => MapEntries.EnterFromBar,
        MoveKind.Ordinary => MapEntries.MoveByPip,
        MoveKind.BearingOffHighest => MapEntries.BearingOffHighest,
        _ => MapEntries.BearingOffMoveOrRemove,
    };

    /// <inheritdoc/>
    public override string ToString()
    {
        string from = From == Geometry.BarPip ? "bar" : From.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string to = BearsOff ? "off" : To.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"{from}/{to}({Die}){(TakesUpBlot ? "*" : string.Empty)}";
    }
}

/// <summary>
/// A whole throw played out: the moves in the order they were made, and where they left the
/// board.
/// </summary>
/// <param name="Moves">The moves, in order. Empty when nothing could be played.</param>
/// <param name="Result">The position after all of them.</param>
public sealed record Play(ImmutableArray<Move> Moves, Position Result)
{
    /// <summary>
    /// The owner's rulings this play relies on, in <see cref="OwnerRulings.All"/>'s order; empty when it
    /// relies on none. A ruling answers part of a question the corpus leaves open, so a play that lists
    /// one is offered on its owner's authority and not Hoyle's (<c>docs/decisions/0009</c>, <c>docs/decisions/0010</c>):
    /// <list type="bullet">
    /// <item><see cref="OwnerRulings.TheHigherNumberIsCompelled"/> where either number of the throw alone could be
    /// played but not both, and this play uses the higher;</item>
    /// <item><see cref="OwnerRulings.APlayIsThePositionItReaches"/> where another order of the same
    /// numbers reaches the same position under different rules, and this play, the first order found,
    /// stands for both;</item>
    /// <item><see cref="OwnerRulings.BearingOffStopsUntilEveryManIsHomeAgain"/> where the play is made by a player
    /// whose man, hit after he began to bear off, has re-entered, with a number still to play, so it is made
    /// without bearing off;</item>
    /// <item><see cref="OwnerRulings.BearingOffBeginsWithinTheThrow"/> where a move of this play bears
    /// off, or moves within the home table under bearing off, in a throw that began before every man
    /// was home.</item>
    /// </list>
    /// Each move's <see cref="Move.Authority"/> is still the map entry that permits it once the ruling
    /// is applied.
    /// </summary>
    public ImmutableArray<OwnerRuling> Rulings { get; init; } = [];


    /// <summary>The total number of pips this play consumed from the throw.</summary>
    public int PipsUsed
    {
        get
        {
            int total = 0;
            foreach (var move in Moves)
            {
                total += move.Die;
            }

            return total;
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        Moves.IsEmpty ? "(nothing playable)" : string.Join(" ", Moves);
}
