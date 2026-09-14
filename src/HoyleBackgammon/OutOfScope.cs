using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>
/// The three entries the map records as <c>scope: out</c>, reachable rather than absent.
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
    /// Calling a throw aloud, the higher number first. <see cref="MapEntries.CallingTheThrow"/>,
    /// <c>scope: out</c>: the corpus states it and it is normative, but it is spoken by one
    /// player to the other, moves no man, and the corpus attaches no consequence to it. The move
    /// is made in accordance with the throw, not the call, so an engine handed the dice has
    /// nothing to do with it.
    /// </summary>
    /// <returns>Always <see cref="UnresolvedReason.OutsideCurrentScope"/>.</returns>
    public static Resolution<string> CallTheThrow() =>
        Resolution<string>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.OutsideCurrentScope,
            "call a throw aloud, the higher number first",
            MapEntries.CallingTheThrow.Locator));

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
