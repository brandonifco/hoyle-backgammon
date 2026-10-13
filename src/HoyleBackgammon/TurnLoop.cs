using System.Collections.Immutable;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon;

/// <summary>
/// The turn loop as an immutable step surface: from any <see cref="TurnState"/> say what is decided next
/// (<see cref="Next"/>), and move to the next state by applying one of the answers the decision offers.
/// <see cref="Game.Play"/> is a client of exactly this, so the two cannot disagree about what a game is.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is for.</b> A caller that must branch a game at a decision, or list the outcomes of a chance event
/// instead of sampling one, cannot do so through <see cref="IDecider"/> and <see cref="IRandomSource"/>. This
/// surface exposes the loop's own state between decisions, so a caller never re-implements the loop (and with it the
/// rules) to do so. Nothing here chooses: no policy, no evaluation, no strategy.
/// </para>
/// <para>
/// <b>Purity.</b> No operation mutates its inputs or any shared state. <see cref="Next"/> and the operations on the
/// decisions it returns allocate what they return and nothing else; the one memo, <see cref="LegalPlays.For"/>'s, is
/// per call. Each step costs what <see cref="Game.Play"/> paid for the same turn: no replay from the start.
/// </para>
/// <para>
/// <b>No turn limit.</b> The loop has no bound on a game's length; <see cref="Game.Play"/>'s runaway guard is its
/// own and a caller of this surface bounds its own playouts.
/// </para>
/// <para>
/// <b>Chance is data.</b> The outcomes of a throw are the 21 unordered pairs of two six-faced dice
/// (<see cref="MapEntries.DieFaces"/>); the opening roll's are the 30 ordered pairs that do not tie, which are the
/// tie-free renormalisation of the 36 (<see cref="MapEntries.OpeningRoll"/>: "In the event of a tie, the players
/// throw again"). Each outcome's probability is a count over a stated total on the assumption that every face of a
/// die is as likely as any other and the dice are independent, which is what a sampling variant draws when its
/// <see cref="IRandomSource"/> is uniform. The corpus never says the dice are fair
/// (<see cref="Opening.Throw"/>), so this is the engine's reading of the generator, not a rule of the game.
/// </para>
/// </remarks>
public static class TurnLoop
{
    private static readonly int Faces = DicePair.OfSixes.First.Faces;

    // full-table-suspension: "No entry covers the combination" of both players wholly suspended, so the loop declines it.
    private static readonly UnresolvedResult BothSuspended = new(
        UnresolvedReason.UnsupportedInteraction,
        "continue a game in which both players are wholly suspended against "
        + "each other's full home tables",
        MapEntries.FullTableSuspension.Locator);

    private static readonly ImmutableArray<ChanceOutcome> ThrowOutcomes = BuildThrowOutcomes();

    private static readonly ImmutableArray<ChanceOutcome> OpeningOutcomes = BuildOpeningOutcomes();

    /// <summary>
    /// The state a game begins in, from a position somebody asserted.
    /// </summary>
    /// <remarks>
    /// As <see cref="Game.Play"/>: the position is demanded rather than defaulted, and the start is never itself
    /// examined for a winner, a turn is played first. With no <paramref name="opener"/> the first decision is the
    /// chance event of the throw for the right to begin; with one it is that player's turn and no roll was made,
    /// so he has no option on his throw (<see cref="MapEntries.NextGameOpening"/>).
    /// </remarks>
    /// <param name="start">The asserted starting position.</param>
    /// <param name="opener">Who begins, when the previous game settled it; null throws for the right as at starting.</param>
    /// <returns>A <see cref="TurnPhase.OpeningRoll"/> state, or a <see cref="TurnPhase.Turn"/> state for the opener.</returns>
    public static TurnState Begin(AssertedPosition start, Player? opener = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        return opener is { } first
            ? new TurnState(TurnPhase.Turn, start.Position, first, null, null, null)
            : new TurnState(TurnPhase.OpeningRoll, start.Position, null, null, null, null);
    }

    /// <summary>
    /// Whose decision, or which chance event, comes next from <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns>
    /// One of <see cref="PlayDecision"/>, <see cref="AdoptionDecision"/>, <see cref="ChanceDecision"/>,
    /// <see cref="PassDecision"/>, <see cref="TerminalDecision"/> and <see cref="UnresolvedDecision"/>.
    /// </returns>
    public static Decision Next(TurnState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (state.Phase)
        {
            case TurnPhase.OpeningRoll:
                return new ChanceDecision(state, ChanceKind.OpeningRoll, OpeningOutcomes, Faces * (Faces - 1));

            case TurnPhase.Over:
                return new TerminalDecision(state, Outcome.ResultOf(state.Position, state.Winner!.Value));

            case TurnPhase.Play:
                return PlaysFor(state);

            default:
                return TurnStart(state);
        }
    }

