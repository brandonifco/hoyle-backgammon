using RulesKernel.Randomness;

namespace Tabletop.Dice;

/// <summary>
/// A die of <see cref="Faces"/> faces, numbered 1 through <see cref="Faces"/>.
/// </summary>
/// <remarks>
/// The kernel ships no dice on purpose (RulesKernel docs/decisions/0002): randomness is an
/// optional package and dice are subject-matter vocabulary. What it ships is
/// <see cref="UniformInt.Below(IRandomSource, uint)"/>, which returns a value in
/// <c>[0, bound)</c>. The face numbering here is the documented <c>f - 1</c> convention:
/// face <c>f</c> of a d(n) is the raw value <c>f - 1</c>.
/// </remarks>
public readonly record struct Die
{
    /// <summary>The number of faces. Always at least one.</summary>
    public int Faces { get; }

    /// <summary>A die of <paramref name="faces"/> faces.</summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="faces"/> is below one.</exception>
    public Die(int faces)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(faces, 1);
        Faces = faces;
    }

    /// <summary>The six-faced die.</summary>
    public static Die D6 => new(6);

    /// <summary>True unless this is <c>default(Die)</c>, which bypasses the constructor.</summary>
    public bool IsValid => Faces >= 1;

    /// <summary>
    /// Throws the die, consuming exactly one bounded draw from <paramref name="source"/> unless
    /// <see cref="UniformInt.Below(IRandomSource, uint)"/> rejects a raw value.
    /// </summary>
    /// <returns>A face in <c>[1, Faces]</c>.</returns>
    public int Throw(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!IsValid)
        {
            throw new InvalidOperationException(
                "this is default(Die), which has no faces; construct one with new Die(faces).");
        }

        return (int)UniformInt.Below(source, (uint)Faces) + 1;
    }

    /// <inheritdoc/>
    public override string ToString() => IsValid ? $"d{Faces}" : "(invalid die)";
}
