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
            Position = position,
            Player = player,
            Thrown = thrown,
        });

    /// <summary>The compelled plays, after checking the answer carries back the assertion it was asked about.</summary>
    private static ImmutableArray<Play> Compelled(Resolution<object> resolution, AssertedPosition position)
    {
        var answer = Assert.IsType<AssertedAnswer<ImmutableArray<Play>>>(Assert.IsType<Resolution<object>.Resolved>(resolution).Value);
        Assert.Same(position, answer.Position);
        Assert.Equal(nameof(MustPlayWholeThrowEntryPointTests), answer.AssertedBy);
        return answer.Value;
    }

    [Fact]
    public void A_six_trois_with_a_man_up_resolves_to_the_plays_of_the_whole_throw_each_move_citing_its_entry()
    {
        // White has a man up and Black's fifteen stand on his own thirteen point (White's 12), so
        // either number enters and the other can always follow. Entering and stopping short is
        // not open to him: every compelled play enters and then plays the other number, and no
        // other play is offered. Entering with the trois and running that man on six reaches the
        // same position with the same numbers as bar/19 19/16, and the engine offers that play once:
        // both orders are an entry and a move-by-pip, so no rule differs between them, which is
        // not must-play-whole-throw's open question (map 6.0.0, rules-factory#125; docs/decisions/0008).
        var position = Asserted(
            Board.Men().At(Geometry.BarPip, 1).At(8, 6).RestAt(6),
            Board.Men().RestAt(13));

        var plays = Compelled(Resolve(position, Player.White, new DiceThrow(6, 3)), position);

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

        var play = Assert.Single(Compelled(Resolve(position, Player.White, new DiceThrow(2, 2)), position));

        Assert.Equal("8/6(2) 6/4(2) 4/2(2)", play.ToString());
        Assert.All(play.Moves, move => Assert.Equal(MapEntries.MoveByPip, move.Authority));
        Assert.Equal(1, play.Result.Men(Player.White, 2));
    }

    [Fact]
    public void Two_orders_reaching_the_same_position_under_different_rules_decline_citing_page_275()
    {
        // White's last man outside stands on the eight point; he throws deuce ace. 8/6 then 6/5 and
        // 8/7 then 7/5 leave the same men on the same points with the same numbers used, but not
        // under the same rules: after 8/6 every man is home, so the ace is played after bearing off
        // has begun, and in the other order the man is still outside, on the seven point, when the
        // deuce is played. Whether two such orders are one play or two is must-play-whole-throw's
        // question since RulesFactory.Maps.HoyleBackgammon 6.0.0 (rules-factory#125, fate
        // unresolved, the map's own example). Until map 6.0.0 the engine offered 8/6 6/5 once
        // (docs/decisions/0007); now it declines, citing the entry (docs/decisions/0008).
        var position = Asserted(Board.Men().At(8, 1).RestAt(6), Board.Men().RestAt(13));

        var unresolved = Assert.IsType<Resolution<object>.Unresolved>(
            Resolve(position, Player.White, new DiceThrow(2, 1))).Result;

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Reason);
        Assert.Equal("BACKGAMMON / Playing / p. 275", unresolved.Locator.Citation);
        Assert.Equal(EntryPoints.MustPlayWholeThrow.Registered.Locator, unresolved.Locator);

        // Both orders are open move by move, through the single-die entry point, and reach the same
        // position under different rules: 8/6 then 6/5 under bearing-off-move-or-remove, and 8/7 then
        // 7/5 under move-by-pip twice.
        var deuce = Assert.Single(MovesByPip(position, 2), m => m.From == 8);
        var homed = Then(position, deuce);
        var aceAfter = Assert.Single(MovesByPip(homed, 1), m => m.From == 6);
        Assert.Equal(MapEntries.BearingOffMoveOrRemove, aceAfter.Authority);

        var ace = Assert.Single(MovesByPip(position, 1), m => m.From == 8);
        var between = Then(position, ace);
        var deuceAfter = Assert.Single(MovesByPip(between, 2), m => m.From == 7);
        Assert.Equal([MapEntries.MoveByPip, MapEntries.MoveByPip], new[] { ace.Authority, deuceAfter.Authority });
        Assert.Equal(Then(homed, aceAfter).Position, Then(between, deuceAfter).Position);
    }

    [Fact]
    public void A_number_left_after_the_last_man_comes_home_declines_citing_bearing_off_eligible()
    {
        // bearing-off-eligible's example since RulesFactory.Maps.HoyleBackgammon 6.0.0
        // (rules-factory#125, fate unresolved): White's last man outside is on the nine point, the
        // other fourteen on his ace point, where no number moves them before bearing off begins, and
        // he throws six-trois. 9/3 then off with the trois, and 9/6 then off with the six, are legal
        // if the stage begins partway through the throw and neither is if it does not. Both orders
        // are an ordinary move and a removal, so this is not must-play-whole-throw's question; it is
        // only whether bearing off begins within the throw (docs/decisions/0008).
        var position = Asserted(Board.Men().At(9, 1).RestAt(1), Board.Men().RestAt(13));

        var unresolved = Assert.IsType<Resolution<object>.Unresolved>(
            Resolve(position, Player.White, new DiceThrow(6, 3))).Result;

        Assert.Equal(UnresolvedReason.RequiresInterpretation, unresolved.Reason);
        Assert.Equal("BACKGAMMON / Bearing off the Men / p. 275", unresolved.Locator.Citation);
        Assert.Equal(EntryPoints.BearingOffEligible.Registered.Locator, unresolved.Locator);

        // The decline is the throw's, not the position's: a throw whose last number brings the man
        // home leaves no number to ask about, and resolves (9/7 7/6, the one play of deuce-ace).
        var play = Assert.Single(Compelled(Resolve(position, Player.White, new DiceThrow(2, 1)), position));
        Assert.Equal("9/7(2) 7/6(1)", play.ToString());
        Assert.All(play.Moves, m => Assert.Equal(MapEntries.MoveByPip, m.Authority));
    }

    private static AssertedPosition Then(AssertedPosition position, Move move) =>
        new(position.Position.Apply(Player.White, move.From, move.To), AssertedBy: nameof(MustPlayWholeThrowEntryPointTests));

    private static ImmutableArray<Move> MovesByPip(AssertedPosition position, int die) =>
        Assert.IsType<AssertedAnswer<ImmutableArray<Move>>>(Assert.IsType<Resolution<object>.Resolved>(
            EntryPoints.MoveByPip.Resolve(new MoveByPipRequest { Position = position, Player = Player.White, Die = die })).Value).Value;

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
            Position = position,
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

        var answer = Assert.IsType<AssertedAnswer<GameValue>>(resolved.Value);
        Assert.Equal(expected, answer.Value);
        Assert.Equal(nameof(GameValueEntryPointTests), answer.AssertedBy);
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
/// Who asserted a position survives the typed surface. Every entry whose request takes a position takes
/// an <see cref="AssertedPosition"/> and answers with an <see cref="AssertedAnswer{T}"/> carrying that
/// same assertion back, as <see cref="Game.Play"/> carries its start into <see cref="GameRecord.Start"/>.
/// </summary>
public class AssertedPositionEntryPointTests
{
    private static readonly RulesKernel.Provenance.SourceLocator Cited = MapEntries.StartingPosition.Locator;

