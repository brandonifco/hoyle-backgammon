using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>
/// The one entry the map records as beyond the declared adapter's reach, reachable rather
/// than absent.
/// </summary>
/// <remarks>
/// The shape is <see cref="OutOfScope"/>'s and the reason is the same: "the corpus does not
/// carry this in a form the adapter can read" and "nobody looked" must not be
/// indistinguishable, and at runtime that means the engine has to be able to say which it is.
/// <para>
/// <b>Nothing inside this engine calls it.</b> That is not an oversight and the method is not
/// a stub standing in for work not done — <see cref="Geometry"/> holds every position in
/// player-relative pips, so no rule here ever has occasion to ask which physical compartment
/// of a board is whose. The method exists for a caller outside the engine — a renderer, say,
/// which must put the men somewhere on a screen — and it is the engine's only
/// <see cref="UnresolvedReason.MissingRulesData"/>.
/// </para>
/// </remarks>
public static class BeyondAdapter
{
    /// <summary>
    /// Which side of a physical board is a player's inner table.
    /// <see cref="MapEntries.InnerTableHandedness"/>: "With the men placed as in Fig. 1, the
    /// right hand is the inner or home table, and the left hand consequently the outer table."
    /// </summary>
    /// <remarks>
    /// The rule is fully determined in the corpus and is not ambiguous — but the sentence is
    /// only meaningful with Fig. 1 in view, because Fig. 1 is what fixes the orientation
    /// "right hand" is relative to. The declared adapter is <c>plain-text</c> and cannot read
    /// a figure, so by the factory's correspondence table this is
    /// <see cref="UnresolvedReason.MissingRulesData"/>.
    /// </remarks>
    /// <returns>Always <see cref="UnresolvedReason.MissingRulesData"/>.</returns>
    public static Resolution<Quarter> WhichCompartmentIsTheInnerTable() =>
        Resolution<Quarter>.FromUnresolved(new UnresolvedResult(
            UnresolvedReason.MissingRulesData,
            "identify which physical compartment of a board is a player's inner table",
            MapEntries.InnerTableHandedness.Locator));
}
