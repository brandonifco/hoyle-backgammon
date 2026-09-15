using RulesKernel.Resolution;

namespace HoyleBackgammon;

internal static partial class Handlers
{
    /// <summary><c>hit-pays-single-stake</c>: what a hit pays, <see cref="Outcome.Pays"/>, under the agreement asserted for <c>agreed-backgammon-multiple</c>, which does not change it.</summary>
    internal static partial Resolution<object> HitPaysSingleStake(Requests.HitPaysSingleStakeRequest request) =>
        Value(Outcome.Pays(global::HoyleBackgammon.GameValue.Hit, Agreed(request.Assertions)));
}
