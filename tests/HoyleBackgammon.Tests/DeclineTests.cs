using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// The four entries the map records as <c>declined</c>, reached as unresolved results rather
/// than found missing from the engine.
/// </summary>
/// <remarks>
/// <c>calling-the-throw</c> is the newest: a stated rule the map had no verdict on until the
/// blind mapping trial flagged it (<c>rules-factory#42</c>).
/// <para>
/// The other three are not what they were. <c>starting-position</c> used to be one of them, on a map
/// version since retracted; the corrected map declines <c>inner-table-handedness</c> instead —
/// a fact stated only in Fig. 1, and the only one in this corpus genuinely beyond a plain-text
/// adapter. See <c>docs/decisions/0003</c>.
/// </para>
/// </remarks>
public class DeclineTests
{
    [Fact]
    public void The_inner_tables_handedness_declines_as_missing_rules_data()
    {
        var result = Assert.IsType<Resolution<Quarter>.Unresolved>(
            BeyondAdapter.WhichCompartmentIsTheInnerTable());

        Assert.Equal(UnresolvedReason.MissingRulesData, result.Result.Reason);
        Assert.Equal(MapEntries.InnerTableHandedness.Locator, result.Result.Locator);
        Assert.Equal("BACKGAMMON / The Board and Men / p. 272", result.Result.Locator.Citation);
    }

    [Fact]
    public void Doubling_declines_as_outside_current_scope()
    {
        var result = Assert.IsType<Resolution<int>.Unresolved>(OutOfScope.Double());

        Assert.Equal(UnresolvedReason.OutsideCurrentScope, result.Result.Reason);
        Assert.Equal(MapEntries.DoublingCube.Locator, result.Result.Locator);
        Assert.Equal("BACKGAMMON / The Board and Men / p. 272", result.Result.Locator.Citation);
    }

    [Fact]
    public void Calling_the_throw_declines_as_outside_current_scope()
    {
        var result = Assert.IsType<Resolution<string>.Unresolved>(OutOfScope.CallTheThrow());

        Assert.Equal(UnresolvedReason.OutsideCurrentScope, result.Result.Reason);
        Assert.Equal(MapEntries.CallingTheThrow.Locator, result.Result.Locator);
        Assert.Equal("BACKGAMMON / Playing / p. 273", result.Result.Locator.Citation);
    }

    [Fact]
    public void Opening_advice_declines_as_outside_current_scope()
    {
        var result = Assert.IsType<Resolution<Play>.Unresolved>(OutOfScope.BestOpening());

        Assert.Equal(UnresolvedReason.OutsideCurrentScope, result.Result.Reason);
        Assert.Equal(MapEntries.StrategyAdvice.Locator, result.Result.Locator);
        Assert.Equal("BACKGAMMON / Hints for Play / p. 277", result.Result.Locator.Citation);
    }
}
