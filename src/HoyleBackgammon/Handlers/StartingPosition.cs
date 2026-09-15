using RulesKernel.Resolution;

namespace HoyleBackgammon;

internal static partial class Handlers
{
    /// <summary><c>starting-position</c>: <see cref="Setup.StartingPositionFromCorpus"/>.</summary>
    internal static partial Resolution<object> StartingPosition(Requests.StartingPositionRequest request) =>
        Value(Setup.StartingPositionFromCorpus());
}
