using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>must-play-whole-throw</c>'s rule reads.</summary>
    public sealed partial class MustPlayWholeThrowRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }

        /// <summary>The throw of two dice.</summary>
        public DiceThrow? Thrown { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>must-play-whole-throw</c>: the plays a throw compels, or the rule's decline, <see cref="LegalPlays.For"/>.</summary>
        internal static partial Resolution<object> MustPlayWholeThrow(Requests.MustPlayWholeThrowRequest request) =>
            Answer(LegalPlays.For(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Movement.Entitlement(Demand(request.Thrown, request.EntryId, nameof(request.Thrown)))));
    }
}