    /// <summary>White all home on his six, five and four points; Black's fifteen on his own thirteen.</summary>
    private static readonly AssertedPosition Home = new(
        Board.Of(Board.Men().At(6, 5).At(5, 5).RestAt(4), Board.Men().RestAt(13)),
        AssertedBy: "a study of bearing off",
        Justification: Cited);

    /// <summary>White has borne off all fifteen; Black has two off and the rest home, a hit.</summary>
    private static readonly AssertedPosition Won = new(
        Board.Of(Board.Men().RestBorneOff(), Board.Men().At(Geometry.BorneOffPip, 2).RestAt(3)),
        AssertedBy: "a finished game");

    public static TheoryData<string, Func<Resolution<object>>, AssertedPosition> Entries => new()
    {
        { "move-by-pip", () => EntryPoints.MoveByPip.Resolve(new() { Position = Home, Player = Player.Black, Die = 3 }), Home },
        { "legal-destination", () => EntryPoints.LegalDestination.Resolve(new() { Position = Home, Player = Player.White, Pip = 3 }), Home },
        { "made-point", () => EntryPoints.MadePoint.Resolve(new() { Position = Home, Player = Player.White, Pip = 6 }), Home },
        { "blot-hit", () => EntryPoints.BlotHit.Resolve(new() { Position = Home, Player = Player.White, From = 6, To = 3 }), Home },
        { "enter-from-bar", () => EntryPoints.EnterFromBar.Resolve(new() { Position = Home, Player = Player.White }), Home },
        { "full-table-suspension", () => EntryPoints.FullTableSuspension.Resolve(new() { Position = Home, Player = Player.White }), Home },
        { "must-play-whole-throw", () => EntryPoints.MustPlayWholeThrow.Resolve(new() { Position = Home, Player = Player.White, Thrown = new DiceThrow(6, 3) }), Home },
        { "bearing-off-eligible", () => EntryPoints.BearingOffEligible.Resolve(new() { Position = Home, Player = Player.White }), Home },
        { "bearing-off-move-or-remove", () => EntryPoints.BearingOffMoveOrRemove.Resolve(new() { Position = Home, Player = Player.White, Die = 5 }), Home },
        { "bearing-off-highest", () => EntryPoints.BearingOffHighest.Resolve(new() { Position = Home, Player = Player.White, Die = 6 }), Home },
        { "bearing-off-doublets", () => EntryPoints.BearingOffDoublets.Resolve(new() { Position = Home, Player = Player.White, Thrown = new DiceThrow(2, 2) }), Home },
        { "win-condition", () => EntryPoints.WinCondition.Resolve(new() { Position = Won, Player = Player.White }), Won },
        { "game-value", () => EntryPoints.GameValue.Resolve(new() { Position = Won, Winner = Player.White }), Won },
    };

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_entry_asked_about_an_asserted_position_answers_with_that_assertion(
        string entry, Func<Resolution<object>> resolve, AssertedPosition asserted)
    {
        var value = Assert.IsType<Resolution<object>.Resolved>(resolve()).Value;

        // The answer is an AssertedAnswer<T> of whatever the rule returns, and its assertion is the
        // caller's own object: who asserted it and what they cited, not a copy the engine made up.
        var type = value.GetType();
        Assert.True(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AssertedAnswer<>), $"{entry} answered a {type}");
        var position = Assert.IsType<AssertedPosition>(type.GetProperty(nameof(AssertedAnswer<object>.Position))!.GetValue(value));
        Assert.Same(asserted, position);
        Assert.Equal(asserted.AssertedBy, type.GetProperty(nameof(AssertedAnswer<object>.AssertedBy))!.GetValue(value));
        Assert.Equal(asserted.Justification, type.GetProperty(nameof(AssertedAnswer<object>.Justification))!.GetValue(value));
    }

    [Fact]
    public void Every_request_that_takes_a_position_takes_an_asserted_one_and_is_resolved_above()
    {
        // No request type still takes a bare Position, and the theory above covers every one that
        // takes a position at all, so a new entry cannot drop the attribution unnoticed.
        var positional = typeof(IEntryRequest).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == "HoyleBackgammon.Requests" && typeof(IEntryRequest).IsAssignableFrom(t))
            .Select(t => (Type: t, Property: t.GetProperty("Position")))
            .Where(r => r.Property is not null)
            .ToList();

        Assert.All(positional, r => Assert.Equal(typeof(AssertedPosition), r.Property!.PropertyType));
        Assert.Equal(
            Entries.Select(row => (string)row[0]).Order(StringComparer.Ordinal),
            positional.Select(r => ((IEntryRequest)Activator.CreateInstance(r.Type)!).EntryId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_request_without_a_position_is_the_callers_error_not_an_answer()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            EntryPoints.GameValue.Resolve(new() { Winner = Player.White }));

        Assert.Equal(nameof(GameValueRequest.Position), error.ParamName);
    }
}

