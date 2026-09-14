using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// The three entries the map records as <c>declined</c>, reached as unresolved results rather
/// than found missing from the engine.
/// </summary>
public class DeclineTests
{
    [Fact]
    public void Starting_position_declines_as_missing_rules_data()
    {
        var result = Assert.IsType<Resolution<Position>.Unresolved>(Setup.StartingPositionFromCorpus());

        Assert.Equal(UnresolvedReason.MissingRulesData, result.Result.Reason);
        Assert.Equal(MapEntries.StartingPosition.Locator, result.Result.Locator);
    }

    [Fact]
    public void Doubling_declines_as_outside_current_scope()
    {
        var result = Assert.IsType<Resolution<int>.Unresolved>(OutOfScope.Double());

        Assert.Equal(UnresolvedReason.OutsideCurrentScope, result.Result.Reason);
        Assert.Equal("(absent)", result.Result.Locator.Citation);
    }

    [Fact]
    public void Opening_advice_declines_as_outside_current_scope()
    {
        var result = Assert.IsType<Resolution<Play>.Unresolved>(OutOfScope.BestOpening());

        Assert.Equal(UnresolvedReason.OutsideCurrentScope, result.Result.Reason);
        Assert.Equal(MapEntries.StrategyAdvice.Locator, result.Result.Locator);
    }

    [Fact]
    public void Every_decline_cites_the_pinned_corpus()
    {
        foreach (var entry in new[]
                 {
                     MapEntries.StartingPosition, MapEntries.DoublingCube, MapEntries.StrategyAdvice,
                     MapEntries.MustPlayWholeThrow, MapEntries.StakeMultiplier,
                 })
        {
            Assert.Equal(MapEntries.Baseline.SourceId, entry.Locator.SourceId);
        }
    }

    [Fact]
    public void The_engine_pins_the_corpus_it_was_read_from_and_names_its_generator()
    {
        Assert.False(Game.Identity.IsDeterministicWithoutRandomness);
        Assert.Equal(
            "5d505fa9f6202340eb55313b8ef607b816087a860d3d51b1bf92b5f65240645e",
            Assert.Single(Game.Identity.SourceBaselines).ContentHash);
    }
}
