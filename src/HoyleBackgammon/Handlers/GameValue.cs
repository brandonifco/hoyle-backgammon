using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>game-value</c>'s rule reads.</summary>
    public sealed partial class GameValueRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player who has won.</summary>
        public Player? Winner { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>game-value</c>: <see cref="Outcome.ValueOf"/>, a hit, a gammon or a backgammon, or the rule's decline.</summary>
        internal static partial Resolution<object> GameValue(Requests.GameValueRequest request) =>
            Answer(Outcome.ValueOf(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Winner, request.EntryId, nameof(request.Winner))));
    }
}
