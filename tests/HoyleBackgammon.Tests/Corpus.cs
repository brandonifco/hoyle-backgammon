using RulesKernel.Provenance;

namespace HoyleBackgammon.Tests;

/// <summary>
/// Positions the corpus states, as a caller of this engine would assert them.
/// </summary>
internal static class Corpus
{
    /// <summary>
    /// The starting arrangement, read from the corpus's prose rather than from Fig. 1.
    /// </summary>
    /// <remarks>
    /// This is an <em>assertion by the test</em>, not a rule the engine knows: the engine's
    /// own answer to "where do the men go" is a decline
    /// (<see cref="Setup.StartingPositionFromCorpus"/>), and the tests are a caller like any
    /// other.
    /// <para>
    /// The prose is on p. 272: "two of White's men are placed on the ace point in Black's
    /// inner table, five are placed on the six point in Black's outer table, three on the
    /// deuce point in White's outer table, and five on the six point in White's inner table.
    /// Black's men are placed in like manner on the points immediately facing these."
    /// </para>
    /// <para>
    /// Read through the point designations: Black's inner ace point is the far end of White's
    /// course, pip 24; Black's outer six point is pip 13; White's own outer deuce point is
    /// pip 8; White's own inner six point is pip 6. Black's are the same numbers in Black's
    /// own pips, "immediately facing".
    /// </para>
    /// <para>
    /// That this can be read at all is finding 1 in <c>MAP-FINDINGS.md</c>: the map declines
    /// <c>starting-position</c> as beyond a plain-text adapter, and a plain-text adapter reads
    /// the sentence above perfectly well.
    /// </para>
    /// </remarks>
    public static AssertedPosition StartingPosition { get; } = new(
        Position.Create(
            white: new Dictionary<int, int> { [24] = 2, [13] = 5, [8] = 3, [6] = 5 },
            black: new Dictionary<int, int> { [24] = 2, [13] = 5, [8] = 3, [6] = 5 }),
        AssertedBy: "HoyleBackgammon.Tests, read from the corpus's prose",
        Justification: new SourceLocator(MapEntries.SourceId, "BACKGAMMON / The Board and Men / p. 272"));

    /// <summary>
    /// The distribution in the bearing-off worked example: "five men on the cinque point,
    /// three on the quatre point, three on the deuce, and four on the ace point, the trois and
    /// six points being unoccupied (see Fig. 2)."
    /// </summary>
    /// <remarks>
    /// Fifteen men, and stated in prose in full. The adversary's men are placed on his own
    /// ace point, which the example does not speak of and which cannot interfere: his ace
    /// point is the far end of the player's course, outside the player's home table.
    /// </remarks>
    public static Position BearingOffExample { get; } = Position.Create(
        white: new Dictionary<int, int> { [5] = 5, [4] = 3, [2] = 3, [1] = 4 },
        black: new Dictionary<int, int> { [1] = 15 });
}
