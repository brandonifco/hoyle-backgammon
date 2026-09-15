using RulesKernel.Resolution;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>bearing-off-eligible</c>'s rule reads.</summary>
    public sealed partial class BearingOffEligibleRequest
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
        /// <summary>
        /// <c>bearing-off-eligible</c>: <see cref="BearingOff.Eligibility"/>, which names the owner's ruling where
        /// a man hit mid-bear-off has re-entered (<c>docs/decisions/0010</c>). Ruleset versions 2 to 5 declined there.
        /// </summary>
        internal static partial Resolution<object> BearingOffEligible(Requests.BearingOffEligibleRequest request)
        {
            var asserted = Demand(request.Position, request.EntryId, nameof(request.Position));
            return Value(asserted, BearingOff.Eligibility(
                asserted.Position,
                Demand(request.Player, request.EntryId, nameof(request.Player))));
        }
    }
}
