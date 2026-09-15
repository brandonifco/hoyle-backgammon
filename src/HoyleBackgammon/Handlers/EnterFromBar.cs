using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>enter-from-bar</c>'s rule reads.</summary>
    public sealed partial class EnterFromBarRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

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
            Value(Movement.MustEnterFromBar(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Player, request.EntryId, nameof(request.Player))));
    }
}
