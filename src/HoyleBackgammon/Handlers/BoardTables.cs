using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>board-tables</c>'s rule reads.</summary>
    public sealed partial class BoardTablesRequest
    {
        /// <summary>A pip, in the numbering of the player whose point it is (<see cref="Geometry"/>).</summary>
        public int? Pip { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>board-tables</c>: which quarter a pip lies in, <see cref="Geometry.QuarterOf"/>.</summary>
        internal static partial Resolution<object> BoardTables(Requests.BoardTablesRequest request) =>
            Value(Geometry.QuarterOf(Demand(request.Pip, request.EntryId, nameof(request.Pip))));
    }
}
