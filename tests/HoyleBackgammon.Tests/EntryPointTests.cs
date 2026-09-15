using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HoyleBackgammon.Requests;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// <c>must-play-whole-throw</c> resolved end to end through the generated typed surface: a caller
/// asserts a position, builds the entry's request with a player and a throw, and resolves it through
/// <see cref="EntryPoints.MustPlayWholeThrow"/>. Nothing here calls <see cref="LegalPlays"/> or any
/// other rule directly (rules-factory#3, criterion 2).
/// </summary>
public class MustPlayWholeThrowEntryPointTests
{
    private static AssertedPosition Asserted(Side white, Side black) =>
        new(Board.Of(white, black), AssertedBy: nameof(MustPlayWholeThrowEntryPointTests));

    private static Resolution<object> Resolve(AssertedPosition position, Player player, DiceThrow thrown) =>
        EntryPoints.MustPlayWholeThrow.Resolve(new MustPlayWholeThrowRequest
        {
            Position = position.Position,
            Player = player,
            Thrown = thrown,
        });

    private static ImmutableArray<Play> Compelled(Resolution<object> resolution) =>
        Assert.IsType<ImmutableArray<Play>>(Assert.IsType<Resolution<object>.Resolved>(resolution).Value);

    [Fact]
    public void A_six_trois_with_a_man_up_resolves_to_the_plays_of_the_whole_throw_each_move_citing_its_entry()
    {
        // White has a man up and Black's fifteen stand on his own thirteen point (White's 12), so
        // either number enters and the other can always follow. Entering and stopping short is
        // not open to him: every compelled play enters and then plays the other number, and no
        // other play is offered. Entering with the trois and running that man on six reaches the
        // same position with the same numbers as bar/19 19/16, and the engine offers that play once.
        var position = Asserted(
            Board.Men().At(Geometry.BarPip, 1).At(8, 6).RestAt(6),
            Board.Men().RestAt(13));

        var plays = Compelled(Resolve(position, Player.White, new DiceThrow(6, 3)));

        Assert.Equal(
            new[]
            {
                "bar/19(6) 19/16(3)", "bar/19(6) 8/5(3)", "bar/19(6) 6/3(3)", "bar/22(3) 8/2(6)",
            }.Order(StringComparer.Ordinal),
            plays.Select(p => p.ToString()).Order(StringComparer.Ordinal));
        Assert.All(plays, play =>
        {
            Assert.Equal(9, play.PipsUsed);
            Assert.Equal(MapEntries.EnterFromBar, play.Moves[0].Authority);
            Assert.Equal(MapEntries.MoveByPip, play.Moves[1].Authority);
        });
    }

    [Fact]
    public void Doublets_resolve_to_the_one_play_that_goes_as_far_as_the_throw_can_each_move_citing_its_entry()
    {
        // One mobile man on the eight point walks down by deuces, 8, 6, 4, 2, and the fourth deuce
        // would take him off, which he may not do: his other man is stuck on the far ace point.
        // Three of the four numbers, and nothing less, is the one compelled play.
        var position = Asserted(
            Board.Men().At(24, 1).At(8, 1).RestAt(1),
            Board.Men().At(3, 2).RestAt(12));

        var play = Assert.Single(Compelled(Resolve(position, Player.White, new DiceThrow(2, 2))));

        Assert.Equal("8/6(2) 6/4(2) 4/2(2)", play.ToString());
        Assert.All(play.Moves, move => Assert.Equal(MapEntries.MoveByPip, move.Authority));
        Assert.Equal(1, play.Result.Men(Player.White, 2));
    }

    [Fact]
    public void Either_die_alone_playable_but_not_both_declines_citing_page_275()
    {
        // Playing the six leaves the man on the deuce point, where a trois cannot be played;
        // playing the trois leaves him on the cinque, where a six cannot. The corpus gives no rule
        // for choosing, and the entry point says so with the entry's own citation.
        var position = Asserted(
            Board.Men().At(24, 1).At(8, 1).RestAt(1),
            Board.Men().At(7, 2).At(4, 2).RestAt(12));

        var unresolved = Assert.IsType<Resolution<object>.Unresolved>(
            Resolve(position, Player.White, new DiceThrow(6, 3))).Result;

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Reason);
        Assert.Equal("hoyle-1909", unresolved.Locator.SourceId);
        Assert.Equal("BACKGAMMON / Playing / p. 275", unresolved.Locator.Citation);
        Assert.Equal(unresolved.Locator, Assert.Single(Registry.Citations(EntryPoints.MustPlayWholeThrow.Id)));
        Assert.Equal(unresolved.Locator, EntryPoints.MustPlayWholeThrow.Registered.Locator);
    }
}

