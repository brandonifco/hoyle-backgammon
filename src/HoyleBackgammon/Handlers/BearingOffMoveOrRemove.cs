using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>bearing-off-move-or-remove</c>'s rule reads.</summary>
    public sealed partial class BearingOffMoveOrRemoveRequest
    {
        /// <summary>
        /// The position the rule is asked about, and who asserted it. The answer carries the assertion
        /// back (<see cref="AssertedAnswer{T}"/>), as <see cref="GameRecord.Start"/> does for a game.
        /// </summary>
        public AssertedPosition? Position { get; init; }

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
        /// <summary><c>bearing-off-move-or-remove</c>: <see cref="BearingOff.MovesForDie"/>.</summary>
        internal static partial Resolution<object> BearingOffMoveOrRemove(Requests.BearingOffMoveOrRemoveRequest request) =>
            Value(Demand(request.Position, request.EntryId, nameof(request.Position)), BearingOff.MovesForDie(
                request.Position!.Position,
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Demand(request.Die, request.EntryId, nameof(request.Die))));
    }
}
