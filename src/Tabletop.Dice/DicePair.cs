using RulesKernel.Randomness;

namespace Tabletop.Dice;

/// <summary>
/// A pair of dice thrown together.
/// </summary>
/// <remarks>
/// The two dice are thrown in a fixed order — <see cref="First"/> then <see cref="Second"/> —
/// so that the number and order of draws taken from an <see cref="IRandomSource"/> is a
/// property of this type rather than of a caller's loop. Replay depends on it.
/// </remarks>
public readonly record struct DicePair
{
    /// <summary>The die thrown first.</summary>
    public Die First { get; }

    /// <summary>The die thrown second.</summary>
    public Die Second { get; }

    /// <summary>A pair of the two given dice.</summary>
    public DicePair(Die first, Die second)
    {
        if (!first.IsValid)
        {
            throw new ArgumentException("first is default(Die).", nameof(first));
        }

        if (!second.IsValid)
        {
            throw new ArgumentException("second is default(Die).", nameof(second));
        }

        First = first;
        Second = second;
    }

    /// <summary>A pair of six-faced dice.</summary>
    public static DicePair OfSixes => new(Die.D6, Die.D6);

    /// <summary>
    /// Throws both dice, <see cref="First"/> before <see cref="Second"/>.
    /// </summary>
    public DiceThrow Throw(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int first = First.Throw(source);
        int second = Second.Throw(source);
        return new DiceThrow(first, second);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{First}+{Second}";
}
