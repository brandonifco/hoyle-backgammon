namespace HoyleBackgammon.Tests;

/// <summary>
/// Positions the corpus states, as a caller of this engine would assert them.
/// </summary>
internal static class Corpus
{
    /// <summary>
    /// The starting arrangement, as the engine derives it, wrapped for the callers that want
    /// an <see cref="AssertedPosition"/>.
    /// </summary>
    /// <remarks>
    /// This used to be the test suite asserting, from the corpus's prose, an arrangement the
    /// engine declined to know. The map's retraction of that decline made the fixture the
    /// production rule, so the position now comes from
    /// <see cref="Setup.StartingPositionFromCorpus"/> and the tests no longer carry a second
    /// copy of the four numbers. What a second copy would check is not the arrangement but
    /// whether two literals were typed alike; the numbers are pinned against the corpus's
    /// prose in <c>StartingPositionTests</c>, which reads them off the designations.
    /// <para>
    /// The attribution is still the test's own, because who asserts a position is a fact
    /// about the caller and not about the corpus — and here the caller is quoting the engine.
    /// </para>
    /// </remarks>
    public static AssertedPosition StartingPosition { get; } = new(
        Setup.StartingPositionFromCorpus(),
        AssertedBy: "HoyleBackgammon.Tests, quoting Setup.StartingPositionFromCorpus",
        Justification: MapEntries.StartingPosition.Locator);

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
