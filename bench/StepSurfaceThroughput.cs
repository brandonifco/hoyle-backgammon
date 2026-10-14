#:project ../src/HoyleBackgammon/HoyleBackgammon.csproj
#:property TargetFrameworks=net10.0
#:property GenerateDocumentationFile=false
#:property RestorePackagesWithLockFile=false
#:property TreatWarningsAsErrors=false

// Throughput of the turn loop, through Game.Play and through the step surface (TurnLoop), over a seeded fixed batch.
//
//   dotnet run -c Release bench/StepSurfaceThroughput.cs [playouts] [repetitions]
//
// This is a timing harness. It reads a clock, so it is not part of the engine, not in HoyleBackgammon.slnx and not
// run by scripts/validate.sh; AGENTS.md section 8 (determinism) governs the engine, not this file. What it measures
// is deterministic: the same playouts every run, and it checks that the three loops it times did the same work.
// docs/measurements/2026-10-13-step-surface-throughput.md says how to read it.

using System.Collections.Immutable;
using System.Diagnostics;
using HoyleBackgammon;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;

int playouts = args.Length > 0 ? int.Parse(args[0]) : 1_000;
int repetitions = args.Length > 1 ? int.Parse(args[1]) : 7;
var start = new AssertedPosition(Setup.StartingPositionFromCorpus(), "bench");

Console.WriteLine($"playouts per run: {playouts}; runs timed: {repetitions}; seeds 1..{playouts}, stream 1; decider stream 2");

// 1. Game.Play, which builds a GameRecord, with a uniformly random decider.
long GamePlay(out long turns, out long hash)
{
    turns = 0;
    hash = 0;
    var clock = Stopwatch.StartNew();
    for (ulong seed = 1; seed <= (ulong)playouts; seed++)
    {
        var result = Game.Play(start, Pcg32.FromSeed(seed, stream: 1), new RandomDecider(seed));
        var record = ((Resolution<GameRecord>.Resolved)result).Value;
        turns += record.Turns.Length;
        hash = unchecked(hash * 31 + record.Turns.Length * 7 + (int)record.Winner + (int)record.Value);
    }

    return clock.ElapsedTicks;
}

// STEP-BEGIN
// 2. The step surface driven as Game.Play drives it, building the same GameRecord's turns: the cost of the surface
//    with the bookkeeping a recorder does.
long StepWithRecord(out long turns, out long hash)
{
    turns = 0;
    hash = 0;
    var clock = Stopwatch.StartNew();
    for (ulong seed = 1; seed <= (ulong)playouts; seed++)
    {
        var source = Pcg32.FromSeed(seed, stream: 1);
        var decider = new RandomDecider(seed);
        var list = new List<Turn>();
        var state = TurnLoop.Begin(start);
        OpeningRoll? roll = null;
        DiceThrow? thrown = null;
        while (!state.IsOver)
        {
            switch (TurnLoop.Next(state))
            {
                case ChanceDecision { Kind: ChanceKind.OpeningRoll } chance:
                    var rolled = chance.Sample(source);
                    roll = rolled.Roll;
                    state = rolled.State;
                    break;
                case ChanceDecision chance:
                    var sampled = chance.Sample(source);
                    thrown = sampled.Thrown;
                    state = sampled.State;
                    break;
                case AdoptionDecision adoption:
                    bool adopt = decider.AdoptOpeningThrow(roll!);
                    state = adoption.Apply(adopt);
                    thrown = adopt ? roll!.Deciding : null;
                    break;
                case PassDecision pass:
                    list.Add(new Turn(pass.Player, null, null, state.Position));
                    state = pass.Apply();
                    break;
                case PlayDecision choice:
                    var play = choice.Plays[decider.ChoosePlay(choice.Player, choice.Plays)];
                    state = choice.Apply(play);
                    list.Add(new Turn(choice.Player, thrown, play, state.Position));
                    break;
                default:
                    throw new InvalidOperationException("the game declined");
            }
        }

        var result = ((TerminalDecision)TurnLoop.Next(state)).Result;
        var outcome = ((Resolution<GameResult>.Resolved)result).Value;
        turns += list.Count;
        hash = unchecked(hash * 31 + list.Count * 7 + (int)outcome.Winner + (int)outcome.Value);
    }

    return clock.ElapsedTicks;
}

// 3. A lean playout, the way a search that only wants a result would run it: no record, no history, the chosen play
//    applied by index.
long StepLean(out long turns, out long hash)
{
    turns = 0;
    hash = 0;
    var clock = Stopwatch.StartNew();
    for (ulong seed = 1; seed <= (ulong)playouts; seed++)
    {
        var source = Pcg32.FromSeed(seed, stream: 1);
        var decider = new RandomDecider(seed);
        long count = 0;
        var state = TurnLoop.Begin(start);
        OpeningRoll? roll = null;
        while (!state.IsOver)
        {
            switch (TurnLoop.Next(state))
            {
                case ChanceDecision chance:
                    var sampled = chance.Sample(source);
                    roll = sampled.Roll ?? roll;
                    state = sampled.State;
                    break;
                case AdoptionDecision adoption:
                    state = adoption.Apply(decider.AdoptOpeningThrow(roll!));
                    break;
                case PassDecision pass:
                    count++;
                    state = pass.Apply();
                    break;
                case PlayDecision choice:
                    count++;
                    state = choice.Apply(decider.ChoosePlay(choice.Player, choice.Plays));
                    break;
                default:
                    throw new InvalidOperationException("the game declined");
            }
        }

        var outcome = ((Resolution<GameResult>.Resolved)((TerminalDecision)TurnLoop.Next(state)).Result).Value;
        turns += count;
        hash = unchecked(hash * 31 + count * 7 + (int)outcome.Winner + (int)outcome.Value);
    }

    return clock.ElapsedTicks;
}

