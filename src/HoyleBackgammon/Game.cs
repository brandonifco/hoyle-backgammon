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

/// <summary>A finished game, with the replay identity and the map it was played under.</summary>
/// <remarks>
/// Two records are comparable only when <see cref="Identity"/> and <see cref="Map"/> agree.
/// <see cref="ToCanonicalJson"/> is the record as bytes, the engine's own and the only rendering a
/// replay should hash (<c>docs/decisions/0006</c>).
/// </remarks>
/// <param name="Identity">The replay identity the game was played under, <see cref="Game.Identity"/>.</param>
/// <param name="Map">The map package the engine was produced from, <see cref="MapPackage.FromProvenance"/>.</param>
/// <param name="Start">The position the game began from, and who asserted it.</param>
/// <param name="OpeningRoll">
/// The throw for the right to begin, or null when the previous game settled who opens.
/// </param>
/// <param name="OpeningThrowAdopted">Whether the opener adopted the deciding pair.</param>
/// <param name="Turns">Every turn, in order.</param>
/// <param name="Winner">Who won.</param>
/// <param name="Value">Whether the win was a hit, a gammon or a backgammon. <see cref="ValueRulings"/> names any owner's ruling it relies on.</param>
/// <param name="Next">Who throws first in the game after this one.</param>
public sealed record GameRecord(
    ReplayCompatibilityIdentity Identity,
    MapPackage Map,
    AssertedPosition Start,
    OpeningRoll? OpeningRoll,
    bool OpeningThrowAdopted,
    ImmutableArray<Turn> Turns,
    Player Winner,
    GameValue Value,
    NextOpening Next)
{
    /// <summary>
    /// The owner's rulings <see cref="Value"/> relies on, in <see cref="OwnerRulings.All"/>'s order; empty when the
    /// corpus names the result unaided (<see cref="GameResult.Rulings"/>, <c>docs/decisions/0010</c>).
    /// </summary>
    public ImmutableArray<OwnerRuling> ValueRulings { get; init; } = [];

    /// <summary>The game's result, for scoring a rubber: the winner and the value, with the rulings the value relies on.</summary>
    public GameResult Result => new(Winner, Value) { Rulings = ValueRulings };

    /// <summary>
    /// The record in its canonical serialisation, replay schema 4: RFC 8785 canonical JSON, UTF-8
    /// without a byte-order mark, every field of the record including <see cref="Identity"/>,
    /// <see cref="Map"/>, on each turn played the owner's rulings its play relies on
    /// (<see cref="Play.Rulings"/>), and the owner's rulings the game's value relies on
    /// (<see cref="ValueRulings"/>), each with who ruled and when. The same game gives the same bytes on every
    /// run, framework and platform (<c>docs/decisions/0006</c> defines the shape; <c>docs/decisions/0009</c> adds
    /// each turn's <c>rulings</c>; <c>docs/decisions/0010</c> adds <c>valueRulings</c>).
    /// </summary>
    /// <returns>The bytes.</returns>
    public byte[] ToCanonicalJson() => GameRecordJson.Serialise(this);
}

/// <summary>
/// Plays one game through, from a position somebody asserted.
/// </summary>
public static class Game
{
    /// <summary>
    /// The engine's replay identity: which ruleset, which replay schema, which pinned corpus
    /// and which generator. Two runs are comparable only if these agree, and every
    /// <see cref="GameRecord"/> carries it.
    /// </summary>
    /// <remarks>
    /// The replay schema is version 4 since the record names the owner's rulings its value relies on
    /// (<c>valueRulings</c>, <c>docs/decisions/0010</c>), and version 3 since each turn of a <see cref="GameRecord"/> names the owner's
    /// rulings its play relies on (<c>docs/decisions/0009</c>); version 2 gave the record its identity,
    /// its map and a canonical serialisation of its own (<c>docs/decisions/0006</c>).
    /// The ruleset is version 6 from Brandon's rulings of 2026-09-15 on the first parts of those two questions and on
    /// all of <c>game-value</c>'s, which make every throw playable and every finish valued: where either number alone
    /// can be played the higher is compelled, a man hit mid-bear-off who re-enters bears off again only once every man
    /// is home, the finish no named result covers is a hit, and a loser both gammoned and backgammoned by the corpus's
    /// words is backgammoned. A throw or finish version 5 declined for any of them is played or valued, and names the
    /// ruling (<c>docs/decisions/0010</c>).
    /// Version 5 began with Brandon's rulings of 2026-09-15 on, which answer the second parts of
    /// two questions <c>RulesFactory.Maps.HoyleBackgammon</c> 6.0.0 leaves open, and not from the corpus:
    /// orders of a throw reaching the same position are one play, and bearing off begins within the throw
    /// that brings the last man home. A throw version 4 declined for either is played, and the play names
    /// the ruling (<c>docs/decisions/0009</c>). Version 4 began with map 6.0.0 and declined both
    /// (<c>docs/decisions/0008</c>).
    /// Version 3 began with <c>RulesFactory.Maps.HoyleBackgammon</c> 4.0.0, which
    /// makes a win against a loser with nothing off and a man in the winner's home table decline
    /// where version 2 valued it a backgammon (<c>docs/decisions/0005</c>). Version 2 began with
    /// map 3.0.0, whose corrections made some games decline where version 1 finished them
    /// (<c>docs/decisions/0004</c>).
    /// </remarks>
    public static ReplayCompatibilityIdentity Identity { get; } = new(
        ruleset: new RulesetVersion("hoyle-1909-backgammon", 6),
        replaySchema: new ReplaySchemaVersion(4),
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
                return Outcome.ResultOf(position, winner).Match(
                    result => Resolution<GameRecord>.FromValue(new GameRecord(
                        Identity,
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
