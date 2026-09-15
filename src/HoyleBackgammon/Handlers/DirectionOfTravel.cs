using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>direction-of-travel</c>'s rule reads.</summary>
    public sealed partial class DirectionOfTravelRequest
    {
        /// <summary>A pip, in the numbering of the player whose point it is (<see cref="Geometry"/>).</summary>
        public int? Pip { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>direction-of-travel</c>: the same point in the other player's numbering, <see cref="Geometry.Mirror"/>.</summary>
        internal static partial Resolution<object> DirectionOfTravel(Requests.DirectionOfTravelRequest request) =>
            Value(Geometry.Mirror(Demand(request.Pip, request.EntryId, nameof(request.Pip))));
    }
}