    private static Decision TurnStart(TurnState state)
    {
        var toMove = state.ToMove!.Value;

        // full-table-suspension: a suspended player does not throw. If both are suspended at once neither can ever
        // move, and no entry covers the combination. Reachable only from an asserted start (Game.Play's remarks).
        if (Movement.IsWhollySuspended(state.Position, toMove))
        {
            return Movement.IsWhollySuspended(state.Position, toMove.Adversary())
                ? new UnresolvedDecision(BothSuspended)
                : new PassDecision(state);
        }

        // The option belongs to the opener's first throw, and is not taken until he actually throws.
        if (state.Option is { } option && option.Opener == toMove)
        {
            return new AdoptionDecision(state, option);
        }

        return new ChanceDecision(state, ChanceKind.Throw, ThrowOutcomes, Faces * Faces);
    }

    private static Decision PlaysFor(TurnState state)
    {
        var legal = LegalPlays.For(state.Position, state.ToMove!.Value, Movement.Entitlement(state.PendingThrow!.Value));
        return legal switch
        {
            Resolution<ImmutableArray<Play>>.Unresolved unresolved => new UnresolvedDecision(unresolved.Result),
            Resolution<ImmutableArray<Play>>.Resolved resolved => new PlayDecision(state, resolved.Value),
            _ => throw new InvalidOperationException("a resolution is resolved or unresolved."),
        };
    }

    private static ImmutableArray<ChanceOutcome> BuildThrowOutcomes()
    {
        // Higher die first, highest first: the order Hoyle calls a throw in. A pair of unlike faces can fall two ways.
        var outcomes = ImmutableArray.CreateBuilder<ChanceOutcome>();
        for (int higher = Faces; higher >= 1; higher--)
        {
            for (int lower = higher; lower >= 1; lower--)
            {
                outcomes.Add(new ChanceOutcome(new DiceThrow(higher, lower), higher == lower ? 1 : 2));
            }
        }

        return outcomes.ToImmutable();
    }

    private static ImmutableArray<ChanceOutcome> BuildOpeningOutcomes()
    {
        // White's die and Black's, in the order the engine throws them; who begins is part of the outcome.
        var outcomes = ImmutableArray.CreateBuilder<ChanceOutcome>();
        for (int white = 1; white <= Faces; white++)
        {
            for (int black = 1; black <= Faces; black++)
            {
                if (white != black)
                {
                    outcomes.Add(new ChanceOutcome(new DiceThrow(white, black), 1));
                }
            }
        }

        return outcomes.ToImmutable();
    }

    internal static TurnState Pass(TurnState state) =>
        new(TurnPhase.Turn, state.Position, state.ToMove!.Value.Adversary(), state.Option, null, null);

    internal static TurnState Adopt(TurnState state, OutstandingOption option, bool adopt) =>
        adopt
            ? new TurnState(TurnPhase.Play, state.Position, state.ToMove, null, Canonical(option.Deciding), null)
            : new TurnState(TurnPhase.Turn, state.Position, state.ToMove, null, null, null);

    internal static TurnState Played(TurnState state, Play play)
    {
        // The terminal check Game.Play made after every play: a winner ends the game, else the adversary is to move.
        var position = play.Result;
        return Outcome.Winner(position) is { } winner
            ? new TurnState(TurnPhase.Over, position, null, null, null, winner)
            : new TurnState(TurnPhase.Turn, position, state.ToMove!.Value.Adversary(), state.Option, null, null);
    }

    internal static TurnState Thrown(TurnState state, DiceThrow thrown) =>
        new(TurnPhase.Play, state.Position, state.ToMove, state.Option, Canonical(thrown), null);

    internal static TurnState Rolled(TurnState state, DiceThrow deciding)
    {
        var opener = Opening.OpenerOf(deciding);
        return new TurnState(TurnPhase.Turn, state.Position, opener, new OutstandingOption(opener, deciding), null, null);
    }

    internal static DiceThrow Canonical(DiceThrow thrown) => new(thrown.Higher, thrown.Lower);

    internal static void CheckFaces(DiceThrow outcome)
    {
        if (outcome.First < 1 || outcome.Second < 1 || outcome.First > Faces || outcome.Second > Faces)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome, $"a die of {Faces} faces shows a face from 1 to {Faces}.");
        }
    }
}

/// <summary>What kind of chance event a <see cref="ChanceDecision"/> is.</summary>
public enum ChanceKind
{
    /// <summary>The throw for the right to begin: White's die and Black's, ties thrown again (<see cref="MapEntries.OpeningRoll"/>).</summary>
    OpeningRoll,

