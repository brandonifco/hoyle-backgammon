using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

public class DeterminismTests
{
    private static GameRecord PlayOut(ulong seed, IDecider decider, out int drawn)
    {
        var counting = new CountingSource(Pcg32.FromSeed(seed, stream: 1));
        var result = Game.Play(Corpus.StartingPosition, counting, decider);
        drawn = counting.Drawn;
        return Assert.IsType<Resolution<GameRecord>.Resolved>(result).Value;
    }

    [Fact]
    public void The_same_seed_and_the_same_decisions_give_the_same_game()
    {
        var first = PlayOut(20260913UL, new FirstOptionDecider(), out int firstDraws);
        var second = PlayOut(20260913UL, new FirstOptionDecider(), out int secondDraws);

        Assert.Equal(firstDraws, secondDraws);
        Assert.Equal(first.Turns.Length, second.Turns.Length);
        Assert.Equal(first.Winner, second.Winner);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(
            first.Turns.Select(t => $"{t.Player} {t.Thrown} {t.Play}"),
            second.Turns.Select(t => $"{t.Player} {t.Thrown} {t.Play}"));
    }

    [Fact]
    public void A_different_seed_gives_a_different_game()
    {
        // Without this the test above would pass against an engine that ignored the generator.
        var first = PlayOut(20260913UL, new FirstOptionDecider(), out _);
        var other = PlayOut(1UL, new FirstOptionDecider(), out _);

        Assert.NotEqual(
            first.Turns.Select(t => $"{t.Player} {t.Thrown} {t.Play}"),
            other.Turns.Select(t => $"{t.Player} {t.Thrown} {t.Play}"));
    }

    [Fact]
    public void Every_draw_is_accounted_for_by_a_throw_of_two_dice()
    {
        // A suspended player does not throw, and a re-thrown opening costs a pair. Two draws
        // per pair thrown and not one draw more: if any rule quietly consulted the generator,
        // or if a suspended turn threw, this would not balance.
        var record = PlayOut(20260913UL, new FirstOptionDecider(), out int drawn);

        // Every turn that threw cost a pair, except an adopted opening throw, which re-used
        // the deciding pair and cost nothing.
        int pairs = record.OpeningRoll!.Attempts.Length
            + record.Turns.Count(t => t.Thrown is not null)
            - (record.OpeningThrowAdopted ? 1 : 0);

        Assert.Equal(2 * pairs, drawn);
    }

    [Fact]
    public void The_thirty_men_are_conserved_through_every_turn()
    {
        var record = PlayOut(20260913UL, new FirstOptionDecider(), out _);

        foreach (var turn in record.Turns)
        {
            foreach (var player in Players.Both)
            {
                int total = 0;
                for (int pip = 0; pip <= Geometry.BarPip; pip++)
                {
                    total += turn.Position.Men(player, pip);
                }

                Assert.Equal(Position.MenPerPlayer, total);
            }
        }
    }

    [Fact]
    public void A_finished_game_ends_with_the_winner_bearing_off_his_last_man()
    {
        var record = PlayOut(20260913UL, new FirstOptionDecider(), out _);
        var last = record.Turns[^1];

        Assert.Equal(record.Winner, last.Player);
        Assert.Equal(Position.MenPerPlayer, last.Position.BorneOff(record.Winner));
        Assert.True(last.Play!.Moves[^1].BearsOff);
    }

    [Fact]
    public void The_recorded_game_replays_move_for_move_from_its_start_and_its_moves()
    {
        // The record is the evidence, so it has to be independently checkable: replaying the
        // moves against Position.Apply must reproduce every position in it.
        var record = PlayOut(20260913UL, new FirstOptionDecider(), out _);
        var position = record.Start.Position;

        foreach (var turn in record.Turns)
        {
            foreach (var move in turn.Play?.Moves ?? [])
            {
                position = position.Apply(turn.Player, move.From, move.To);
            }

            Assert.Equal(turn.Position, position);
        }
    }

    [Fact]
    public void The_starting_position_travels_with_the_result_and_names_who_asserted_it()
    {
        var record = PlayOut(20260913UL, new FirstOptionDecider(), out _);

        Assert.Equal(Corpus.StartingPosition.AssertedBy, record.Start.AssertedBy);
        Assert.NotNull(record.Start.Justification);
    }

    [Fact]
    public void Scripted_decisions_are_consumed_in_order_and_running_out_is_an_error()
    {
        var decider = new ScriptedDecider([1, 0, 0]);

        var error = Assert.Throws<InvalidOperationException>(
            () => Game.Play(Corpus.StartingPosition, Pcg32.FromSeed(7UL, 1UL), decider));

        Assert.Contains("were scripted", error.Message, StringComparison.Ordinal);
        Assert.Equal(3, decider.Consumed);
    }

    [Fact]
    public void The_winner_of_a_hit_opens_the_next_game_without_a_roll_for_the_right()
    {
        // next-game-opening in the engine: an opener supplied by the caller means the roll for
        // the right does not decide, and the option to throw again does not arise.
        // Black has one man left on his ace point; whatever he throws bears it off and ends
        // the game on the first turn, so the whole of the generator's use here is the turn.
        var start = new AssertedPosition(
            Board.Of(Board.Men().RestAt(1), Board.Men().At(1, 1).RestBorneOff()),
            AssertedBy: "DeterminismTests");
        var source = new CountingSource(Pcg32.FromSeed(99UL, 1UL));

        var record = Assert.IsType<Resolution<GameRecord>.Resolved>(
            Game.Play(start, source, new FirstOptionDecider(), opener: Player.Black)).Value;

        Assert.Equal(Player.Black, Assert.Single(record.Turns).Player);
        Assert.Null(record.OpeningRoll);
        Assert.False(record.OpeningThrowAdopted);
        Assert.Equal(Player.Black, record.Winner);

        // Two draws: one pair for his own throw, and none at all for a roll for the right
        // that the previous game's hit made unnecessary.
        Assert.Equal(2, source.Drawn);
    }

    [Fact]
    public void Two_players_suspended_against_each_other_is_an_interaction_no_entry_covers()
    {
        // Each has a man up and twelve men making all six points of his own home table, so
        // neither can ever enter and neither can ever move. Fifteen men a side permits it; the
        // corpus only ever describes one player suspended while "the adversary continues to
        // throw and move".
        var deadlock = new AssertedPosition(
            Board.Of(
                Board.Men().At(Geometry.BarPip, 1).At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2)
                    .At(6, 2).RestAt(8),
                Board.Men().At(Geometry.BarPip, 1).At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2)
                    .At(6, 2).RestAt(8)),
            AssertedBy: "DeterminismTests");

        var result = Game.Play(deadlock, Pcg32.FromSeed(3UL, 1UL), new FirstOptionDecider());

        var unresolved = Assert.IsType<Resolution<GameRecord>.Unresolved>(result);
        Assert.Equal(UnresolvedReason.UnsupportedInteraction, unresolved.Result.Reason);
        Assert.Equal(MapEntries.FullTableSuspension.Locator, unresolved.Result.Locator);
    }
}
