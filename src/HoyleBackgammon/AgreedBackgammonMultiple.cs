using RulesKernel.Provenance;

namespace HoyleBackgammon;

/// <summary>
/// The multiple a backgammon pays, as the players agreed it, with the attribution that makes
/// it answerable for.
/// </summary>
/// <remarks>
/// <see cref="MapEntries.AgreedBackgammonMultiple"/> is a delegated standard, not a gap: "the
/// loser pays either thrice or four times (as may have been agreed) the amount of the single
/// stake." The corpus names the decider, so the engine owes the figure the four things a
/// method owes any condition no computation settles — demand it, attribute it, record it
/// alongside the outcome, and never infer it. This type is the demand and the attribution;
/// <see cref="StakeDue.Agreement"/> is the record alongside the outcome. Shaped like
/// <see cref="AssertedPosition"/>, which does the same job for a position, and named for the
/// corpus's own word rather than for the schema's.
/// <para>
/// The corpus also <em>bounds</em> the figure — thrice or four times, and nothing else — and
/// that bound is a rule it states rather than a judgement it delegates, so it is enforced
/// here instead of being discarded. An earlier reading of the entry treated the delegation as
/// an ambiguity and returned <c>RequiresInterpretation</c>, which declined a job the corpus
/// gave the engine the means to do and threw the bound away with it. See
/// <c>rules-factory/docs/decisions/0005</c> and finding 9 in <c>MAP-FINDINGS.md</c>.
/// </para>
/// </remarks>
/// <param name="Multiple">The figure agreed: <see cref="Thrice"/> or <see cref="FourTimes"/>.</param>
/// <param name="AgreedBy">Who is answerable for it. Free text; the engine does not parse it.</param>
/// <param name="Justification">
/// Where the agreement is recorded, when the parties can cite something — a club's standing
/// terms, a match agreement. Null when they cannot, which is the ordinary case for two people
/// at a board, and saying so is honest.
/// </param>
public sealed record AgreedBackgammonMultiple(
    int Multiple, string AgreedBy, SourceLocator? Justification = null)
{
    /// <summary>The lower of the two figures the corpus admits.</summary>
    public const int Thrice = 3;

    /// <summary>The higher of the two figures the corpus admits.</summary>
    public const int FourTimes = 4;

    /// <summary>The agreed multiple, checked against the bound the corpus states.</summary>
    public int Multiple { get; } = CheckMultiple(Multiple);

    /// <summary>Who agreed it, checked to be non-empty.</summary>
    public string AgreedBy { get; } = CheckAgreedBy(AgreedBy);

    private static int CheckMultiple(int multiple)
    {
        if (multiple is not (Thrice or FourTimes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiple),
                multiple,
                "a backgammon pays thrice or four times the single stake and nothing else "
                + $"[{MapEntries.AgreedBackgammonMultiple.Locator}].");
        }

        return multiple;
    }

    private static string CheckAgreedBy(string agreedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agreedBy);
        return agreedBy;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        Justification is { } locator
            ? $"{Multiple}x for a backgammon (agreed by {AgreedBy}, from {locator})"
            : $"{Multiple}x for a backgammon (agreed by {AgreedBy}, uncited)";
}

/// <summary>
/// What a finished game pays, and the agreement it was settled under.
/// </summary>
/// <remarks>
/// The agreement travels with the figure even where it did not decide it — a hit pays the
/// single stake and a gammon double whatever was agreed — because "record it alongside the
/// outcome" is an obligation about the result, not about whichever branch happened to consult
/// the input. A reader of a settled stake can always see whose agreement it was settled under,
/// exactly as a reader of a <see cref="GameRecord"/> can see whose position it was played
/// from.
/// </remarks>
/// <param name="Value">The kind of win being paid for.</param>
/// <param name="Multiple">What it pays, as a multiple of the single stake.</param>
/// <param name="Agreement">The players' agreement, as supplied.</param>
public sealed record StakeDue(GameValue Value, int Multiple, AgreedBackgammonMultiple Agreement)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Value} pays {Multiple}x [{Agreement}]";
}
