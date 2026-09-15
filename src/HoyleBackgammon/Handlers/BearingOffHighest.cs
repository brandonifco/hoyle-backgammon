using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>bearing-off-highest</c>'s rule reads.</summary>
    public sealed partial class BearingOffHighestRequest
    {
        /// <summary>The position the rule is asked about.</summary>
        public Position? Position { get; init; }

        /// <summary>The player the rule is asked about.</summary>
        public Player? Player { get; init; }

        /// <summary>The number of one die.</summary>
        public int? Die { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>bearing-off-highest</c>: <see cref="BearingOff.MovesForDie"/>, which offers the removal from the highest point exactly when the other two fashions yield nothing.</summary>
        internal static partial Resolution<object> BearingOffHighest(Requests.BearingOffHighestRequest request) =>
            Value(BearingOff.MovesForDie(
                Demand(request.Position, request.EntryId, nameof(request.Position)),
                Demand(request.Player, request.EntryId, nameof(request.Player)),
                Demand(request.Die, request.EntryId, nameof(request.Die))));
    }
}
