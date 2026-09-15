using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>bearing-off-doublets</c>'s rule reads.</summary>
    public sealed partial class BearingOffDoubletsRequest
    {
        /// <summary>
        /// The position the rule is asked about, and who asserted it. The answer carries the assertion
        /// back (<see cref="AssertedAnswer{T}"/>), as <see cref="GameRecord.Start"/> does for a game.
        /// </summary>
        public AssertedPosition? Position { get; init; }

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
        /// <summary><c>bearing-off-doublets</c>: the plays a throw permits, four numbers for doublets, <see cref="LegalPlays.For"/>.</summary>
        internal static partial Resolution<object> BearingOffDoublets(Requests.BearingOffDoubletsRequest request) =>
            Answer(Demand(request.Position, request.EntryId, nameof(request.Position)), LegalPlays.For(
                request.Position!.Position,
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Movement.Entitlement(Demand(request.Thrown, request.EntryId, nameof(request.Thrown)))));
    }
}