/// <summary>
/// <c>game-value</c> resolved end to end through <see cref="EntryPoints.GameValue"/> from finished
/// positions a caller asserts: the three named results, and the two overlaps the map leaves
/// unresolved. Nothing here calls <see cref="Outcome"/> directly.
/// </summary>
public class GameValueEntryPointTests
{
    /// <summary>White has borne off all fifteen; Black stands as <paramref name="black"/> says.</summary>
    private static Resolution<object> WhiteHasWon(Side black)
    {
        var position = new AssertedPosition(
            Board.Of(Board.Men().RestBorneOff(), black),
            AssertedBy: nameof(GameValueEntryPointTests));

        return EntryPoints.GameValue.Resolve(new GameValueRequest
        {
            Position = position.Position,
            Winner = Player.White,
        });
    }

    public static TheoryData<string, GameValue> Finished => new()
    {
        // All Black's men home and two borne off: "has begun to bear off" -- a hit.
        { "off=2 4=6 3=7", GameValue.Hit },

        // Nothing borne off, nothing up, nothing in White's home table -- a gammon.
        { "13=15", GameValue.Gammon },

        // A man up, and two borne off so that it is not also a gammon -- a backgammon.
        { "bar=1 off=2 3=12", GameValue.Backgammon },
    };

    [Theory]
    [MemberData(nameof(Finished))]
    public void A_finished_game_resolves_to_a_hit_a_gammon_or_a_backgammon(string black, GameValue expected)
    {
        var resolved = Assert.IsType<Resolution<object>.Resolved>(WhiteHasWon(Parse(black)));

        Assert.Equal(expected, Assert.IsType<GameValue>(resolved.Value));
    }

    [Theory]
    [InlineData("bar=1 13=14")]
    [InlineData("19=1 13=14")]
    public void Nothing_borne_off_with_a_man_up_or_in_the_winners_home_table_declines_citing_the_entry(string black)
    {
        // Nothing borne off answers the gammon condition; a man up, or a man on White's home table
        // (Black's 19 to 24), answers the backgammon condition. game-value's question names both
        // (map 3.0.0 row 52; map 4.0.0, finding 17), and the corpus does not say which he suffers.
        var unresolved = Assert.IsType<Resolution<object>.Unresolved>(WhiteHasWon(Parse(black))).Result;

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Reason);
        Assert.Equal("hoyle-1909", unresolved.Locator.SourceId);
        Assert.Equal("BACKGAMMON / Bearing off the Men / p. 276", unresolved.Locator.Citation);
        Assert.Equal(unresolved.Locator, Assert.Single(Registry.Citations(EntryPoints.GameValue.Id)));
        Assert.Contains("gammon condition and the backgammon condition", unresolved.Attempted, StringComparison.Ordinal);
    }

    private static Side Parse(string men)
    {
        var side = Board.Men();
        foreach (var slot in men.Split(' '))
        {
            var (where, count) = (slot.Split('=')[0], int.Parse(slot.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture));
            int pip = where switch
            {
                "bar" => Geometry.BarPip,
                "off" => Geometry.BorneOffPip,
                _ => int.Parse(where, System.Globalization.CultureInfo.InvariantCulture),
            };
            side.At(pip, count);
        }

        return side;
    }
}

/// <summary>
/// One whole game through the public surface: the corpus's starting position asserted as a caller
/// asserts it, <see cref="Game.Play"/> with a seeded <see cref="Pcg32"/>, the decisions recorded, and
/// the game replayed from the same seed and those decisions.
/// </summary>
public class SeededGameReplayTests
{
    private const ulong Seed = 20260914UL;

    /// <summary>
    /// The SHA-256 of the recorded game's rendering. A literal, as <see cref="IdentityTests"/> keeps
    /// its literals: if this changes, the engine plays this seed and these decisions differently,
    /// which is a decision about the ruleset version and not a number to update until green.
    /// </summary>
    private const string RecordedReplaySha256 = "606eb92458af7754b365c76b3d3eba436cd919a2c7db4d1c593b9a65e8500b9a";

