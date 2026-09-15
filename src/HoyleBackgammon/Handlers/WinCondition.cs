using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>win-condition</c>'s rule reads.</summary>
    public sealed partial class WinConditionRequest
    {
        /// <summary>
        /// The position the rule is asked about, and who asserted it. The answer carries the assertion
        /// back (<see cref="AssertedAnswer{T}"/>), as <see cref="GameRecord.Start"/> does for a game.
        /// </summary>
        public AssertedPosition? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>win-condition</c>: whether the player has removed all his men, <see cref="BearingOff.HasWon"/>.</summary>
        internal static partial Resolution<object> WinCondition(Requests.WinConditionRequest request) =>
            Value(Demand(request.Position, request.EntryId, nameof(request.Position)), BearingOff.HasWon(
                request.Position!.Position,
                Demand(request.Player, request.EntryId, nameof(request.Player))));
    }
}
