using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>blot-hit</c>'s rule reads.</summary>
    public sealed partial class BlotHitRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }

        /// <summary>The pip the man moves from.</summary>
        public int? From { get; init; }

        /// <summary>The pip the man moves to.</summary>
        public int? To { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>blot-hit</c>: the position after a man moves, a blot on the destination taken up, <see cref="Position.Apply"/>.</summary>
        internal static partial Resolution<object> BlotHit(Requests.BlotHitRequest request) =>
            Value(Demand(request.Position, request.EntryId, nameof(request.Position)).Apply(
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Demand(request.From, request.EntryId, nameof(request.From)),
                Demand(request.To, request.EntryId, nameof(request.To))));
    }
}