// 4. Each operation alone, over the states one batch of playouts passes through: how the turn splits.
void OperationCosts()
{
    var playStates = new List<TurnState>();
    var turnStates = new List<TurnState>();
    var chanceStates = new List<TurnState>();
    var decisions = new List<PlayDecision>();
    for (ulong seed = 1; seed <= (ulong)Math.Min(playouts, 200); seed++)
    {
        var source = Pcg32.FromSeed(seed, stream: 1);
        var decider = new RandomDecider(seed);
        var state = TurnLoop.Begin(start);
        OpeningRoll? roll = null;
        while (!state.IsOver)
        {
            var decision = TurnLoop.Next(state);
            switch (decision)
            {
                case ChanceDecision chance:
                    if (chance.Kind == ChanceKind.Throw)
                    {
                        chanceStates.Add(state);
                    }

                    var sampled = chance.Sample(source);
                    roll = sampled.Roll ?? roll;
                    state = sampled.State;
                    break;
                case AdoptionDecision adoption:
                    state = adoption.Apply(decider.AdoptOpeningThrow(roll!));
                    break;
                case PassDecision pass:
                    state = pass.Apply();
                    break;
                case PlayDecision choice:
                    playStates.Add(state);
                    decisions.Add(choice);
                    state = choice.Apply(decider.ChoosePlay(choice.Player, choice.Plays));
                    turnStates.Add(state);
                    break;
                default:
                    throw new InvalidOperationException("the game declined");
            }
        }
    }

    void Time(string name, int count, Action once)
    {
        once();
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < repetitions; i++)
        {
            once();
        }

        double ms = clock.ElapsedTicks * 1000.0 / Stopwatch.Frequency / repetitions;
        Console.WriteLine($"{name,-52} {count,7} calls/run  {ms,8:F2} ms/run  =>  {count / (ms / 1000.0),12:N0} per second, {ms * 1000.0 / count,8:F3} us each");
    }

    long sink = 0;
    Time("Next at a Play state (LegalPlays.For)", playStates.Count, () => { foreach (var s in playStates) { sink += ((PlayDecision)TurnLoop.Next(s)).Plays.Length; } });
    Time("PlayDecision.Apply(index 0) (winner check, next state)", decisions.Count, () => { foreach (var d in decisions) { sink += d.Apply(0).Position.GetHashCode(); } });
    Time("Next at a Turn state (suspension check, option)", turnStates.Count, () => { foreach (var s in turnStates) { sink += TurnLoop.Next(s) is ChanceDecision ? 1 : 0; } });
    Time("Next + ChanceDecision.Apply(chosen throw)", chanceStates.Count, () => { foreach (var s in chanceStates) { sink += ((ChanceDecision)TurnLoop.Next(s)).Apply(new DiceThrow(6, 5)).Phase == TurnPhase.Play ? 1 : 0; } });
    var rng = Pcg32.FromSeed(99UL, stream: 1);
    Time("Next + ChanceDecision.Sample (two draws)", chanceStates.Count, () => { foreach (var s in chanceStates) { sink += ((ChanceDecision)TurnLoop.Next(s)).Sample(rng).Thrown.First; } });
    Console.WriteLine($"(sink {sink}; keeps the loops from being optimised away)");
}
// STEP-END

void Report(string name, Func<(long Ticks, long Turns, long Hash)> run)
{
    run(); // one untimed run: JIT and tiered compilation settle before anything is timed
    var times = new List<double>();
    long turns = 0, hash = 0;
    for (int i = 0; i < repetitions; i++)
    {
        var (ticks, t, h) = run();
        turns = t;
        hash = h;
        times.Add(ticks * 1000.0 / Stopwatch.Frequency);
    }

    var sorted = times.OrderBy(x => x).ToArray();
    double median = sorted[sorted.Length / 2];
    Console.WriteLine(
        $"{name,-34} turns={turns} check={hash}  run ms: [{string.Join(", ", times.Select(x => x.ToString("F1")))}]  "
        + $"median {median:F1} ms  =>  {turns / (median / 1000.0):N0} turns/s, {playouts / (median / 1000.0):N0} playouts/s");
}

Report("Game.Play (builds a GameRecord)", () => { long ticks = GamePlay(out long t, out long h); return (ticks, t, h); });
// STEP-BEGIN
Report("step surface, with Turn records", () => { long ticks = StepWithRecord(out long t, out long h); return (ticks, t, h); });
Report("step surface, lean playout", () => { long ticks = StepLean(out long t, out long h); return (ticks, t, h); });
OperationCosts();
// STEP-END

internal sealed class RandomDecider(ulong seed) : IDecider
{
    private readonly Pcg32 _answers = Pcg32.FromSeed(seed, stream: 2);

    public bool AdoptOpeningThrow(OpeningRoll roll) => (_answers.NextUInt32() & 1) == 0;

    public int ChoosePlay(Player player, ImmutableArray<Play> plays) => (int)(_answers.NextUInt32() % (uint)plays.Length);
}
