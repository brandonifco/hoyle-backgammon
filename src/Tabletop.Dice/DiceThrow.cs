namespace Tabletop.Dice;

/// <summary>
/// The outcome of throwing two dice: the two faces, in the order they were thrown.
/// </summary>
/// <remarks>
/// The throw order is retained rather than normalised because it is the only part of the
/// outcome that a replay can check against the generator. Rulesets that name a throw
/// high-first (Hoyle's "six deuce") can read <see cref="Higher"/> and <see cref="Lower"/>.
/// <para>
/// This type says a throw of equal faces is <see cref="IsDoublets"/> and stops there. What
/// doublets are worth is a ruleset's business: Hoyle plays them twice over, other games do
/// something else or nothing at all.
/// </para>
/// </remarks>
public readonly record struct DiceThrow
{
    /// <summary>The face of the first die thrown.</summary>
    public int First { get; }

    /// <summary>The face of the second die thrown.</summary>
    public int Second { get; }

    /// <summary>A throw of the two given faces, in throw order.</summary>
    public DiceThrow(int first, int second)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(second, 1);
        First = first;
        Second = second;
    }

    /// <summary>True when both dice show the same face.</summary>
    public bool IsDoublets => First == Second;

    /// <summary>The greater of the two faces.</summary>
    public int Higher => Math.Max(First, Second);

    /// <summary>The lesser of the two faces.</summary>
    public int Lower => Math.Min(First, Second);

    /// <inheritdoc/>
    public override string ToString() => $"{First}-{Second}";
}
