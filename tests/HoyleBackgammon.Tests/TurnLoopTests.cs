using System.Collections.Immutable;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// Positions for the tests that need a player wholly suspended
/// (<see cref="MapEntries.FullTableSuspension"/>), as a caller would assert them.
/// </summary>
internal static class Suspended
{
    /// <summary>
    /// Black has a man on the bar and White holds all six points of his home table, so Black cannot enter and does
    /// not throw. White's point six holds five men: four sixes bear four of them off and break it.
    /// </summary>
    public static AssertedPosition BlackAgainstSixPoints { get; } = new(
        Board.Of(
            Board.Men().At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2).At(6, 5),
            Board.Men().At(Geometry.BarPip, 1).RestAt(13)),
        AssertedBy: nameof(Suspended));

    /// <summary>The same suspension with White's last three men on the eight point, so he is not yet bearing off.</summary>
    public static AssertedPosition BlackAgainstSixPointsWithMenOut { get; } = new(
        Board.Of(
            Board.Men().At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2).At(6, 2).RestAt(8),
            Board.Men().At(Geometry.BarPip, 1).RestAt(13)),
        AssertedBy: nameof(Suspended));

    /// <summary>Each player has a man up against the other's full home table, so neither can ever move.</summary>
    public static AssertedPosition EachAgainstTheOther { get; } = new(
        Board.Of(
            Board.Men().At(Geometry.BarPip, 1).At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2).At(6, 2).RestAt(8),
            Board.Men().At(Geometry.BarPip, 1).At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2).At(6, 2).RestAt(8)),
        AssertedBy: nameof(Suspended));
}

/// <summary>
/// <c>Game.Play</c> as a client of the step surface: no seeded game changed (the loop that played the games before,
/// <see cref="ReferenceGame"/>, is the oracle), and a caller driving the surface itself reaches the same record.
/// </summary>
public class TurnLoopEquivalenceTests
{
    private const int Seeds = 1_000;

    private static byte[] Bytes(Resolution<GameRecord> result) =>
        Assert.IsType<Resolution<GameRecord>.Resolved>(result).Value.ToCanonicalJson();

    private static Pcg32 Source(ulong seed) => Pcg32.FromSeed(seed, stream: 1);

