using RulesKernel.Randomness;
using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>throw-two-dice</c>'s rule reads.</summary>
    public sealed partial class ThrowTwoDiceRequest
    {
        /// <summary>The generator the dice draw from.</summary>
        public IRandomSource? Source { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>throw-two-dice</c>: <see cref="Opening.Throw"/>.</summary>
        internal static partial Resolution<object> ThrowTwoDice(Requests.ThrowTwoDiceRequest request) =>
            Value(Opening.Throw(Demand(request.Source, request.EntryId, nameof(request.Source))));
    }
}
