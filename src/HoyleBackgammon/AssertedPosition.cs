using RulesKernel.Provenance;
using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>
/// A position supplied by somebody outside the engine, with the attribution that makes it
/// answerable for.
/// </summary>
/// <remarks>
/// The engine cannot originate a starting position: <c>starting-position</c> is declined
/// (see <see cref="Setup.StartingPositionFromCorpus"/>). So a game begins from a position a
/// caller asserts, and the method's treatment of a condition no computation settles applies:
/// demand it, attribute it, record it alongside the outcome, and never infer it. The
/// attribution travels with the game's result; see <c>docs/decisions/0002</c>.
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

/// <summary>Setting a game up.</summary>
public static class Setup
{
    /// <summary>
    /// The starting arrangement, asked of the corpus. Always declines.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.StartingPosition"/> carries
    /// <c>beyondAdapter: { adapter: "plain-text", modality: "illustration" }</c>: the rule is
    /// in the corpus, in Fig. 1, and the declared adapter cannot read a figure. By the
    /// factory's correspondence table that is <see cref="UnresolvedReason.MissingRulesData"/>.
    /// <para>
    /// This method exists so the decline is <em>reachable</em> rather than being a hole in the
    /// engine. A caller who asks where to put the men gets an answer that says which entry and
    /// which page, and can then decide what to assert.
    /// </para>
    /// <para>
    /// The map is wrong about this entry — the arrangement is also stated in prose on p. 272,
    /// which a plain-text adapter reads perfectly well. The engine implements the map as
    /// written and records the defect; see finding 1 in <c>MAP-FINDINGS.md</c>.
    /// </para>
    /// </remarks>
    public static Resolution<Position> StartingPositionFromCorpus() =>
        Resolution<Position>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.MissingRulesData,
            "place the men as at the start of a game",
            MapEntries.StartingPosition.Locator));
}