    /// <summary>A throw of both dice (<see cref="MapEntries.ThrowTwoDice"/>).</summary>
    Throw,
}

/// <summary>One outcome of a chance event, and how likely it is.</summary>
/// <param name="Throw">
/// The outcome. For <see cref="ChanceKind.Throw"/> the pair with its higher die first (the pair's other order is the
/// same outcome); for <see cref="ChanceKind.OpeningRoll"/> White's die then Black's, which differ.
/// </param>
/// <param name="Ways">
/// How many of the <see cref="ChanceDecision.Total"/> equally likely ordered pairs of faces give this outcome: its
/// probability is <c>Ways / Total</c>, exactly.
/// </param>
public readonly record struct ChanceOutcome(DiceThrow Throw, int Ways);

/// <summary>What <see cref="ChanceDecision.Sample"/> drew, and the state it reached.</summary>
/// <param name="State">The state after the draw. Equal to applying <see cref="Thrown"/> to the same chance event.</param>
/// <param name="Thrown">
/// The pair drawn, in the order drawn: what <see cref="Turn.Thrown"/> records. For an opening roll, the deciding pair.
/// </param>
/// <param name="Roll">
/// For an opening roll, every attempt including the ties thrown again, as <see cref="Opening.RollForTheRight"/>
/// returns it and <see cref="GameRecord.OpeningRoll"/> records it; else null.
/// </param>
public readonly record struct SampledChance(TurnState State, DiceThrow Thrown, OpeningRoll? Roll);

/// <summary>
/// What is decided next from a <see cref="TurnState"/>. Closed: the six kinds below are all there are. Each holds the
/// state it was read from, and each operation on it returns a new state and leaves that one as it was.
/// </summary>
public abstract class Decision
{
    private protected Decision()
    {
    }
}

/// <summary>A player chooses a play for the throw he holds.</summary>
public sealed class PlayDecision : Decision
{
    internal PlayDecision(TurnState state, ImmutableArray<Play> plays)
    {
        State = state;
        Plays = plays;
    }

    /// <summary>The state this decision was read from.</summary>
    public TurnState State { get; }

    /// <summary>The player choosing.</summary>
    public Player Player => State.ToMove!.Value;

    /// <summary>
    /// The legal plays, in the fixed order <see cref="LegalPlays.For"/> gives them, never empty: a throw with nothing
    /// playable offers exactly one play, which moves no man.
    /// </summary>
    public ImmutableArray<Play> Plays { get; }

    /// <summary>Plays <see cref="Plays"/>[<paramref name="index"/>].</summary>
    /// <param name="index">An index into <see cref="Plays"/>.</param>
    /// <returns>The next state.</returns>
    public TurnState Apply(int index)
    {
        if (index < 0 || index >= Plays.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index), index, $"{Plays.Length} legal plays are offered.");
        }

        return TurnLoop.Played(State, Plays[index]);
    }

    /// <summary>Plays one of <see cref="Plays"/>, found by value.</summary>
    /// <param name="play">A play equal to one this decision offers (moves, resulting position and rulings).</param>
    /// <returns>The next state.</returns>
    /// <exception cref="ArgumentException">If <paramref name="play"/> is not one of the legal plays offered.</exception>
    public TurnState Apply(Play play)
    {
        ArgumentNullException.ThrowIfNull(play);
        foreach (var offered in Plays)
        {
            if (ReferenceEquals(offered, play) || SamePlay(offered, play))
            {
                return TurnLoop.Played(State, offered);
            }
        }

        throw new ArgumentException($"{play} is not one of the {Plays.Length} legal plays offered.", nameof(play));
    }

    private static bool SamePlay(Play a, Play b) =>
        a.Result.Equals(b.Result)
        && a.Moves.AsSpan().SequenceEqual(b.Moves.AsSpan())
        && a.Rulings.AsSpan().SequenceEqual(b.Rulings.AsSpan());
}

/// <summary>The opener chooses whether to adopt the opening throw (<see cref="MapEntries.OpeningThrowerOption"/>).</summary>
public sealed class AdoptionDecision : Decision
{
    internal AdoptionDecision(TurnState state, OutstandingOption option)
    {
        State = state;
        Option = option;
    }

    /// <summary>The state this decision was read from.</summary>
    public TurnState State { get; }

    /// <summary>The option, whose <see cref="OutstandingOption.Opener"/> is the player choosing.</summary>
    public OutstandingOption Option { get; }

    /// <summary>
    /// Answers the option. The option is spent either way. Adopting holds the deciding pair as the throw to play;
    /// declining leaves the player to throw both dice again, a chance event.
    /// </summary>
    /// <param name="adopt">Whether the opener adopts the deciding pair.</param>
    /// <returns>The next state.</returns>
    public TurnState Apply(bool adopt) => TurnLoop.Adopt(State, Option, adopt);
}

