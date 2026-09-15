using RulesKernel.Randomness;
using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>opening-thrower-option</c>'s rule reads.</summary>
    public sealed partial class OpeningThrowerOptionRequest
    {
        /// <summary>The roll for the right to begin.</summary>
        public global::HoyleBackgammon.OpeningRoll? Roll { get; init; }

        /// <summary>Whether the opener adopts the points shown by the two dice rather than throwing again.</summary>
        public bool Adopt { get; init; }

        /// <summary>The generator the dice draw from.</summary>
        public IRandomSource? Source { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>opening-thrower-option</c>: <see cref="Opening.OpeningThrow"/>.</summary>
        internal static partial Resolution<object> OpeningThrowerOption(Requests.OpeningThrowerOptionRequest request) =>
            Value(Opening.OpeningThrow(
                Demand(request.Roll, request.EntryId, nameof(request.Roll)),
                request.Adopt,
                Demand(request.Source, request.EntryId, nameof(request.Source))));
    }
}