    [Fact]
    public void Game_Play_as_a_client_of_the_step_surface_gives_the_bytes_the_loop_it_replaced_gave_over_a_thousand_seeds()
    {
        // Mutation: ChanceDecision.Sample threw once and discarded it before the throw it kept, four draws a turn for
        // two (mutations/turnloop-equivalence.json); this test went red.
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var expected = Bytes(ReferenceGame.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed)));
            var actual = Bytes(Game.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed)));

            Assert.True(expected.AsSpan().SequenceEqual(actual), $"seed {seed}: Game.Play's record is not the reference loop's.");
        }
    }

    [Fact]
    public void Driving_the_step_surface_reproduces_the_Game_Play_record_byte_for_byte_over_a_thousand_seeds()
    {
        // Mutation: Game.Play answered the opening option with the opposite of what its decider said
        // (`adoption.Apply(!adopted)`); this test went red, against a driver that does not.
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var expected = Bytes(Game.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed)));
            var actual = Bytes(StepGame.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed)));

            Assert.True(expected.AsSpan().SequenceEqual(actual), $"seed {seed}: the step surface's record is not Game.Play's.");
        }
    }

    [Fact]
    public void Driving_the_step_surface_by_chosen_outcomes_reproduces_the_record_too()
    {
        // The driver draws the dice itself and hands them over as chosen outcomes, so the generator never reaches the
        // step surface. Mutation: ChanceDecision.Apply took an opening outcome with White's die and Black's swapped,
        // which Sample does not; this test went red.
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var expected = Bytes(Game.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed)));
            var actual = Bytes(StepGame.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed), chosen: true));

            Assert.True(expected.AsSpan().SequenceEqual(actual), $"seed {seed}: chosen outcomes did not reproduce Game.Play's record.");
        }
    }

    [Theory]
    [InlineData(Player.White)]
    [InlineData(Player.Black)]
    public void A_game_the_previous_one_settled_who_opens_has_no_roll_and_no_option_in_any_driver(Player opener)
    {
        // Mutation: Begin with an opener left an opening option outstanding for him, with no roll behind it, so the
        // decider was asked a question the loop never asked; this test went red.
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var reference = ReferenceGame.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed), opener);
            var game = Game.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed), opener);
            var stepped = StepGame.Play(Corpus.StartingPosition, Source(seed), new SeededDecider(seed), opener);

            var record = Assert.IsType<Resolution<GameRecord>.Resolved>(game).Value;
            Assert.Null(record.OpeningRoll);
            Assert.False(record.OpeningThrowAdopted);
            Assert.Equal(opener, record.Turns[0].Player);
            Assert.True(Bytes(reference).AsSpan().SequenceEqual(Bytes(game)), $"seed {seed}");
            Assert.True(Bytes(reference).AsSpan().SequenceEqual(Bytes(stepped)), $"seed {seed}");
        }
    }

    [Fact]
    public void A_game_played_from_a_position_with_a_player_wholly_suspended_records_the_forfeited_turns_as_the_loop_did()
    {
        // Mutation: TurnLoop.Pass left the side to move as it was, so the suspended player was passed to again and
        // again; this test went red (Game.Play reached its runaway guard).
        int passes = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var start = Suspended.BlackAgainstSixPointsWithMenOut;
            var reference = Bytes(ReferenceGame.Play(start, Source(seed), new SeededDecider(seed)));
            var game = Game.Play(start, Source(seed), new SeededDecider(seed));
            var stepped = Bytes(StepGame.Play(start, Source(seed), new SeededDecider(seed)));

            Assert.True(reference.AsSpan().SequenceEqual(Bytes(game)), $"seed {seed}");
            Assert.True(reference.AsSpan().SequenceEqual(stepped), $"seed {seed}");
            passes += Assert.IsType<Resolution<GameRecord>.Resolved>(game).Value.Turns.Count(t => t.Thrown is null);
        }

        // Without this the loop above would pass against a start nobody was suspended in.
        Assert.True(passes > 0, "no game forfeited a turn.");
    }

    [Fact]
    public void Both_players_wholly_suspended_declines_the_same_way_in_every_driver()
    {
        // Mutation: TurnLoop.Next never found the adversary suspended too, and passed a turn that nobody can play;
        // this test went red.
        var reference = Assert.IsType<Resolution<GameRecord>.Unresolved>(
            ReferenceGame.Play(Suspended.EachAgainstTheOther, Source(3UL), new FirstOptionDecider()));
        var game = Assert.IsType<Resolution<GameRecord>.Unresolved>(
            Game.Play(Suspended.EachAgainstTheOther, Source(3UL), new FirstOptionDecider()));
        var stepped = Assert.IsType<Resolution<GameRecord>.Unresolved>(
            StepGame.Play(Suspended.EachAgainstTheOther, Source(3UL), new FirstOptionDecider()));

        Assert.Equal(UnresolvedReason.UnsupportedInteraction, game.Result.Reason);
        Assert.Equal(MapEntries.FullTableSuspension.Locator, game.Result.Locator);
        Assert.Equal(reference.Result, game.Result);
        Assert.Equal(reference.Result, stepped.Result);

        // And from the step surface directly, whoever opens, without a generator: the deadlock is read off the state.
        foreach (var opener in Players.Both)
        {
            var decision = TurnLoop.Next(TurnLoop.Begin(Suspended.EachAgainstTheOther, opener));
            Assert.Equal(reference.Result, Assert.IsType<UnresolvedDecision>(decision).Result);
        }
    }

    [Fact]
    public void Game_Play_keeps_its_runaway_guard_counting_the_turns_passed_and_the_step_surface_has_none()
    {
        // Mutation: Game.Play's guard let one turn more than the limit through (`turn <= turnLimit`); this test went
        // red at the limit one short of the game's length.
        foreach (var start in new[] { Corpus.StartingPosition, Suspended.BlackAgainstSixPointsWithMenOut })
        {
            var record = Assert.IsType<Resolution<GameRecord>.Resolved>(
                StepGame.Play(start, Source(11UL), new FirstOptionDecider())).Value;
            int length = record.Turns.Length;
            Assert.True(length > 3);

            // The limit is the game's length, passed turns included: at that many turns the game is returned, one
            // fewer and it is a runaway, with the message the loop always gave.
            var exact = Game.Play(start, Source(11UL), new FirstOptionDecider(), turnLimit: length);
            Assert.True(record.ToCanonicalJson().AsSpan().SequenceEqual(Bytes(exact)));
            var runaway = Assert.Throws<InvalidOperationException>(
                () => Game.Play(start, Source(11UL), new FirstOptionDecider(), turnLimit: length - 1));
            var expected = Assert.Throws<InvalidOperationException>(
                () => ReferenceGame.Play(start, Source(11UL), new FirstOptionDecider(), turnLimit: length - 1));
            Assert.Equal(expected.Message, runaway.Message);
        }

        // Passed turns are in the record, so the second start's game forfeits some.
        Assert.Contains(
            Assert.IsType<Resolution<GameRecord>.Resolved>(
                StepGame.Play(Suspended.BlackAgainstSixPointsWithMenOut, Source(11UL), new FirstOptionDecider())).Value.Turns,
            t => t.Thrown is null);
    }
}

/// <summary>Chance as data: which outcomes, with what probability, and what sampling draws.</summary>
public class TurnLoopChanceTests
{
    private static ChanceDecision ThrowEvent()
    {
        var state = TurnLoop.Begin(Corpus.StartingPosition, Player.White);
        return Assert.IsType<ChanceDecision>(TurnLoop.Next(state));
    }

    private static ChanceDecision OpeningEvent() =>
        Assert.IsType<ChanceDecision>(TurnLoop.Next(TurnLoop.Begin(Corpus.StartingPosition)));