/// <summary>
/// One whole game through the public surface: the corpus's starting position asserted as a caller
/// asserts it, <see cref="Game.Play"/> with a seeded <see cref="Pcg32"/>, the decisions recorded, and
/// the game replayed from the same seed and those decisions.
/// </summary>
public class SeededGameReplayTests
{
    private const ulong Seed = 20260965UL;

    /// <summary>
    /// The SHA-256 of the recorded game's canonical serialisation, <see cref="GameRecord.ToCanonicalJson"/>.
    /// A literal, as <see cref="IdentityTests"/> keeps its literals: if this changes, the engine plays this
    /// seed and these decisions differently or writes them differently, which is a decision about the
    /// ruleset version or the replay schema and not a number to update until green.
    /// </summary>
    /// <remarks>
    /// Re-pinned from <c>606eb924...e8500b9a</c> when the record gained its own serialisation (replay
    /// schema 2, <c>docs/decisions/0006</c>). The game did not change: the test's former rendering, run
    /// over the new record with the schema read as 1, still hashes to the old literal, 65 turns and a
    /// gammon for White. Only the bytes did, so the ruleset stays at version 3.
    /// <para>
    /// Re-pinned again, with a new seed, for ruleset version 4 (map 6.0.0, rules-factory#125,
    /// <c>docs/decisions/0008</c>). Seed 20260914 no longer finishes: its game reaches a throw that brings
    /// White's last man home with a number left, which bearing-off-eligible now leaves open, and declines.
    /// Seed 20260965 is the first from 20260900 whose game under these decisions finishes: 72 turns and a
    /// hit for Black. Previous pin <c>878936f1...0604da3d</c>.
    /// </para>
    /// </remarks>
    private const string RecordedReplaySha256 = "5b2745a7bf0d50781db73bc169c9248da41f3ea51c768acd6dc5592a3d45af8b";

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
        byte[] recorded = first.ToCanonicalJson();

