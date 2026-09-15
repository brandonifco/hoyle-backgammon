using RulesKernel.Resolution;

namespace HoyleBackgammon;

internal static partial class Handlers
{
    /// <summary><c>player-count</c>: the two players, <see cref="Players.Both"/>.</summary>
    internal static partial Resolution<object> PlayerCount(Requests.PlayerCountRequest request) =>
        Value(Players.Both);
}
