using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>made-point</c>'s rule reads.</summary>
    public sealed partial class MadePointRequest
    {
        /// <summary>
        /// The position the rule is asked about, and who asserted it. The answer carries the assertion
        /// back (<see cref="AssertedAnswer{T}"/>), as <see cref="GameRecord.Start"/> does for a game.
        /// </summary>
        public AssertedPosition? Position { get; init; }

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
        /// <summary><c>made-point</c>: <see cref="Position.HasMadePoint"/>.</summary>
        internal static partial Resolution<object> MadePoint(Requests.MadePointRequest request) =>
            Value(Demand(request.Position, request.EntryId, nameof(request.Position)), request.Position!.Position.HasMadePoint(
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Demand(request.Pip, request.EntryId, nameof(request.Pip))));
    }
}
