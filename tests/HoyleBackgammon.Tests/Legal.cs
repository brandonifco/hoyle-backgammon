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
}
