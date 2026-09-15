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
        /// <summary><c>bearing-off-eligible</c>: <see cref="BearingOff.IsEligible"/>, except where <see cref="BearingOff.HasReEnteredMidBearOff"/> holds, the case <see cref="LegalPlays.For"/> declines.</summary>
        internal static partial Resolution<object> BearingOffEligible(Requests.BearingOffEligibleRequest request)
        {
            var asserted = Demand(request.Position, request.EntryId, nameof(request.Position));
            var position = asserted.Position;
            var player = Demand(request.Player, request.EntryId, nameof(request.Player));

            // The same case, the same reason and the same citation as LegalPlays.For's decline:
            // the map's question is whether the stage lasts once a man is hit and re-enters.
            if (BearingOff.HasReEnteredMidBearOff(position, player))
            {
                return Resolution<object>.FromUnresolved(new UnresolvedResult(
                    UnresolvedReason.RequiresInterpretation,
                    "say whether a player who had begun to bear off and whose man, hit, has re-entered "
                    + "may go on bearing off the men still at home",
                    MapEntries.BearingOffEligible.Locator));
            }

            return Value(asserted, BearingOff.IsEligible(position, player));
        }
    }
}
