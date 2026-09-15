using System.Collections.Immutable;
using RulesKernel.Resolution;
using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

internal static class Legal
{
    /// <summary>The legal plays for a throw, asserting that it resolves (since ruleset version 6 every throw does, docs/decisions/0010).</summary>
    public static ImmutableArray<Play> Plays(Position position, Player player, DiceThrow thrown) =>
        Assert.IsType<Resolution<ImmutableArray<Play>>.Resolved>(
            LegalPlays.For(position, player, Movement.Entitlement(thrown))).Value;
}