        // The replay knows nothing but the seed and the recorded decisions.
        var script = new ScriptedDecider(recorder.Decisions);
        var replaySource = new CountingSource(Pcg32.FromSeed(Seed, stream: 1));
        var replay = Assert.IsType<Resolution<GameRecord>.Resolved>(Game.Play(start, replaySource, script)).Value;

        Assert.Equal(recorded, replay.ToCanonicalJson());
        Assert.Equal(recorder.Decisions.Count, script.Consumed);
        Assert.Equal(firstSource.Drawn, replaySource.Drawn);
        Assert.Equal(RecordedReplaySha256, Convert.ToHexString(SHA256.HashData(recorded)).ToLowerInvariant());

        // Two runs are comparable only under the same identity, and the record carries it: ruleset
        // hoyle-1909-backgammon version 4 (map 6.0.0, rules-factory#125), replay schema 2, from map 6.0.0. The map is the one the
        // embedded provenance names, not a constant of the engine's.
        Assert.Equal(Game.Identity, first.Identity);
        Assert.Equal("hoyle-1909-backgammon", first.Identity.Ruleset.Id);
        Assert.Equal(4, first.Identity.Ruleset.Version);
        Assert.Equal(2, first.Identity.ReplaySchema.Version);
        Assert.Equal(new MapPackage("RulesFactory.Maps.HoyleBackgammon", "6.0.0"), first.Map);
        using var provenance = JsonDocument.Parse(EngineProvenance.ReadBytes());
        var map = provenance.RootElement.GetProperty("map");
        Assert.Equal(map.GetProperty("packageId").GetString(), first.Map.PackageId);
        Assert.Equal(map.GetProperty("version").GetString(), first.Map.Version);
        Assert.StartsWith(
            "{\"identity\":{\"randomAlgorithm\":\"pcg_setseq_64_xsh_rr_32\",\"replaySchema\":2,\"ruleset\":{\"id\":\"hoyle-1909-backgammon\",\"version\":4},",
            Encoding.UTF8.GetString(recorded),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"map\":{\"packageId\":\"RulesFactory.Maps.HoyleBackgammon\",\"version\":\"6.0.0\"}",
            Encoding.UTF8.GetString(recorded),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_canonical_serialisation_is_rfc_8785_json_of_every_field_of_the_record()
    {
        // A record built by hand, so every field and every escape is on the page: members sorted by
        // name, no whitespace, a suspended turn (no throw, moves null) beside a turn with nothing
        // playable (moves empty) and one with a hit, the justification null, and an asserter whose
        // name needs a quote, a backslash, a newline, a unit separator and a non-ASCII letter.
        var position = Setup.StartingPositionFromCorpus();
        var record = new GameRecord(
            Game.Identity,
            new MapPackage("Some.Map", "1.2.3"),
            new AssertedPosition(position, AssertedBy: "Ann \"A\\B\"\n\u001fé"),
            new OpeningRoll([new DiceThrow(2, 2), new DiceThrow(5, 3)], Player.White),
            OpeningThrowAdopted: true,
            [
                new Turn(Player.Black, null, null, position),
                new Turn(Player.White, new DiceThrow(6, 6), new Play([], position), position),
                new Turn(Player.Black, new DiceThrow(5, 3), new Play([new Move(8, 3, 5, MoveKind.Ordinary, TakesUpBlot: true)], position), position),
            ],
            Player.White,
            GameValue.Gammon,
            NextOpening.ThrowAgainForTheRight);

        string men = "[0,0,0,0,0,0,5,0,3,0,0,0,0,5,0,0,0,0,0,0,0,0,0,0,2,0]";
        string board = $"{{\"Black\":{men},\"White\":{men}}}";
        string expected =
            "{\"identity\":{\"randomAlgorithm\":\"pcg_setseq_64_xsh_rr_32\",\"replaySchema\":2,"
            + "\"ruleset\":{\"id\":\"hoyle-1909-backgammon\",\"version\":4},"
            + "\"sourceBaselines\":[{\"asOf\":null,\"contentHash\":\"5d505fa9f6202340eb55313b8ef607b816087a860d3d51b1bf92b5f65240645e\","
            + "\"hashDerivation\":\"gutenberg-plain-text-including-boilerplate\",\"sourceId\":\"hoyle-1909\"}]},"
            + "\"map\":{\"packageId\":\"Some.Map\",\"version\":\"1.2.3\"},"
            + "\"next\":\"ThrowAgainForTheRight\","
            + "\"openingRoll\":{\"attempts\":[[2,2],[5,3]],\"opener\":\"White\"},"
            + "\"openingThrowAdopted\":true,"
            + $"\"start\":{{\"assertedBy\":\"Ann \\\"A\\\\B\\\"\\n\\u001fé\",\"justification\":null,\"position\":{board}}},"
            + "\"turns\":["
            + $"{{\"moves\":null,\"player\":\"Black\",\"position\":{board},\"thrown\":null}},"
            + $"{{\"moves\":[],\"player\":\"White\",\"position\":{board},\"thrown\":[6,6]}},"
            + "{\"moves\":[{\"authority\":\"move-by-pip\",\"die\":5,\"from\":8,\"kind\":\"Ordinary\",\"takesUpBlot\":true,\"to\":3}],"
            + $"\"player\":\"Black\",\"position\":{board},\"thrown\":[5,3]}}"
            + "],"
            + "\"value\":\"Gammon\",\"winner\":\"White\"}";

        byte[] bytes = record.ToCanonicalJson();

        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes);
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