    [Fact]
    public void A_throw_lists_exactly_the_twenty_one_distinct_throws_with_their_exact_probabilities()
    {
        // Mutation: the outcome table gave a doublet 2 ways and an unlike pair 1 (`higher == lower ? 2 : 1`); this
        // test went red.
        var chance = ThrowEvent();

        Assert.Equal(ChanceKind.Throw, chance.Kind);
        Assert.Equal(36, chance.Total);
        Assert.Equal(21, chance.Outcomes.Length);
        Assert.Equal(21, chance.Outcomes.Select(o => (o.Throw.Higher, o.Throw.Lower)).Distinct().Count());
        Assert.All(chance.Outcomes, o => Assert.True(o.Throw.First >= o.Throw.Second, $"{o.Throw} is not higher die first."));

        var doublets = chance.Outcomes.Where(o => o.Throw.IsDoublets).ToArray();
        var unlike = chance.Outcomes.Where(o => !o.Throw.IsDoublets).ToArray();
        Assert.Equal(6, doublets.Length);
        Assert.All(doublets, o => Assert.Equal(1, o.Ways));
        Assert.Equal(15, unlike.Length);
        Assert.All(unlike, o => Assert.Equal(2, o.Ways));

        // The probabilities sum to one: 6/36 + 15 * 2/36 = 36/36.
        Assert.Equal(chance.Total, chance.Outcomes.Sum(o => o.Ways));
        Assert.Equal(36, doublets.Sum(o => o.Ways) + unlike.Sum(o => o.Ways));

        // Every pair of faces from 1 to 6 is there.
        var expected = from high in Enumerable.Range(1, 6) from low in Enumerable.Range(1, high) select (high, low);
        Assert.Equal(expected.OrderBy(x => x), chance.Outcomes.Select(o => (o.Throw.Higher, o.Throw.Lower)).OrderBy(x => x));
    }

    [Fact]
    public void The_opening_roll_lists_only_the_non_tied_throws_renormalised_over_thirty()
    {
        // Mutation: the opening table kept the tied pairs (`white != black` replaced by `white >= 1`), listing 36
        // outcomes; this test went red.
        var chance = OpeningEvent();

        Assert.Equal(ChanceKind.OpeningRoll, chance.Kind);
        Assert.Equal(30, chance.Total);
        Assert.Equal(30, chance.Outcomes.Length);
        Assert.All(chance.Outcomes, o => Assert.False(o.Throw.IsDoublets, $"{o.Throw} is a tie."));
        Assert.All(chance.Outcomes, o => Assert.Equal(1, o.Ways));
        Assert.Equal(chance.Total, chance.Outcomes.Sum(o => o.Ways));

        // White's die then Black's: (5, 3) and (3, 5) are different outcomes, since a different player opens.
        Assert.Equal(30, chance.Outcomes.Select(o => (o.Throw.First, o.Throw.Second)).Distinct().Count());
        Assert.Contains(chance.Outcomes, o => o.Throw.First == 5 && o.Throw.Second == 3);
        Assert.Contains(chance.Outcomes, o => o.Throw.First == 3 && o.Throw.Second == 5);
    }

    [Fact]
    public void The_sampled_frequencies_of_a_throw_agree_with_the_listed_probabilities_over_a_fixed_seed()
    {
        // Mutation: ChanceDecision.Sample threw both dice and reported the first twice, so only the six doublets were
        // ever drawn; this test went red.
        var chance = ThrowEvent();
        var source = Pcg32.FromSeed(20261013UL, stream: 1);
        const int Samples = 36_000;
        var seen = new Dictionary<(int, int), int>();
        for (int i = 0; i < Samples; i++)
        {
            var thrown = chance.Sample(source).Thrown;
            var key = (thrown.Higher, thrown.Lower);
            seen[key] = seen.GetValueOrDefault(key) + 1;
        }

        Assert.Equal(21, seen.Count);
        foreach (var outcome in chance.Outcomes)
        {
            double p = (double)outcome.Ways / chance.Total;
            double expected = Samples * p;
            double sigma = Math.Sqrt(Samples * p * (1 - p));
            int observed = seen[(outcome.Throw.Higher, outcome.Throw.Lower)];
            Assert.True(
                Math.Abs(observed - expected) <= 5 * sigma,
                $"{outcome.Throw}: {observed} sampled, {expected} expected (5 sigma = {5 * sigma}).");
        }
    }

    [Fact]
    public void The_sampled_frequencies_of_the_opening_roll_agree_with_the_listed_probabilities_over_a_fixed_seed()
    {
        // Mutation: Opening.RollForTheRight accepted the first pair thrown, ties included (`attempt.First > 0` for
        // `attempt.First != attempt.Second`), so a tie was sampled as the roll; this test went red.
        var chance = OpeningEvent();
        var source = Pcg32.FromSeed(20261013UL, stream: 2);
        const int Samples = 30_000;
        var seen = new Dictionary<(int, int), int>();
        for (int i = 0; i < Samples; i++)
        {
            var thrown = chance.Sample(source).Thrown;
            var key = (thrown.First, thrown.Second);
            seen[key] = seen.GetValueOrDefault(key) + 1;
        }

        Assert.Equal(30, seen.Count);
        foreach (var outcome in chance.Outcomes)
        {
            double p = (double)outcome.Ways / chance.Total;
            double expected = Samples * p;
            double sigma = Math.Sqrt(Samples * p * (1 - p));
            int observed = seen[(outcome.Throw.First, outcome.Throw.Second)];
            Assert.True(
                Math.Abs(observed - expected) <= 5 * sigma,
                $"{outcome.Throw}: {observed} sampled, {expected} expected (5 sigma = {5 * sigma}).");
        }
    }

