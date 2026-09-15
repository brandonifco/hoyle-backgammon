using System.Collections.Immutable;
using RulesKernel.Provenance;

namespace HoyleBackgammon;

/// <summary>
/// A position supplied by somebody outside the engine, with the attribution that makes it
/// answerable for.
/// </summary>
/// <remarks>
/// The engine <em>can</em> originate the corpus's starting position — see
/// <see cref="Setup.StartingPositionFromCorpus"/> — so this type is no longer the only way a
/// game can begin. It stays because most positions are not the starting one: a mid-game
/// study, a replayed log, an endgame the corpus never states. For those the engine has no
/// fact and must not invent one, and the method's treatment of a condition no computation
/// settles applies: demand it, attribute it, record it alongside the outcome, and never infer
/// it. The attribution travels with the game's result; see <c>docs/decisions/0002</c>, as
/// amended by <c>docs/decisions/0003</c>.
/// </remarks>
/// <param name="Position">The arrangement being asserted.</param>
/// <param name="AssertedBy">Who is answerable for it. Free text; the engine does not parse it.</param>
/// <param name="Justification">
/// Where the asserter says it comes from, when they can cite something. Null when they cannot
/// — a caller setting up a mid-game study has nothing to cite, and saying so is honest.
/// </param>
public sealed record AssertedPosition(
    Position Position, string AssertedBy, SourceLocator? Justification = null)
{
    /// <summary>The asserted arrangement, with its asserter checked to be non-empty.</summary>
    public string AssertedBy { get; } = Check(AssertedBy);

    private static string Check(string assertedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assertedBy);
        return assertedBy;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        Justification is { } locator
            ? $"{Position} (asserted by {AssertedBy}, from {locator})"
            : $"{Position} (asserted by {AssertedBy}, uncited)";
}

/// <summary>
/// An entry's answer about a position somebody asserted, with the assertion it rests on.
/// </summary>
/// <remarks>
/// What <see cref="EntryPoints"/> returns for every entry whose request takes a position: the rule's
/// value, and the <see cref="AssertedPosition"/> it was asked about, so the attribution survives the
/// typed surface as it survives <see cref="Game.Play"/> into <see cref="GameRecord.Start"/>
/// (<c>docs/decisions/0002</c>).
/// </remarks>
/// <typeparam name="T">The rule's value type.</typeparam>
/// <param name="Value">The rule's value.</param>
/// <param name="Position">The position the rule was asked about, and who asserted it.</param>
public sealed record AssertedAnswer<T>(T Value, AssertedPosition Position)
    where T : notnull
{
    /// <summary>Who is answerable for the position, <see cref="AssertedPosition.AssertedBy"/>.</summary>
    public string AssertedBy => Position.AssertedBy;

    /// <summary>Where the asserter says the position comes from, <see cref="AssertedPosition.Justification"/>.</summary>
    public SourceLocator? Justification => Position.Justification;
}

/// <summary>Setting a game up.</summary>
public static class Setup
{
    /// <summary>
    /// The starting arrangement, derived from the corpus.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.StartingPosition"/> states it in prose: "two of White's men are
    /// placed on the ace point in Black's inner table, five are placed on the six point in
    /// Black's outer table, three on the deuce point in White's outer table, and five on the
    /// six point in White's inner table. Black's men are placed in like manner on the points
    /// immediately facing these."
    /// <para>
    /// Read through <see cref="MapEntries.PointDesignations"/> and
    /// <see cref="MapEntries.DirectionOfTravel"/>, those four points are the mover's own pips
    /// 24, 13, 8 and 6 (<see cref="Arrangement"/>): Black's inner ace point is the far end of
    /// White's course, Black's outer six point is 13, White's own outer deuce point is 8, and
    /// his own inner six point is 6. "Immediately facing" is <see cref="Geometry.Mirror"/>,
    /// which gives the same four numbers again in Black's own pips — so one table serves both
    /// sides.
    /// </para>
    /// <para>
    /// This returns a plain <see cref="Position"/> and not a <c>Resolution</c>: there is
    /// nothing left for a caller to settle. It used to decline, on a map version since
    /// retracted; see <c>docs/decisions/0003</c>.
    /// </para>
    /// </remarks>
    public static Position StartingPositionFromCorpus() =>
        Position.Create(white: Arrangement, black: Arrangement);

    /// <summary>
    /// The four points of the starting arrangement, in the mover's own pip numbering, with the
    /// number of his men on each. Two plus five plus three plus five is fifteen, which
    /// <see cref="Position.Create"/> re-checks against <see cref="MapEntries.MenCount"/>.
    /// </summary>
    public static IReadOnlyDictionary<int, int> Arrangement { get; } =
        ImmutableSortedDictionary.CreateRange(
            new Dictionary<int, int> { [24] = 2, [13] = 5, [8] = 3, [6] = 5 });
}
