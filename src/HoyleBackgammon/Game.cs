using System.Collections.Immutable;
using RulesKernel.Identity;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon;

/// <summary>One player's turn, as it happened.</summary>
/// <param name="Player">Whose turn it was.</param>
/// <param name="Thrown">
/// What he threw, or null when his play was wholly suspended and he did not throw at all.
/// </param>
/// <param name="Play">What he played. Empty of moves when nothing in the throw could be played.</param>
/// <param name="Position">The position his turn left behind.</param>
public sealed record Turn(Player Player, DiceThrow? Thrown, Play? Play, Position Position);

/// <summary>A finished game.</summary>
/// <param name="Start">The position the game began from, and who asserted it.</param>
/// <param name="OpeningRoll">
/// The throw for the right to begin, or null when the previous game settled who opens.
/// </param>
/// <param name="OpeningThrowAdopted">Whether the opener adopted the deciding pair.</param>
/// <param name="Turns">Every turn, in order.</param>
/// <param name="Winner">Who won.</param>
/// <param name="Value">Whether the win was a hit, a gammon or a backgammon.</param>
/// <param name="Next">Who throws first in the game after this one.</param>
public sealed record GameRecord(
    AssertedPosition Start,
    OpeningRoll? OpeningRoll,
    bool OpeningThrowAdopted,
    ImmutableArray<Turn> Turns,
    Player Winner,
    GameValue Value,
    NextOpening Next);

/// <summary>
/// Plays one game through, from a position somebody asserted.
/// </summary>
public static class Game
{
    /// <summary>
    /// The engine's replay identity: which ruleset, which replay schema, which pinned corpus
    /// and which generator. Two runs are comparable only if these agree.
    /// </summary>
    public static ReplayCompatibilityIdentity Identity { get; } = new(
        ruleset: new RulesetVersion("hoyle-1909-backgammon", 1),
        replaySchema: new ReplaySchemaVersion(1),
        sourceBaselines: [MapEntries.Baseline],
        randomAlgorithm: RandomAlgorithmId.Pcg32SetSeq64XshRr32);

    /// <summary>
    /// Plays a game from <paramref name="start"/>.
    /// </summary>
    /// <remarks>
    /// The position is demanded rather than defaulted. The engine does have the corpus's
    /// starting arrangement (<see cref="Setup.StartingPositionFromCorpus"/>) and a caller may
    /// hand it straight back in, but most games worth playing out here do not begin there —
    /// a mid-game study or a replayed log begins wherever it begins, and defaulting would
    /// make "the corpus's arrangement" and "whatever the caller had in mind" look the same in
    /// the record. So the assertion travels into <see cref="GameRecord.Start"/>, and a reader
    /// of a result can always see whose position it was played from.
    /// </remarks>
    /// <param name="start">The asserted starting position.</param>
    /// <param name="source">The generator. Consumed one draw per die thrown.</param>
    /// <param name="decider">Where the choices come from.</param>
    /// <param name="opener">
    /// Who begins, when the previous game settled it — <see cref="MapEntries.NextGameOpening"/>
    /// gives the winner of a hit the first throw with no roll for the right. Null throws for
    /// the right as at starting.
    /// </param>
    /// <param name="turnLimit">
    /// A runaway guard, not a rule. Nothing in the corpus bounds a game's length; this exists
    /// so a bug cannot hang a test run.
    /// </param>
    public static Resolution<GameRecord> Play(
        AssertedPosition start,
        IRandomSource source,
        IDecider decider,
        Player? opener = null,
        int turnLimit = 100_000)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(decider);
        ArgumentOutOfRangeException.ThrowIfLessThan(turnLimit, 1);

        // next-game-opening: after a hit the winner "throws first in the game next following",
        // with no roll for the right and so no option to throw again. After a gammon or a
        // backgammon the players "throw again for the right to begin, as at starting". An
        // opener supplied here is the first case; null is the second.
        var roll = opener is null ? Opening.RollForTheRight(source) : null;
        var toMove = opener ?? roll!.Opener;

        // The option belongs to the opener's first throw, and is not taken until he actually
        // throws: a player who is wholly suspended does not throw, and must not consume the
        // dice (or the decision) on a turn he never gets.
        bool optionOutstanding = roll is not null;
        bool adopted = false;

        var position = start.Position;
        var turns = ImmutableArray.CreateBuilder<Turn>();

        for (int turn = 0; turn < turnLimit; turn++)
        {
            // full-table-suspension: a suspended player does not throw. If both are suspended
            // at once -- each with a man up against the other's full home table, which
            // fifteen men a side does permit -- neither can ever move, and the corpus says
            // only that "the adversary continues to throw and move". No entry covers the
            // combination.
            //
            // This branch is live, and reachable only from an asserted start. Under
            // rules-factory/docs/decisions/0006 no sequence of play produces it: a player's
            // own move never puts his own man on the bar, a move never adds ADVERSARY men to
            // the adversary's home table, and a player already suspended does not move at all,
            // so a newly-suspended player was suspended already. (Under the reading 0006
            // rejected it does arise in play -- enter onto your own blot in the adversary's
            // table, and that table is then "full" while you still have a man up.) What keeps
            // it live is that Play demands a position rather than deriving one: a caller may
            // assert the deadlock, and DeterminismTests
            // .Two_players_suspended_against_each_other_is_an_interaction_no_entry_covers does.
            if (Movement.IsWhollySuspended(position, toMove))
            {
                if (Movement.IsWhollySuspended(position, toMove.Adversary()))
                {
                    return Resolution<GameRecord>.FromUnresolved(new UnresolvedResult(
                        UnresolvedReason.UnsupportedInteraction,
                        "continue a game in which both players are wholly suspended against "
                        + "each other's full home tables",
                        MapEntries.FullTableSuspension.Locator));
                }

                turns.Add(new Turn(toMove, null, null, position));
                toMove = toMove.Adversary();
                continue;
            }

            DiceThrow thrown;
            if (optionOutstanding && roll is not null && toMove == roll.Opener)
            {
                optionOutstanding = false;
                adopted = decider.AdoptOpeningThrow(roll);
                thrown = Opening.OpeningThrow(roll, adopted, source);
            }
            else
            {
                thrown = Opening.Throw(source);
            }

            var legal = LegalPlays.For(position, toMove, Movement.Entitlement(thrown));
            if (legal is Resolution<ImmutableArray<Play>>.Unresolved unresolved)
            {
                return Resolution<GameRecord>.FromUnresolved(unresolved.Result);
            }

            var plays = ((Resolution<ImmutableArray<Play>>.Resolved)legal).Value;
            var play = plays[decider.ChoosePlay(toMove, plays)];
            position = play.Result;
            turns.Add(new Turn(toMove, thrown, play, position));

            if (Outcome.Winner(position) is { } winner)
            {
                return Outcome.ValueOf(position, winner).Match(
                    value => Resolution<GameRecord>.FromValue(new GameRecord(
                        start, roll, adopted, turns.ToImmutable(), winner, value, Outcome.Next(value))),
                    Resolution<GameRecord>.FromUnresolved);
            }

            toMove = toMove.Adversary();
        }

        throw new InvalidOperationException(
            $"the game passed the runaway guard of {turnLimit} turns without ending.");
    }
}