    [Fact]
    public void Sampling_a_throw_takes_two_draws_in_throw_order_and_keeps_the_order_drawn()
    {
        // Mutation: ChanceDecision.Sample reported the pair with its dice in the other order
        // (`new DiceThrow(t.Second, t.First)`); this test went red.
        var source = ScriptedSource.Faces(2, 5);

        var sampled = ThrowEvent().Sample(source);

        Assert.Equal(2, source.Drawn);
        Assert.Equal(new DiceThrow(2, 5), sampled.Thrown);
        Assert.Null(sampled.Roll);
        Assert.Equal(TurnPhase.Play, sampled.State.Phase);
        // The order drawn is not game state: the state holds the higher die first.
        Assert.Equal(new DiceThrow(5, 2), sampled.State.PendingThrow);
    }

    [Fact]
    public void Sampling_the_opening_roll_throws_again_on_a_tie_and_gives_the_deciding_pair_the_option()
    {
        // Mutation: TurnLoop.Rolled named White the opener whatever the dice showed; this test went red.
        var source = ScriptedSource.Faces(3, 3, 2, 5);

        var sampled = OpeningEvent().Sample(source);

        Assert.Equal(4, source.Drawn);
        var roll = Assert.IsType<OpeningRoll>(sampled.Roll);
        Assert.Equal([new DiceThrow(3, 3), new DiceThrow(2, 5)], roll.Attempts.ToArray());
        Assert.Equal(Player.Black, roll.Opener);
        Assert.Equal(new DiceThrow(2, 5), sampled.Thrown);
        Assert.Equal(TurnPhase.Turn, sampled.State.Phase);
        Assert.Equal(Player.Black, sampled.State.ToMove);
        Assert.Equal(new OutstandingOption(Player.Black, new DiceThrow(2, 5)), sampled.State.Option);
        Assert.Null(sampled.State.PendingThrow);
    }

    [Fact]
    public void Sampling_needs_a_generator()
    {
        // Mutation: Sample answered a null generator with one of its own (`source ??= Pcg32.FromSeed(0UL, 1UL)`); this
        // test went red.
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ThrowEvent().Sample(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => OpeningEvent().Sample(null!)).ParamName);
    }
}

/// <summary>Applying a chosen outcome gives the state sampling that outcome gives.</summary>
public class TurnLoopChosenOutcomeTests
{
    [Fact]
    public void Applying_each_of_the_twenty_one_throws_gives_the_state_sampling_it_gives_in_either_order_drawn()
    {
        // Mutation: TurnLoop.Thrown held the throw as given instead of higher die first, so (2, 5) and (5, 2) reached
        // unequal states; this test went red.
        var state = TurnLoop.Begin(Corpus.StartingPosition, Player.White);
        var chance = Assert.IsType<ChanceDecision>(TurnLoop.Next(state));

        foreach (var outcome in chance.Outcomes)
        {
            var chosen = chance.Apply(outcome.Throw);
            var sampledAsListed = chance.Sample(ScriptedSource.Faces(outcome.Throw.First, outcome.Throw.Second)).State;
            var sampledSwapped = chance.Sample(ScriptedSource.Faces(outcome.Throw.Second, outcome.Throw.First)).State;

            Assert.Equal(sampledAsListed, chosen);
            Assert.Equal(sampledSwapped, chosen);
            Assert.Equal(sampledSwapped.GetHashCode(), chosen.GetHashCode());
            Assert.Equal(TurnPhase.Play, chosen.Phase);
            Assert.Equal(Player.White, chosen.ToMove);
            Assert.Equal(outcome.Throw, chosen.PendingThrow);
            Assert.Equal(Corpus.StartingPosition.Position, chosen.Position);
        }
    }

    [Fact]
    public void Applying_each_of_the_thirty_opening_throws_gives_the_state_sampling_it_gives_whatever_ties_came_before()
    {
        // Mutation: TurnLoop.Rolled named the adversary of the player whose die was higher; this test went red.
        var chance = Assert.IsType<ChanceDecision>(TurnLoop.Next(TurnLoop.Begin(Corpus.StartingPosition)));

        foreach (var outcome in chance.Outcomes)
        {
            var chosen = chance.Apply(outcome.Throw);
            var direct = chance.Sample(ScriptedSource.Faces(outcome.Throw.First, outcome.Throw.Second)).State;
            var afterTies = chance.Sample(ScriptedSource.Faces(4, 4, 1, 1, outcome.Throw.First, outcome.Throw.Second)).State;

            Assert.Equal(direct, chosen);
            Assert.Equal(afterTies, chosen);
            var expectedOpener = outcome.Throw.First > outcome.Throw.Second ? Player.White : Player.Black;
            Assert.Equal(expectedOpener, chosen.ToMove);
            Assert.Equal(new OutstandingOption(expectedOpener, outcome.Throw), chosen.Option);
        }
    }

