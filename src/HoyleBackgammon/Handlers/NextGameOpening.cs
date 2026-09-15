using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>next-game-opening</c>'s rule reads.</summary>
    public sealed partial class NextGameOpeningRequest
    {
        /// <summary>The kind of win that ended the game.</summary>
        public global::HoyleBackgammon.GameValue? Value { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>next-game-opening</c>: <see cref="Outcome.Next"/>.</summary>
        internal static partial Resolution<object> NextGameOpening(Requests.NextGameOpeningRequest request) =>
            Value(Outcome.Next(Demand(request.Value, request.EntryId, nameof(request.Value))));
    }
}
