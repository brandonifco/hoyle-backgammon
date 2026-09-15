using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>rubber-scoring</c>'s rule reads.</summary>
    public sealed partial class RubberScoringRequest
    {
        /// <summary>The rubber's games, first first, each with the owner's rulings its value relied on (<see cref="GameResult.Rulings"/>).</summary>
        public IReadOnlyList<GameResult>? Games { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>rubber-scoring</c>: <see cref="Outcome.RubberWinner"/>, with the owner's rulings its games' values relied on, or the rule's decline.</summary>
        internal static partial Resolution<object> RubberScoring(Requests.RubberScoringRequest request) =>
            Answer(Outcome.RubberWinner(Demand(request.Games, request.EntryId, nameof(request.Games))));
    }
}
