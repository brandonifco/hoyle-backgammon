using System.Collections.Immutable;
using RulesKernel.Resolution;
using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

internal static class Legal
{
    /// <summary>The legal plays for a throw, asserting that the corpus settles the case.</summary>
    public static ImmutableArray<Play> Plays(Position position, Player player, DiceThrow thrown) =>
        Assert.IsType<Resolution<ImmutableArray<Play>>.Resolved>(
            LegalPlays.For(position, player, Movement.Entitlement(thrown))).Value;

    /// <summary>The unresolved result for a throw the corpus does not settle.</summary>
    public static UnresolvedResult Unresolved(Position position, Player player, DiceThrow thrown) =>
        Assert.IsType<Resolution<ImmutableArray<Play>>.Unresolved>(
            LegalPlays.For(position, player, Movement.Entitlement(thrown))).Result;

    /// <summary>
    /// Whether a decline is must-play-whole-throw's first question, either die alone playable but not
    /// both. The entry's second question (map 6.0.0, rules-factory#125) cites the same locator, so the
    /// two are told apart by what the engine attempted.
    /// </summary>
    public static bool IsEitherDieAloneDecline(UnresolvedResult result) =>
        result.Locator.Equals(MapEntries.MustPlayWholeThrow.Locator)
        && result.Attempted.StartsWith("choose between plays that use incomparable parts", StringComparison.Ordinal);
}
