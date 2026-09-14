using RulesKernel.Randomness;

namespace HoyleBackgammon.Tests;

/// <summary>
/// A generator that hands back exactly the raw words it was given. Face <c>f</c> of a d6 is
/// the raw value <c>f - 1</c>, so <c>Faces(6, 3)</c> reads as "a six and a trois".
/// </summary>
internal sealed class ScriptedSource(params uint[] raw) : IRandomSource
{
    private int _next;

    public int Drawn => _next;

    public static ScriptedSource Faces(params int[] faces)
    {
        var raw = new uint[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            raw[i] = (uint)(faces[i] - 1);
        }

        return new ScriptedSource(raw);
    }

    public uint NextUInt32()
    {
        if (_next >= raw.Length)
        {
            throw new InvalidOperationException(
                $"the engine asked for draw {_next} and only {raw.Length} were scripted.");
        }

        return raw[_next++];
    }
}

/// <summary>Counts the draws another generator is asked for.</summary>
internal sealed class CountingSource(IRandomSource inner) : IRandomSource
{
    public int Drawn { get; private set; }

    public uint NextUInt32()
    {
        Drawn++;
        return inner.NextUInt32();
    }
}
