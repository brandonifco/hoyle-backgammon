using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>legal-destination</c>'s rule reads.</summary>
    public sealed partial class LegalDestinationRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }

        /// <summary>A pip, in the numbering of the player whose point it is (<see cref="Geometry"/>).</summary>
        public int? Pip { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>legal-destination</c>: <see cref="Movement.IsPermittedDestination"/>.</summary>
        internal static partial Resolution<object> LegalDestination(Requests.LegalDestinationRequest request) =>
            Value(Movement.IsPermittedDestination(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Demand(request.Pip, request.EntryId, nameof(request.Pip))));
    }
}
