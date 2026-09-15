using RulesKernel.Resolution;

namespace HoyleBackgammon;

internal static partial class Handlers
{
    /// <summary><c>men-count</c>: <see cref="Position.MenPerPlayer"/>.</summary>
    internal static partial Resolution<object> MenCount(Requests.MenCountRequest request) =>
        Value(Position.MenPerPlayer);
}