    [Fact]
    public void An_outcome_a_die_cannot_show_or_an_opening_tie_is_refused_and_leaves_the_state_as_it_was()
    {
        // Mutation: CheckFaces accepted a face above the die's six (the upper bound removed); this test went red.
        var throwing = Assert.IsType<ChanceDecision>(
            TurnLoop.Next(TurnLoop.Begin(Corpus.StartingPosition, Player.White)));
        var opening = Assert.IsType<ChanceDecision>(TurnLoop.Next(TurnLoop.Begin(Corpus.StartingPosition)));
        var before = throwing.State;

        Assert.Throws<ArgumentOutOfRangeException>(() => throwing.Apply(new DiceThrow(7, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => throwing.Apply(new DiceThrow(1, 7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => throwing.Apply(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => opening.Apply(new DiceThrow(6, 7)));
        Assert.Throws<ArgumentException>(() => opening.Apply(new DiceThrow(4, 4)));

        Assert.Equal(before, throwing.State);
        Assert.Equal(TurnPhase.Play, throwing.Apply(new DiceThrow(6, 6)).Phase);
    }
}

/// <summary>Nothing the step surface does changes what it was given.</summary>
public class TurnLoopPurityTests
{
    /// <summary>
    /// Every state a seeded walk of one game passes through, taking the step surface's decisions with a seeded
    /// decider; each call walks afresh, so two calls with one seed give two independent copies of the same states.
    /// </summary>
    private static List<TurnState> Walk(ulong seed, AssertedPosition start, Player? opener = null)
    {
        var source = Pcg32.FromSeed(seed, stream: 1);
        var decider = new SeededDecider(seed);
        var states = new List<TurnState>();
        var state = TurnLoop.Begin(start, opener);
        OpeningRoll? roll = null;
        while (true)
        {
            states.Add(state);
            switch (TurnLoop.Next(state))
            {
                case ChanceDecision { Kind: ChanceKind.OpeningRoll } chance:
                    var rolled = chance.Sample(source);
                    roll = rolled.Roll;
                    state = rolled.State;
                    break;
                case ChanceDecision chance:
                    state = chance.Sample(source).State;
                    break;
                case AdoptionDecision adoption:
                    state = adoption.Apply(decider.AdoptOpeningThrow(roll!));
                    break;
                case PassDecision pass:
                    state = pass.Apply();
                    break;
                case PlayDecision choice:
                    state = choice.Apply(decider.ChoosePlay(choice.Player, choice.Plays));
                    break;
                default:
                    return states;
            }
        }
    }

    private static string Describe(Play play) => $"{play}|{play.Result}|{string.Join(",", play.Rulings.Select(r => r.ToString()))}";

    [Fact]
    public void Applying_every_action_from_a_sample_of_states_leaves_each_source_equal_to_a_fresh_copy()
    {
        // Mutation: AdoptionDecision.Apply spent the option in place on the state it was read from (TurnState.Option made
        // settable and cleared there once the next state was built); this test went red.
        var used = new List<TurnState>();
        var fresh = new List<TurnState>();
        var starts = new[] { Corpus.StartingPosition, Suspended.BlackAgainstSixPoints };
        foreach (var start in starts)
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                used.AddRange(Walk(seed, start));
                fresh.AddRange(Walk(seed, start));
            }
        }

        Assert.Equal(fresh.Count, used.Count);

        // The sample reaches every kind of decision.
        var kinds = used.Select(s => TurnLoop.Next(s).GetType()).ToHashSet();
        Assert.Contains(typeof(ChanceDecision), kinds);
        Assert.Contains(typeof(AdoptionDecision), kinds);
        Assert.Contains(typeof(PlayDecision), kinds);
        Assert.Contains(typeof(PassDecision), kinds);
        Assert.Contains(typeof(TerminalDecision), kinds);
        Assert.Contains(used, s => s.Phase == TurnPhase.OpeningRoll);

        int actions = 0;
        for (int i = 0; i < used.Count; i++)
        {
            var source = used[i];
            var hash = source.GetHashCode();
            var text = source.ToString();

            switch (TurnLoop.Next(source))
            {
                case PlayDecision choice:
                    var offered = choice.Plays.Select(Describe).ToArray();
                    for (int index = 0; index < choice.Plays.Length; index++)
                    {
                        // By index, and by value, and both again: the same next state each time.
                        var byIndex = choice.Apply(index);
                        Assert.Equal(byIndex, choice.Apply(choice.Plays[index]));
                        Assert.Equal(byIndex, choice.Apply(index));
                        actions++;
                    }

                    Assert.Equal(offered, choice.Plays.Select(Describe));
                    break;
                case AdoptionDecision adoption:
                    Assert.NotEqual(adoption.Apply(true), adoption.Apply(false));
                    actions += 2;
                    break;
                case ChanceDecision chance:
                    foreach (var outcome in chance.Outcomes)
                    {
                        _ = chance.Apply(outcome.Throw);
                        actions++;
                    }

                    _ = chance.Sample(Pcg32.FromSeed(9UL, stream: 1));
                    break;
                case PassDecision pass:
                    _ = pass.Apply();
                    actions++;
                    break;
                case TerminalDecision terminal:
                    _ = terminal.Result;
                    _ = terminal.Result;
                    break;
            }

            // Reading the decision again changes nothing either.
            _ = TurnLoop.Next(source);

            Assert.Equal(fresh[i], source);
            Assert.Equal(hash, source.GetHashCode());
            Assert.Equal(text, source.ToString());
            Assert.Equal(fresh[i].Position, source.Position);
        }

        Assert.True(actions > 1_000, $"only {actions} actions were applied.");
    }

    [Fact]
    public void The_plays_offered_are_LegalPlays_in_its_order_and_applying_one_never_edits_the_list()
    {
        // Mutation: the decision offered LegalPlays.For's plays in another order (sorted by their text, descending); this
        // test went red.
        int checkedStates = 0;
        foreach (var state in Walk(7UL, Corpus.StartingPosition))
        {
            if (TurnLoop.Next(state) is not PlayDecision choice)
            {
                continue;
            }

            var direct = Assert.IsType<Resolution<ImmutableArray<Play>>.Resolved>(
                LegalPlays.For(state.Position, choice.Player, Movement.Entitlement(state.PendingThrow!.Value))).Value;
            Assert.Equal(direct.Select(Describe), choice.Plays.Select(Describe));
            checkedStates++;
        }

        Assert.True(checkedStates > 20);
    }

    [Fact]
    public void Applying_a_play_the_decision_did_not_offer_is_refused_and_changes_nothing()
    {
        // Mutation: Apply(Play) accepted any play handed to it (`|| offered is not null` on the search's condition); this
        // test went red.
        var state = Walk(5UL, Corpus.StartingPosition).First(s => TurnLoop.Next(s) is PlayDecision);
        var choice = Assert.IsType<PlayDecision>(TurnLoop.Next(state));
        var other = Walk(6UL, Corpus.StartingPosition)
            .Select(s => TurnLoop.Next(s))
            .OfType<PlayDecision>()
            .SelectMany(d => d.Plays)
            .First(p => !choice.Plays.Any(q => q.Result.Equals(p.Result)));
        var forged = new Play(choice.Plays[0].Moves, other.Result);

        Assert.Throws<ArgumentException>(() => choice.Apply(other));
        Assert.Throws<ArgumentException>(() => choice.Apply(forged));
        Assert.Throws<ArgumentOutOfRangeException>(() => choice.Apply(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => choice.Apply(choice.Plays.Length));
        Assert.Throws<ArgumentNullException>(() => choice.Apply((Play)null!));
        Assert.Equal(Walk(5UL, Corpus.StartingPosition).First(s => TurnLoop.Next(s) is PlayDecision), state);
    }
}

/// <summary>The terminal case reports what <see cref="Outcome.ResultOf"/> gives.</summary>
public class TurnLoopTerminalTests
{
    [Fact]
    public void Every_game_driven_to_its_end_reports_the_result_Outcome_ResultOf_gives_and_the_one_Game_Play_records()
    {
        // Mutation: the terminal decision valued every win a hit (`new GameResult(winner, GameValue.Hit)` for
        // Outcome.ResultOf); this test went red on the first gammon.
        var values = new HashSet<GameValue>();
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var source = Pcg32.FromSeed(seed, stream: 1);
            var decider = new SeededDecider(seed);
            var state = TurnLoop.Begin(Corpus.StartingPosition);
            OpeningRoll? roll = null;
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
                        state = chance.Sample(source).State;
                        break;
                    case AdoptionDecision adoption:
                        state = adoption.Apply(decider.AdoptOpeningThrow(roll!));
                        break;
                    case PassDecision pass:
                        state = pass.Apply();
                        break;
                    case PlayDecision choice:
                        state = choice.Apply(decider.ChoosePlay(choice.Player, choice.Plays));
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException($"seed {seed}: the walk declined before it ended.");
                }
            }

            var terminal = Assert.IsType<TerminalDecision>(TurnLoop.Next(state));
            var result = Assert.IsType<Resolution<GameResult>.Resolved>(terminal.Result).Value;
            var expected = Assert.IsType<Resolution<GameResult>.Resolved>(
                Outcome.ResultOf(state.Position, Outcome.Winner(state.Position)!.Value)).Value;

            Assert.Equal(expected.Winner, result.Winner);
            Assert.Equal(expected.Value, result.Value);
            Assert.Equal(expected.Rulings.Select(r => r.Id), result.Rulings.Select(r => r.Id));
            Assert.Equal(expected.Winner, state.Winner);
            Assert.Null(state.ToMove);

            var record = Assert.IsType<Resolution<GameRecord>.Resolved>(
                Game.Play(Corpus.StartingPosition, Pcg32.FromSeed(seed, stream: 1), new SeededDecider(seed))).Value;
            Assert.Equal(result.Winner, record.Winner);
            Assert.Equal(result.Value, record.Value);
            Assert.Equal(result.Rulings.Select(r => r.Id), record.ValueRulings.Select(r => r.Id));
            values.Add(result.Value);
        }

        // Without this the test would pass against a decision that valued every win alike.
        Assert.Contains(GameValue.Gammon, values);
        Assert.Contains(GameValue.Hit, values);
    }

    [Fact]
    public void A_finished_game_is_over_for_good_and_a_state_one_play_from_the_end_is_not()
    {
        // Mutation: TurnLoop.Played looked for a winner in the position before the play instead of the one it reached;
        // this test went red.
        var oneOff = new AssertedPosition(
            Board.Of(
                Board.Men().At(1, 1).RestBorneOff(),
                Board.Men().At(1, 1).RestBorneOff()),
            AssertedBy: nameof(TurnLoopTerminalTests));
        var state = TurnLoop.Begin(oneOff, Player.White);
        Assert.False(state.IsOver);

        var thrown = Assert.IsType<ChanceDecision>(TurnLoop.Next(state)).Apply(new DiceThrow(2, 1));
        var play = Assert.IsType<PlayDecision>(TurnLoop.Next(thrown));
        var finished = play.Apply(0);

        Assert.True(finished.IsOver);
        Assert.Equal(TurnPhase.Over, finished.Phase);
        Assert.Equal(Player.White, finished.Winner);
        var terminal = Assert.IsType<TerminalDecision>(TurnLoop.Next(finished));
        var result = Assert.IsType<Resolution<GameResult>.Resolved>(terminal.Result).Value;
        Assert.Equal(Player.White, result.Winner);
        Assert.Equal(GameValue.Hit, result.Value);
        Assert.Equal(finished, Assert.IsType<TerminalDecision>(TurnLoop.Next(finished)).State);
    }
}

/// <summary>The forced pass, and the opener's option across it.</summary>
public class TurnLoopSuspensionTests
{
    [Fact]
    public void A_wholly_suspended_opener_passes_and_still_holds_his_option_until_he_throws()
    {
        // Mutation: TurnLoop.Pass dropped the outstanding option (`state.Option` replaced by null); this test went red
        // at the option's first assertion after the pass.
        var rolling = Assert.IsType<ChanceDecision>(TurnLoop.Next(TurnLoop.Begin(Suspended.BlackAgainstSixPoints)));
        var rolled = rolling.Apply(new DiceThrow(2, 5));
        var option = new OutstandingOption(Player.Black, new DiceThrow(2, 5));
        Assert.Equal(Player.Black, rolled.ToMove);
        Assert.Equal(option, rolled.Option);

        // Black cannot enter, so he does not throw and is not asked: his turn is forfeited.
        var pass = Assert.IsType<PassDecision>(TurnLoop.Next(rolled));
        Assert.Equal(Player.Black, pass.Player);
        var white = pass.Apply();
        Assert.Equal(Player.White, white.ToMove);
        Assert.Equal(option, white.Option);
        Assert.Equal(rolled.Position, white.Position);

        // White throws as any player does: the option is Black's, not his.
        var whiteThrows = Assert.IsType<ChanceDecision>(TurnLoop.Next(white));
        Assert.Equal(ChanceKind.Throw, whiteThrows.Kind);
        var sixes = whiteThrows.Apply(new DiceThrow(6, 6));
        Assert.Equal(option, sixes.Option);
        var whitePlays = Assert.IsType<PlayDecision>(TurnLoop.Next(sixes));
        Assert.Equal(Player.White, whitePlays.Player);

        // Four sixes bear four men off point six and break it, so Black can enter and is asked about his option.
        var black = whitePlays.Apply(0);
        Assert.Equal(Player.Black, black.ToMove);
        Assert.Equal(option, black.Option);
        var adoption = Assert.IsType<AdoptionDecision>(TurnLoop.Next(black));
        Assert.Equal(option, adoption.Option);

        // Adopting spends the option and holds the pair as the throw, higher die first; declining spends it and throws again.
        var adopted = adoption.Apply(true);
        Assert.Null(adopted.Option);
        Assert.Equal(TurnPhase.Play, adopted.Phase);
        Assert.Equal(new DiceThrow(5, 2), adopted.PendingThrow);
        Assert.Equal(Player.Black, Assert.IsType<PlayDecision>(TurnLoop.Next(adopted)).Player);

        var declined = adoption.Apply(false);
        Assert.Null(declined.Option);
        Assert.Null(declined.PendingThrow);
        Assert.Equal(ChanceKind.Throw, Assert.IsType<ChanceDecision>(TurnLoop.Next(declined)).Kind);
    }

