using System.Collections.Immutable;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon.Tests;

/// <summary>
/// A game driven from outside the engine through the step surface alone, the way a caller who is not
/// <see cref="Game.Play"/> would drive it: <see cref="TurnLoop.Next"/>, then the operation the decision offers. It is
/// written separately from <see cref="Game.Play"/>, with no turn limit, and builds the <see cref="GameRecord"/> a
/// recorder would.
/// </summary>
internal static class StepGame
{
    /// <summary>
    /// Plays a game. With <paramref name="chosen"/> the driver throws the dice itself (<see cref="Opening"/>) and hands
    /// each throw to <see cref="ChanceDecision.Apply(DiceThrow)"/>, so the generator is never given to the step
    /// surface; without it the step surface samples.
    /// </summary>
    public static Resolution<GameRecord> Play(
        AssertedPosition start, IRandomSource source, IDecider decider, Player? opener = null, bool chosen = false)
    {
        var state = TurnLoop.Begin(start, opener);
        OpeningRoll? roll = null;
        bool adopted = false;
        DiceThrow? thrown = null;
        var turns = ImmutableArray.CreateBuilder<Turn>();

        while (true)
        {
            switch (TurnLoop.Next(state))
            {
                case ChanceDecision { Kind: ChanceKind.OpeningRoll } chance:
                    if (chosen)
                    {
                        roll = Opening.RollForTheRight(source);
                        state = chance.Apply(roll.Deciding);
                    }
                    else
                    {
                        var rolled = chance.Sample(source);
                        roll = rolled.Roll;
                        state = rolled.State;
                    }

                    break;

                case ChanceDecision chance:
                    if (chosen)
                    {
                        thrown = Opening.Throw(source);
                        state = chance.Apply(thrown.Value);
                    }
                    else
                    {
                        var sampled = chance.Sample(source);
                        thrown = sampled.Thrown;
                        state = sampled.State;
                    }

                    break;

                case AdoptionDecision adoption:
                    adopted = decider.AdoptOpeningThrow(roll!);
                    state = adoption.Apply(adopted);
                    thrown = adopted ? roll!.Deciding : null;
                    break;

                case PassDecision pass:
                    turns.Add(new Turn(pass.Player, null, null, state.Position));
                    state = pass.Apply();
                    break;

                case PlayDecision choice:
                    var play = choice.Plays[decider.ChoosePlay(choice.Player, choice.Plays)];
                    state = chosen ? choice.Apply(choice.Plays.IndexOf(play)) : choice.Apply(play);
                    turns.Add(new Turn(choice.Player, thrown, play, state.Position));
                    break;

                case TerminalDecision terminal:
                    return terminal.Result.Match(
                        result => Resolution<GameRecord>.FromValue(new GameRecord(
                            Game.Identity,
                            MapPackage.FromProvenance,
                            start, roll, adopted, turns.ToImmutable(), result.Winner, result.Value, Outcome.Next(result.Value))
                        {
                            ValueRulings = result.Rulings,
                        }),
                        Resolution<GameRecord>.FromUnresolved);

                case UnresolvedDecision unresolved:
                    return Resolution<GameRecord>.FromUnresolved(unresolved.Result);
            }
        }
    }
}

/// <summary>
/// A decider whose answers come from a generator of its own, so two instances made from one seed answer alike and a
/// game is not confined to the first play of every list.
/// </summary>
internal sealed class SeededDecider(ulong seed) : IDecider
{
    private readonly Pcg32 _answers = Pcg32.FromSeed(seed, stream: 2);

    public bool AdoptOpeningThrow(OpeningRoll roll) => (_answers.NextUInt32() & 1) == 0;

    public int ChoosePlay(Player player, ImmutableArray<Play> plays) => (int)(_answers.NextUInt32() % (uint)plays.Length);
}
