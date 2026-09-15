using System.Collections.Immutable;

namespace HoyleBackgammon;

/// <summary>
/// An answer to part of a map entry's open question that the corpus does not give and this engine's
/// owner does. Not Hoyle: the map still records the question as <c>fate: unresolved</c>, and a result
/// that relies on a ruling names it (<see cref="Play.Rulings"/>, and each turn's <c>rulings</c> in
/// <see cref="GameRecord.ToCanonicalJson"/>) so it is never passed off as the corpus's
/// (<c>docs/decisions/0009</c>).
/// </summary>
/// <param name="Id">A stable id, the entry's id and the part of its question, e.g. <c>bearing-off-eligible/2</c>.</param>
/// <param name="Entry">The map entry whose question the ruling answers part of.</param>
/// <param name="QuestionPart">Which part of the entry's <c>ambiguity.question</c> it answers, counting from 1.</param>
/// <param name="RuledBy">Who ruled.</param>
/// <param name="RuledOn">When.</param>
/// <param name="Ruling">What was ruled, in a sentence.</param>
/// <param name="Record">The engine decision record that holds it.</param>
public sealed record OwnerRuling(
    string Id,
    MapEntry Entry,
    int QuestionPart,
    string RuledBy,
    DateOnly RuledOn,
    string Ruling,
    string Record);

/// <summary>The owner's rulings this engine applies, each on the second part of an open question.</summary>
public static class OwnerRulings
{
    private const string Record = "docs/decisions/0009-owner-rulings-are-ruleset-version-five.md";

    /// <summary>
    /// <c>bearing-off-eligible</c>, second part: once a player's first number brings his last man home,
    /// the number left bears off. With the last man on the nine point and six-trois, 9/3 then off with the
    /// trois is legal, and so is 9/6 then off with the six.
    /// </summary>
    public static OwnerRuling BearingOffBeginsWithinTheThrow { get; } = new(
        "bearing-off-eligible/2",
        MapEntries.BearingOffEligible,
        QuestionPart: 2,
        RuledBy: "Brandon",
        RuledOn: new DateOnly(2026, 9, 15),
        Ruling: "a number left after the move that brings the last man home is played under bearing off",
        Record);

    /// <summary>
    /// <c>must-play-whole-throw</c>, second part: a play is the position it reaches. Two orders of the same
    /// numbers reaching the same position are one play, offered once, crediting the rules of the first
    /// order found.
    /// </summary>
    public static OwnerRuling APlayIsThePositionItReaches { get; } = new(
        "must-play-whole-throw/2",
        MapEntries.MustPlayWholeThrow,
        QuestionPart: 2,
        RuledBy: "Brandon",
        RuledOn: new DateOnly(2026, 9, 15),
        Ruling: "orders of the same numbers reaching the same position are one play, credited with the rules "
            + "of the first order found",
        Record);

    /// <summary>Every ruling, in the order a <see cref="Play"/> lists them.</summary>
    public static ImmutableArray<OwnerRuling> All { get; } = [APlayIsThePositionItReaches, BearingOffBeginsWithinTheThrow];
}
