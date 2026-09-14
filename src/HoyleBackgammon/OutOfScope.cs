using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>
/// The two entries the map records as <c>scope: out</c>, reachable rather than absent.
/// </summary>
/// <remarks>
/// A caller who asks this engine to double, or to tell them how to open, gets an answer that
/// names the entry and says why — not a missing method. "Absent from the corpus" and "nobody
/// looked" must not be indistinguishable, and at runtime that means the engine has to be able
/// to say which one it is.
/// </remarks>
public static class OutOfScope
{
    /// <summary>
    /// Doubling. <see cref="MapEntries.DoublingCube"/>, <c>scope: out</c>: this 1909 corpus
    /// predates the doubling cube, so the rule is not in it. An engine built from this corpus
    /// is a 1909 engine.
    /// </summary>
    /// <returns>Always <see cref="UnresolvedReason.OutsideCurrentScope"/>.</returns>
    public static Resolution<int> Double() =>
        Resolution<int>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.OutsideCurrentScope,
            "double the stake",
            MapEntries.DoublingCube.Locator));

    /// <summary>
    /// How to play a given opening throw well. <see cref="MapEntries.StrategyAdvice"/>,
    /// <c>scope: out</c>: Hints for Play is advice, not rules.
    /// </summary>
    /// <returns>Always <see cref="UnresolvedReason.OutsideCurrentScope"/>.</returns>
    public static Resolution<Play> BestOpening() =>
        Resolution<Play>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.OutsideCurrentScope,
            "choose the best play for an opening throw",
            MapEntries.StrategyAdvice.Locator));
}