    [Fact]
    public void A_pass_hands_the_turn_to_the_adversary_on_the_same_position()
    {
        // Mutation: TurnLoop.Pass left the side to move as it was; this test went red.
        var state = TurnLoop.Begin(Suspended.BlackAgainstSixPoints, Player.Black);
        var pass = Assert.IsType<PassDecision>(TurnLoop.Next(state));

        var next = pass.Apply();

        Assert.Equal(Player.White, next.ToMove);
        Assert.Equal(state.Position, next.Position);
        Assert.Equal(TurnPhase.Turn, next.Phase);
        Assert.Equal(Player.Black, state.ToMove);
    }
}

/// <summary>The state is a value.</summary>
public class TurnStateTests
{
    [Fact]
    public void Two_states_are_equal_when_the_game_stands_in_the_same_place_and_not_otherwise()
    {
        // Mutation: TurnState.Equals ignored the side to move; this test went red.
        var white = TurnLoop.Begin(Corpus.StartingPosition, Player.White);
        var black = TurnLoop.Begin(Corpus.StartingPosition, Player.Black);
        var again = TurnLoop.Begin(
            new AssertedPosition(Setup.StartingPositionFromCorpus(), AssertedBy: "somebody else"), Player.White);
        var opening = TurnLoop.Begin(Corpus.StartingPosition);

        Assert.Equal(white, again);
        Assert.Equal(white.GetHashCode(), again.GetHashCode());
        Assert.NotEqual(white, black);
        Assert.NotEqual(white, opening);
        Assert.False(white.Equals(null));
        Assert.False(white.Equals((object)"state"));

        // The same side to move, a different pending throw, a different option and a different position are different.
        var chance = Assert.IsType<ChanceDecision>(TurnLoop.Next(white));
        Assert.NotEqual(chance.Apply(new DiceThrow(6, 5)), chance.Apply(new DiceThrow(6, 4)));
        Assert.NotEqual(chance.Apply(new DiceThrow(6, 5)), white);
        var roll = Assert.IsType<ChanceDecision>(TurnLoop.Next(opening));
        Assert.NotEqual(roll.Apply(new DiceThrow(3, 2)), roll.Apply(new DiceThrow(2, 3)));
        Assert.NotEqual(roll.Apply(new DiceThrow(3, 2)), roll.Apply(new DiceThrow(4, 2)));

        var played = Assert.IsType<PlayDecision>(TurnLoop.Next(chance.Apply(new DiceThrow(6, 5)))).Apply(0);
        Assert.NotEqual(played.Position, white.Position);
        Assert.NotEqual(played, white);
    }

