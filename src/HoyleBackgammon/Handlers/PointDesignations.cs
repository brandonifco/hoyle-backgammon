using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>point-designations</c>'s rule reads.</summary>
    public sealed partial class PointDesignationsRequest
    {
        /// <summary>A pip, in the numbering of the player whose point it is (<see cref="Geometry"/>).</summary>
        public int? Pip { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>point-designations</c>: a point's corpus name, <see cref="Geometry.NameOf"/>.</summary>
        internal static partial Resolution<object> PointDesignations(Requests.PointDesignationsRequest request) =>
            Value(Geometry.NameOf(Demand(request.Pip, request.EntryId, nameof(request.Pip))));
    }
}