    [Fact]
    public void A_seeded_game_replays_byte_for_byte_from_its_seed_and_its_recorded_decisions()
    {
        var start = new AssertedPosition(
            Setup.StartingPositionFromCorpus(),
            AssertedBy: nameof(SeededGameReplayTests),
            Justification: MapEntries.StartingPosition.Locator);

        // The first run decides by a fixed but non-trivial policy and records every decision.
        var recorder = new RecordingDecider();
        var firstSource = new CountingSource(Pcg32.FromSeed(Seed, stream: 1));
        var first = Assert.IsType<Resolution<GameRecord>.Resolved>(Game.Play(start, firstSource, recorder)).Value;
        byte[] recorded = Render(first);

        // The replay knows nothing but the seed and the recorded decisions.
        var script = new ScriptedDecider(recorder.Decisions);
        var replaySource = new CountingSource(Pcg32.FromSeed(Seed, stream: 1));
        var replay = Assert.IsType<Resolution<GameRecord>.Resolved>(Game.Play(start, replaySource, script)).Value;

        Assert.Equal(recorded, Render(replay));
        Assert.Equal(recorder.Decisions.Count, script.Consumed);
        Assert.Equal(firstSource.Drawn, replaySource.Drawn);
        Assert.Equal(RecordedReplaySha256, Convert.ToHexString(SHA256.HashData(recorded)).ToLowerInvariant());

        // Two runs are comparable only under the same identity: ruleset hoyle-1909-backgammon
        // version 3, from map 5.0.0. The identity does not carry the map version; the embedded
        // provenance does. Map 5.0.0 changed only that line of the rendering: with it read as
        // 4.0.0, the game renders to the hash pinned under map 4.0.0, 7d26993f...a947cc4.
        Assert.Equal("hoyle-1909-backgammon", Game.Identity.Ruleset.Id);
        Assert.Equal(3, Game.Identity.Ruleset.Version);
        using var provenance = JsonDocument.Parse(EngineProvenance.ReadBytes());
        var map = provenance.RootElement.GetProperty("map");
        Assert.Equal("RulesFactory.Maps.HoyleBackgammon", map.GetProperty("packageId").GetString());
        Assert.Equal("5.0.0", map.GetProperty("version").GetString());
        Assert.StartsWith(
            "identity hoyle-1909-backgammon v3 schema 1 map RulesFactory.Maps.HoyleBackgammon 5.0.0\n",
            Encoding.UTF8.GetString(recorded),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The game as bytes: the replay identity and the map it was produced from, then every field of
    /// the record, in order. <see cref="GameRecord"/> has no serialisation of its own, so this is
    /// the test's, built only from public members.
    /// </summary>
    private static byte[] Render(GameRecord record)
    {
        using var provenance = JsonDocument.Parse(EngineProvenance.ReadBytes());
        var map = provenance.RootElement.GetProperty("map");
        var identity = Game.Identity;
        var text = new StringBuilder();
        text.Append($"identity {identity.Ruleset.Id} v{identity.Ruleset.Version} schema {identity.ReplaySchema.Version} ")
            .Append($"map {map.GetProperty("packageId").GetString()} {map.GetProperty("version").GetString()}\n");
        foreach (var baseline in identity.SourceBaselines)
        {
            text.Append($"baseline {baseline.SourceId} {baseline.ContentHash} {baseline.HashDerivation}\n");
        }

        text.Append($"generator {identity.RandomAlgorithm?.Name}\n")
            .Append($"start {record.Start.Position} asserted-by {record.Start.AssertedBy} justified-by {record.Start.Justification}\n")
            .Append($"opening {string.Join(" ", record.OpeningRoll?.Attempts.Select(a => a.ToString()) ?? [])} ")
            .Append($"opener {record.OpeningRoll?.Opener} adopted {record.OpeningThrowAdopted}\n");
        foreach (var turn in record.Turns)
        {
            text.Append($"turn {turn.Player} {turn.Thrown?.ToString() ?? "suspended"} {turn.Play?.ToString() ?? "-"} => {turn.Position}\n");
        }

        text.Append($"winner {record.Winner} value {record.Value} next {record.Next}\n");
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>Adopts the opening throw and takes the middle play offered, recording each decision.</summary>
    private sealed class RecordingDecider : IDecider
    {
        private readonly List<int> _decisions = [];

        public IReadOnlyList<int> Decisions => _decisions;

        public bool AdoptOpeningThrow(OpeningRoll roll)
        {
            _decisions.Add(1);
            return true;
        }

        public int ChoosePlay(Player player, ImmutableArray<Play> plays)
        {
            int index = plays.Length / 2;
            _decisions.Add(index);
            return index;
        }
    }
}
