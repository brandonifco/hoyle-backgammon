namespace HoyleBackgammon;

/// <summary>
/// One of the two players. <see cref="MapEntries.PlayerCount"/>: "Backgammon is played by two
/// persons."
/// </summary>
/// <remarks>
/// The type is closed over exactly two, and every player-relative rule here rests on that:
/// <see cref="Players.Adversary"/> is total only because there is exactly one other player,
/// and <see cref="Geometry.Mirror"/> pairs the two courses for the same reason.
/// </remarks>
public enum Player
{
    /// <summary>White.</summary>
    White,

    /// <summary>Black — "fifteen white and fifteen black (or red)".</summary>
    Black,
}

/// <summary>Helpers over <see cref="Player"/>.</summary>
public static class Players
{
    /// <summary>Both players, White first. The order is the engine's, not the corpus's.</summary>
    public static readonly IReadOnlyList<Player> Both = [Player.White, Player.Black];

    /// <summary>The other player.</summary>
    public static Player Adversary(this Player player) =>
        player == Player.White ? Player.Black : Player.White;
}
