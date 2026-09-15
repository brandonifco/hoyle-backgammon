using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>enter-from-bar</c>'s rule reads.</summary>
    public sealed partial class EnterFromBarRequest
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
        /// <summary><c>enter-from-bar</c>: whether the player must enter a man before any other play, <see cref="Movement.MustEnterFromBar"/>.</summary>
        internal static partial Resolution<object> EnterFromBar(Requests.EnterFromBarRequest request) =>
            Value(Demand(request.Position, request.EntryId, nameof(request.Position)), Movement.MustEnterFromBar(
                request.Position!.Position,
                Demand(request.Player, request.EntryId, nameof(request.Player))));
    }
}
