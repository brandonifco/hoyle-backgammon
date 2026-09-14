using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// <c>starting-position</c>, now that the engine derives it rather than declining it.
/// </summary>
/// <remarks>
/// These read the arrangement back through <see cref="Geometry.NameOf"/> — that is, through
/// <see cref="MapEntries.PointDesignations"/>, the corpus's own vocabulary — and compare the
/// result with the prose of <see cref="MapEntries.StartingPosition"/> word for word. Nothing
/// here restates the four pip numbers, because a test that restated them would only be
/// checking that two literals were typed alike; the numbers are a reading of the corpus, so
/// what has to be checked is the reading.
/// </remarks>
public class StartingPositionTests
{
    [Fact]
    public void The_four_points_are_the_four_the_corpus_names()
    {
        // "two of White's men are placed on the ace point in Black's inner table, five are
        // placed on the six point in Black's outer table, three on the deuce point in White's
        // outer table, and five on the six point in White's inner table."
        //
        // Written from White's side, "Black's" is "the adversary's" and "White's" is "his".
        string[] prose =
        [
            "2 on the ace point in the adversary's inner table",
            "5 on the six point in the adversary's outer table",
            "3 on the deuce point in his outer table",
            "5 on the six point in his inner table",
        ];

        var start = Setup.StartingPositionFromCorpus();
        var placed = start.OccupiedPoints(Player.White)
            .Select(pip => $"{start.Men(Player.White, pip)} on {Geometry.NameOf(pip)}");

        // OccupiedPoints reads highest pip first, which is furthest-from-home first, which is
        // the order the sentence names them in. Ordered by construction, and asserted as such.
        Assert.Equal(prose, placed);
    }

    [Fact]
    public void Blacks_men_are_placed_on_the_points_immediately_facing_Whites()
    {
        var start = Setup.StartingPositionFromCorpus();

        // "In like manner on the points immediately facing these" is a statement in Black's
        // own vocabulary, not a mirror of White's pips: the point Black calls his ace point in
        // White's inner table faces the point White calls the same, and they are two different
        // points of the board. So Black's arrangement reads identically to White's, named
        // through Black's own designations -- and the two never meet, which
        // Position.Create refuses to let them do.
        foreach (var player in Players.Both)
        {
            Assert.Equal(
                start.OccupiedPoints(Player.White).Select(Geometry.NameOf),
                start.OccupiedPoints(player).Select(Geometry.NameOf));
            Assert.Equal(
                start.OccupiedPoints(Player.White).Select(p => start.Men(Player.White, p)),
                start.OccupiedPoints(player).Select(p => start.Men(player, p)));
        }

        // And "facing" is the mirror: no point White has men on is a point Black has men on.
        foreach (int pip in start.OccupiedPoints(Player.White))
        {
            Assert.Equal(0, start.AdversaryMen(Player.White, pip));
        }
    }

    [Fact]
    public void Nobody_is_on_the_bar_or_borne_off_at_the_start()
    {
        var start = Setup.StartingPositionFromCorpus();

        foreach (var player in Players.Both)
        {
            Assert.Equal(0, start.OnBar(player));
            Assert.Equal(0, start.BorneOff(player));
        }
    }

    [Fact]
    public void The_engine_derives_it_rather_than_needing_it_asserted()
    {
        // The map used to decline this entry, and Game.Play used to be the only way in. A
        // caller can still assert whatever position he likes -- and must, for anything but the
        // start -- but he no longer has to assert this one.
        var derived = Setup.StartingPositionFromCorpus();

        Assert.Equal(derived, Corpus.StartingPosition.Position);
        Assert.Equal(
            MapEntries.StartingPosition.Locator, Corpus.StartingPosition.Justification);
    }
}
