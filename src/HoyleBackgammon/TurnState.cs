using Tabletop.Dice;

namespace HoyleBackgammon;

/// <summary>Where in a turn the game stands.</summary>
public enum TurnPhase
{
    /// <summary>
    /// The throw for the right to begin has not been made: a chance event
    /// (<see cref="MapEntries.OpeningRoll"/>). Nobody is to move yet.
    /// </summary>
    OpeningRoll,

    /// <summary>
    /// A player's turn begins and nothing has been thrown. What comes next depends on the position: he may be
    /// wholly suspended (<see cref="MapEntries.FullTableSuspension"/>), he may have the option on the opening
    /// throw (<see cref="MapEntries.OpeningThrowerOption"/>), or he throws.
    /// </summary>
    Turn,

    /// <summary>The player to move holds a throw (<see cref="TurnState.PendingThrow"/>) and has not played it.</summary>
    Play,

    /// <summary>A man of one side has borne off the last of his men (<see cref="MapEntries.WinCondition"/>).</summary>
    Over,
}

/// <summary>
/// The opener's option on his first throw, outstanding until he actually throws.
/// <see cref="MapEntries.OpeningThrowerOption"/>: "The thrower of the higher number may either adopt the points
/// shown by the two dice as his own throw, or throw again."
/// </summary>
/// <param name="Opener">The player who threw the higher number. The option is his and nobody else's.</param>
/// <param name="Deciding">
/// The deciding pair, White's die first and Black's second (<see cref="OpeningRoll.Deciding"/>): the two dice whose
/// points the opener may adopt. Never doublets.
/// </param>
public readonly record struct OutstandingOption(Player Opener, DiceThrow Deciding);

/// <summary>
/// Everything <see cref="Game.Play"/>'s loop needs to continue from between two decisions, as an immutable value
/// with value equality: the position, the side to move, the opening option if it is still outstanding, the throw
/// made and not yet played, and whether the game is over.
/// </summary>
/// <remarks>
/// <para>
/// A state is made by <see cref="TurnLoop.Begin"/> and moved only by the operations on the decisions
/// <see cref="TurnLoop.Next"/> returns. It has no public constructor, so every state a caller holds was reached by
/// the loop's own transitions and satisfies its invariants (a <see cref="TurnPhase.Play"/> state has a pending
/// throw, an <see cref="TurnPhase.Over"/> state has a winner, and so on). No operation mutates the state it is
/// given.
/// </para>
/// <para>
/// Two states are equal when the game stands in the same place: same phase, position, side to move, outstanding
/// option, pending throw and winner. The pending throw is held with its higher die first, because the order two
/// dice were thrown in is not game state (<see cref="Movement.Entitlement"/> ignores it); the order drawn is
/// reported by <see cref="SampledChance.Thrown"/> for a record to keep. The opening roll's tied attempts are not
/// state either (<see cref="OpeningRoll.Attempts"/>), only the pair that decided it.
/// </para>
/// </remarks>
public sealed class TurnState : IEquatable<TurnState>
{
    internal TurnState(
        TurnPhase phase,
        Position position,
        Player? toMove,
        OutstandingOption? option,
        DiceThrow? pending,
        Player? winner)
    {
        Phase = phase;
        Position = position;
        ToMove = toMove;
        Option = option;
        PendingThrow = pending;
        Winner = winner;
    }

    /// <summary>Where in a turn the game stands.</summary>
    public TurnPhase Phase { get; }

    /// <summary>The position. <see cref="Position"/> does not record the side to move; <see cref="ToMove"/> does.</summary>
    public Position Position { get; }

    /// <summary>
    /// The player whose turn it is, or null before the throw for the right to begin and once the game is over.
    /// </summary>
    public Player? ToMove { get; }

    /// <summary>
    /// The opener's option on his first throw while it is outstanding, else null. It stays outstanding across a turn
    /// the opener forfeits to suspension, and is spent when he is asked, whichever way he answers.
    /// </summary>
    public OutstandingOption? Option { get; }

    /// <summary>
    /// The throw made and not yet played, higher die first, in <see cref="TurnPhase.Play"/>; else null.
    /// </summary>
    public DiceThrow? PendingThrow { get; }

    /// <summary>The winner, in <see cref="TurnPhase.Over"/>; else null.</summary>
    public Player? Winner { get; }

    /// <summary>Whether the game is over.</summary>
    public bool IsOver => Phase == TurnPhase.Over;

    /// <inheritdoc/>
    public bool Equals(TurnState? other) =>
        other is not null
        && Phase == other.Phase
        && ToMove == other.ToMove
        && Option == other.Option
        && PendingThrow == other.PendingThrow
        && Winner == other.Winner
        && Position.Equals(other.Position);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TurnState);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Phase, ToMove, Option, PendingThrow, Winner, Position);

    /// <inheritdoc/>
    public override string ToString()
    {
        string option = Option is { } o ? $", {o.Opener} may adopt {o.Deciding}" : string.Empty;
        string pending = PendingThrow is { } t ? $", holding {t}" : string.Empty;
        string toMove = ToMove is { } p ? $", {p} to move" : string.Empty;
        string winner = Winner is { } w ? $", {w} has won" : string.Empty;
        return $"{Phase}{toMove}{pending}{option}{winner}: {Position}";
    }
}
