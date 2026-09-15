using RulesKernel.Randomness;
using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>opening-roll</c>'s rule reads.</summary>
    public sealed partial class OpeningRollRequest
    {
        /// <summary>The generator the dice draw from.</summary>
        public IRandomSource? Source { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>opening-roll</c>: <see cref="Opening.RollForTheRight"/>.</summary>
        internal static partial Resolution<object> OpeningRoll(Requests.OpeningRollRequest request) =>
            Value(Opening.RollForTheRight(Demand(request.Source, request.EntryId, nameof(request.Source))));
    }
}
