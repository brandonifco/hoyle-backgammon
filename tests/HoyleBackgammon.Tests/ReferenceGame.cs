using System.Collections.Immutable;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon.Tests;

/// <summary>
/// <see cref="Game.Play"/> as it was before it became a client of <see cref="TurnLoop"/>: the loop, copied from
/// the commit before the step surface (<c>fda763a</c>, <c>src/HoyleBackgammon/Game.cs</c> lines 131 to 234) and
/// changed in nothing but its name and its comments. It exists so that "the refactor changed no seeded game" is
/// checked against the loop that played the games, and not against the loop under test: a test comparing
/// <see cref="Game.Play"/> with a driver of the same step surface would pass under any change to the surface that
/// both of them see.
/// </summary>
/// <remarks>
/// It uses only the primitives the loop was built from (<see cref="Opening"/>, <see cref="Movement"/>,
/// <see cref="LegalPlays"/>, <see cref="Outcome"/>). It is the one place in the tests where the loop is written out
/// by hand, and it states no rule of the game.
/// </remarks>
internal static class ReferenceGame
{
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
                return Outcome.ResultOf(position, winner).Match(
                    result => Resolution<GameRecord>.FromValue(new GameRecord(
                        Game.Identity,
                        MapPackage.FromProvenance,
                        start, roll, adopted, turns.ToImmutable(), result.Winner, result.Value, Outcome.Next(result.Value))
                    {
                        ValueRulings = result.Rulings,
                    }),
                    Resolution<GameRecord>.FromUnresolved);
            }

            toMove = toMove.Adversary();
        }

        throw new InvalidOperationException(
            $"the game passed the runaway guard of {turnLimit} turns without ending.");
    }
}