    [Fact]
    public void A_state_is_made_only_by_the_loop_so_every_state_a_caller_holds_satisfies_the_loops_invariants()
    {
        // Mutation: TurnLoop.Played left the pending throw in the state it returned for the next turn; this test went
        // red.
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var source = Pcg32.FromSeed(seed, stream: 1);
            var decider = new SeededDecider(seed);
            var state = TurnLoop.Begin(Corpus.StartingPosition);
            OpeningRoll? roll = null;
            for (int step = 0; step < 5_000 && !state.IsOver; step++)
            {
                switch (state.Phase)
                {
                    case TurnPhase.OpeningRoll:
                        Assert.Null(state.ToMove);
                        Assert.Null(state.Option);
                        Assert.Null(state.PendingThrow);
                        break;
                    case TurnPhase.Turn:
                        Assert.NotNull(state.ToMove);
                        Assert.Null(state.PendingThrow);
                        break;
                    case TurnPhase.Play:
                        Assert.NotNull(state.ToMove);
                        Assert.NotNull(state.PendingThrow);
                        Assert.True(state.PendingThrow.Value.First >= state.PendingThrow.Value.Second);
                        break;
                }

                Assert.Null(state.Winner);
                switch (TurnLoop.Next(state))
                {
                    case ChanceDecision { Kind: ChanceKind.OpeningRoll } chance:
                        var rolled = chance.Sample(source);
                        roll = rolled.Roll;
                        state = rolled.State;
                        break;
                    case ChanceDecision chance:
                        state = chance.Sample(source).State;
                        break;
                    case AdoptionDecision adoption:
                        state = adoption.Apply(decider.AdoptOpeningThrow(roll!));
                        break;
                    case PassDecision pass:
                        state = pass.Apply();
                        break;
                    case PlayDecision choice:
                        state = choice.Apply(decider.ChoosePlay(choice.Player, choice.Plays));
                        break;
                    default:
                        throw new Xunit.Sdk.XunitException("declined");
                }
            }

            Assert.True(state.IsOver);
            Assert.NotNull(state.Winner);
            Assert.Null(state.Option);
            Assert.Null(state.PendingThrow);
        }
    }

    [Fact]
    public void Next_needs_a_state()
    {
        // Mutation: Next dropped its null check (and, separately, Begin did), so a null reached a member access; this
        // test went red on the exception it threw instead.
        Assert.Equal("state", Assert.Throws<ArgumentNullException>(() => TurnLoop.Next(null!)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentNullException>(() => TurnLoop.Begin(null!)).ParamName);
    }
}
