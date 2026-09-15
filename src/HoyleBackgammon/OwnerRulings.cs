using System.Globalization;

namespace HoyleBackgammon;

/// <summary>
/// This engine's additions to the <see cref="OwnerRuling"/> rules-factory generates from the overlay's
/// <c>rulings</c> (<c>Generated/Rulings.g.cs</c>, rules-factory decision 0027). The metadata is the
/// overlay's; nothing here restates it.
/// </summary>
public sealed partial record OwnerRuling
{
    /// <summary>
    /// Which part of the entry's <c>ambiguity.question</c> the ruling answers, counting from 1: the number
    /// after the slash in <see cref="Id"/>, which this engine's ids always carry (<c>must-play-whole-throw/2</c>).
    /// </summary>
    /// <remarks>
    /// The overlay names the part by its <see cref="Span"/>, and that is what the factory checks. The number
    /// is derived for the replay record, whose schema 3 carries <c>questionPart</c>
    /// (<c>docs/decisions/0009</c>), so the record's bytes did not change when the rulings moved into the
    /// overlay.
    /// </remarks>
    public int QuestionPart => int.Parse(Id.AsSpan(Id.IndexOf('/', StringComparison.Ordinal) + 1), NumberStyles.None, CultureInfo.InvariantCulture);
}

/// <summary>The names this engine gave its rulings before the factory generated them, kept as aliases.</summary>
public static partial class OwnerRulings
{
    /// <summary><c>must-play-whole-throw/2</c>: a play is the position it reaches.</summary>
    public static OwnerRuling APlayIsThePositionItReaches => MustPlayWholeThrow2;

    /// <summary><c>bearing-off-eligible/2</c>: bearing off begins within the throw that brings the last man home.</summary>
    public static OwnerRuling BearingOffBeginsWithinTheThrow => BearingOffEligible2;
}
