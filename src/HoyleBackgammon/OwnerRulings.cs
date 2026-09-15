using System.Collections.Immutable;
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
    /// is derived for the replay record, which carries <c>questionPart</c> since schema 3
    /// (<c>docs/decisions/0009</c>), so the record's bytes did not change when the rulings moved into the
    /// overlay.
    /// </remarks>
    public int QuestionPart => int.Parse(Id.AsSpan(Id.IndexOf('/', StringComparison.Ordinal) + 1), NumberStyles.None, CultureInfo.InvariantCulture);
}

/// <summary>Names for the generated rulings that say what each rules, and the order a result lists them in.</summary>
public static partial class OwnerRulings
{
    /// <summary><c>must-play-whole-throw/1</c>: where either number alone can be played but not both, the higher is compelled (<c>docs/decisions/0010</c>).</summary>
    public static OwnerRuling TheHigherNumberIsCompelled => MustPlayWholeThrow1;

    /// <summary><c>must-play-whole-throw/2</c>: a play is the position it reaches (<c>docs/decisions/0009</c>).</summary>
    public static OwnerRuling APlayIsThePositionItReaches => MustPlayWholeThrow2;

    /// <summary><c>bearing-off-eligible/1</c>: a man hit mid-bear-off who re-enters stops bearing off until every man is home again (<c>docs/decisions/0010</c>).</summary>
    public static OwnerRuling BearingOffStopsUntilEveryManIsHomeAgain => BearingOffEligible1;

    /// <summary><c>bearing-off-eligible/2</c>: bearing off begins within the throw that brings the last man home (<c>docs/decisions/0009</c>).</summary>
    public static OwnerRuling BearingOffBeginsWithinTheThrow => BearingOffEligible2;

    /// <summary><c>game-value/1</c>: the finish none of the three named results covers is a hit (<c>docs/decisions/0010</c>).</summary>
    public static OwnerRuling TheUncoveredFinishIsAHit => GameValue1;

    /// <summary><c>game-value/2</c>: a loser with nothing off and a man up or in the winner's home table is backgammoned, not gammoned (<c>docs/decisions/0010</c>).</summary>
    public static OwnerRuling TheOverlapIsABackgammon => GameValue2;

    /// <summary>
    /// <paramref name="rulings"/> without repeats, in <see cref="All"/>'s order (overlay order): the one
    /// order every result lists its rulings in, whatever order they were relied on.
    /// </summary>
    public static ImmutableArray<OwnerRuling> InOrder(IEnumerable<OwnerRuling> rulings)
    {
        ArgumentNullException.ThrowIfNull(rulings);
        var relied = rulings.ToHashSet();
        return [.. All.Where(relied.Contains)];
    }
}
