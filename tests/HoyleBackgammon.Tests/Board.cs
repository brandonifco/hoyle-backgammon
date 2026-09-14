namespace HoyleBackgammon.Tests;

/// <summary>One player's fifteen men, built up in his own pip numbering.</summary>
internal sealed class Side
{
    private readonly Dictionary<int, int> _men = [];

    public Side At(int pip, int count)
    {
        _men[pip] = _men.GetValueOrDefault(pip) + count;
        return this;
    }

    /// <summary>Parks whatever is left of the fifteen on <paramref name="pip"/>.</summary>
    public Side RestAt(int pip) => At(pip, Position.MenPerPlayer - _men.Values.Sum());

    /// <summary>Puts whatever is left of the fifteen off the board.</summary>
    public Side RestBorneOff() => RestAt(Geometry.BorneOffPip);

    public IReadOnlyDictionary<int, int> Men => _men;
}

internal static class Board
{
    public static Side Men() => new();

    public static Position Of(Side white, Side black) => Position.Create(white.Men, black.Men);
}
