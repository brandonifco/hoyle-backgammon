using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// "Backgammon is played by two persons."
/// </summary>
public class PlayerCountTests
{
    [Fact]
    public void A_game_is_played_by_exactly_two_persons_turn_and_turn_about()
    {
        // The type admits exactly two players, each the other's adversary.
        var players = Enum.GetValues<Player>();
        Assert.Equal(2, players.Length);
        Assert.Equal(players, Players.Both);
        foreach (var player in players)
        {
            Assert.NotEqual(player, player.Adversary());
            Assert.Equal(player, player.Adversary().Adversary());
        }

        // And a game played through is played by both of them, alternately: no third person
        // takes a turn, neither sits the game out, and no one moves twice running. Seed 20260913, back
        // from 20260905 under ruleset version 5, which finishes the game version 4 declined (docs/decisions/0009).
        var record = Assert.IsType<Resolution<GameRecord>.Resolved>(
            Game.Play(Corpus.StartingPosition, Pcg32.FromSeed(DeterminismTests.FinishingSeed, 1UL), new FirstOptionDecider())).Value;

        Assert.Equal(
            players.OrderBy(p => p),
            record.Turns.Select(t => t.Player).Distinct().OrderBy(p => p));
        for (int i = 1; i < record.Turns.Length; i++)
        {
            Assert.NotEqual(record.Turns[i - 1].Player, record.Turns[i].Player);
        }
    }
}
