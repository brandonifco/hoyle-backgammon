using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>full-table-suspension</c>'s rule reads.</summary>
    public sealed partial class FullTableSuspensionRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>full-table-suspension</c>: <see cref="Movement.IsWhollySuspended"/>.</summary>
        internal static partial Resolution<object> FullTableSuspension(Requests.FullTableSuspensionRequest request) =>
            Value(Movement.IsWhollySuspended(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Player, request.EntryId, nameof(request.Player))));
    }
}
