using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>stake-multiplier</c>'s rule reads.</summary>
    public sealed partial class StakeMultiplierRequest
    {
        /// <summary>The kind of win.</summary>
        public global::HoyleBackgammon.GameValue? Value { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>stake-multiplier</c>: <see cref="Outcome.Pays"/>, under the agreement asserted for <c>agreed-backgammon-multiple</c> (row 8: demanded of the caller, never inferred).</summary>
        internal static partial Resolution<object> StakeMultiplier(Requests.StakeMultiplierRequest request) =>
            Value(Outcome.Pays(
                Demand(request.Value, request.EntryId, nameof(request.Value)),
                Agreed(request.Assertions)));
    }
}
