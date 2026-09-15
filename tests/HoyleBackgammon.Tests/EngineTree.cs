namespace HoyleBackgammon.Tests;

/// <summary>
/// The engine's source tree, found from the test assembly: the directory holding
/// <c>HoyleBackgammon.slnx</c>.
/// </summary>
internal static class EngineTree
{
    public static DirectoryInfo Root { get; } = Find();

    public static string PathOf(params string[] parts) => Path.Combine([Root.FullName, .. parts]);

    private static DirectoryInfo Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HoyleBackgammon.slnx")))
            {
                return directory;
            }
        }

        throw new DirectoryNotFoundException($"no HoyleBackgammon.slnx above {AppContext.BaseDirectory}");
    }
}
