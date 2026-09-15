using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>move-by-pip</c>'s rule reads.</summary>
    public sealed partial class MoveByPipRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }

        /// <summary>The number of one die.</summary>
        public int? Die { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>move-by-pip</c>: <see cref="Movement.MovesForDie"/>.</summary>
        internal static partial Resolution<object> MoveByPip(Requests.MoveByPipRequest request) =>
            Value(Movement.MovesForDie(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Demand(request.Die, request.EntryId, nameof(request.Die))));
    }
}
