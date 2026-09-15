using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>die-faces</c>'s rule reads.</summary>
    public sealed partial class DieFacesRequest
    {
        /// <summary>The generator the dice draw from.</summary>
        public IRandomSource? Source { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>die-faces</c>: the face one die shows, <see cref="Die.Throw"/> of <see cref="Die.D6"/>.</summary>
        internal static partial Resolution<object> DieFaces(Requests.DieFacesRequest request) =>
            Value(Die.D6.Throw(Demand(request.Source, request.EntryId, nameof(request.Source))));
    }
}