/// <summary>A chance event: the outcomes it can have, their exact probabilities, and how to move to one.</summary>
public sealed class ChanceDecision : Decision
{
    internal ChanceDecision(TurnState state, ChanceKind kind, ImmutableArray<ChanceOutcome> outcomes, int total)
    {
        State = state;
        Kind = kind;
        Outcomes = outcomes;
        Total = total;
    }

    /// <summary>The state this decision was read from.</summary>
    public TurnState State { get; }

    /// <summary>Which chance event.</summary>
    public ChanceKind Kind { get; }

    /// <summary>
    /// Every outcome in a fixed order: for a throw the 21 unordered pairs, doublets 1 way each and the rest 2; for the
    /// opening roll the 30 ordered pairs that do not tie, 1 way each. The ways sum to <see cref="Total"/>.
    /// </summary>
    public ImmutableArray<ChanceOutcome> Outcomes { get; }

    /// <summary>
    /// The stated total each outcome's <see cref="ChanceOutcome.Ways"/> is counted over: 36, the ordered pairs of two
    /// six-faced dice, for a throw; 30, those that do not tie, for the opening roll.
    /// </summary>
    public int Total { get; }

    /// <summary>Moves to a chosen outcome with no generator: the throw as if it had been drawn.</summary>
    /// <param name="outcome">
    /// A throw: any pair of faces, in either order, for <see cref="ChanceKind.Throw"/>; White's die then Black's, not
    /// tied, for <see cref="ChanceKind.OpeningRoll"/>.
    /// </param>
    /// <returns>The state sampling this outcome would have reached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If a face is not one of the die's.</exception>
    /// <exception cref="ArgumentException">If the opening roll is tied, which is thrown again and never an outcome.</exception>
    public TurnState Apply(DiceThrow outcome)
    {
        TurnLoop.CheckFaces(outcome);
        if (Kind == ChanceKind.OpeningRoll)
        {
            if (outcome.IsDoublets)
            {
                throw new ArgumentException($"{outcome} is a tie, which the players throw again.", nameof(outcome));
            }

            return TurnLoop.Rolled(State, outcome);
        }

        return TurnLoop.Thrown(State, outcome);
    }

    /// <summary>
    /// Draws an outcome exactly as <see cref="Game.Play"/> does: <see cref="Opening.RollForTheRight"/> for the opening
    /// roll (two draws a pair, ties thrown again), <see cref="Opening.Throw"/> for a throw (two draws).
    /// </summary>
    /// <param name="source">The generator.</param>
    /// <returns>The state reached and what was drawn.</returns>
    public SampledChance Sample(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Kind == ChanceKind.OpeningRoll)
        {
            var roll = Opening.RollForTheRight(source);
            return new SampledChance(TurnLoop.Rolled(State, roll.Deciding), roll.Deciding, roll);
        }

        var thrown = Opening.Throw(source);
        return new SampledChance(TurnLoop.Thrown(State, thrown), thrown, null);
    }
}

/// <summary>
/// A player wholly suspended by the adversary's full home table does not throw and his turn passes
/// (<see cref="MapEntries.FullTableSuspension"/>). Forced, so there is nothing to choose, but the loop records the turn.
/// </summary>
public sealed class PassDecision : Decision
{
    internal PassDecision(TurnState state) => State = state;

    /// <summary>The state this decision was read from.</summary>
    public TurnState State { get; }

    /// <summary>The player whose turn passes.</summary>
    public Player Player => State.ToMove!.Value;

    /// <summary>Passes the turn: the adversary is to move on the same position, and any outstanding option stays so.</summary>
    /// <returns>The next state.</returns>
    public TurnState Apply() => TurnLoop.Pass(State);
}

/// <summary>The game is over.</summary>
public sealed class TerminalDecision : Decision
{
    internal TerminalDecision(TurnState state, Resolution<GameResult> result)
    {
        State = state;
        Result = result;
    }

    /// <summary>The state this decision was read from.</summary>
    public TurnState State { get; }

    /// <summary>The winner and the value of the win, as <see cref="Outcome.ResultOf"/> gives it, or the decline it makes.</summary>
    public Resolution<GameResult> Result { get; }
}

/// <summary>The engine declines to go on: both players are wholly suspended, or <see cref="LegalPlays.For"/> declined.</summary>
public sealed class UnresolvedDecision : Decision
{
    internal UnresolvedDecision(UnresolvedResult result) => Result = result;

    /// <summary>What was declined and where the corpus is silent.</summary>
    public UnresolvedResult Result { get; }
}
