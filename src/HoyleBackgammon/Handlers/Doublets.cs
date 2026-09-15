using RulesKernel.Resolution;
using Tabletop.Dice;

namespace HoyleBackgammon.Requests
{
    /// <summary>The inputs <c>doublets</c>'s rule reads.</summary>
    public sealed partial class DoubletsRequest
    {
        /// <summary>The throw of two dice.</summary>
        public DiceThrow? Thrown { get; init; }
    }
}

namespace HoyleBackgammon
{
    internal static partial class Handlers
    {
        /// <summary><c>doublets</c>: the numbers a throw entitles, <see cref="Movement.Entitlement"/>.</summary>
        internal static partial Resolution<object> Doublets(Requests.DoubletsRequest request) =>
            Value(Movement.Entitlement(Demand(request.Thrown, request.EntryId, nameof(request.Thrown))));
    }
}
