using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>game-value</c>'s rule reads.</summary>
    public sealed partial class GameValueRequest
    {
        /// <summary>
        /// The position the rule is asked about, and who asserted it. The answer carries the assertion
        /// back (<see cref="AssertedAnswer{T}"/>), as <see cref="GameRecord.Start"/> does for a game.
        /// </summary>
        public AssertedPosition? Position { get; init; }

        /// <summary>The player who has won.</summary>
        public Player? Winner { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>game-value</c>: <see cref="Outcome.ResultOf"/>, a hit, a gammon or a backgammon, with the owner's rulings it relies on.</summary>
        internal static partial Resolution<object> GameValue(Requests.GameValueRequest request) =>
            Answer(Demand(request.Position, request.EntryId, nameof(request.Position)), Outcome.ResultOf(
                request.Position!.Position,
                Demand(request.Winner, request.EntryId, nameof(request.Winner))));
    }
}
