using System.Collections.Immutable;

namespace HoyleBackgammon;

/// <summary>
/// Where every choice a game needs comes from.
/// </summary>
/// <remarks>
/// The engine plays no part in choosing. Determinism is "same seed and same ordered decisions
/// give the same game", so the decisions have to come from somewhere outside the engine and
/// be orderable.
/// </remarks>
public interface IDecider
{
    /// <summary>
    /// Whether the opener adopts the deciding pair rather than throwing again.
    /// <see cref="MapEntries.OpeningThrowerOption"/>.
    /// </summary>
    bool AdoptOpeningThrow(OpeningRoll roll);

    /// <summary>
    /// Which of the legal plays to make, as an index into <paramref name="plays"/>.
    /// </summary>
    int ChoosePlay(Player player, ImmutableArray<Play> plays);
}

/// <summary>
/// Takes the first option every time: never adopts the opening throw, always plays
/// <c>plays[0]</c>. A fixed policy, so a game under it is a function of the seed alone.
/// </summary>
public sealed class FirstOptionDecider : IDecider
{
    /// <inheritdoc/>
    public bool AdoptOpeningThrow(OpeningRoll roll) => false;

    /// <inheritdoc/>
    public int ChoosePlay(Player player, ImmutableArray<Play> plays) => 0;
}

/// <summary>
/// Replays a recorded list of decisions in order: a boolean decision consumes one entry and
/// reads it as non-zero for yes; a play decision consumes one entry and uses it as an index.
/// </summary>
/// <remarks>
/// Running out of decisions is an error rather than a silent fallback. A fallback would make
/// two different decision lists produce the same game, which is exactly the confusion the
/// replay contract exists to prevent.
/// </remarks>
public sealed class ScriptedDecider : IDecider
{
    private readonly IReadOnlyList<int> _decisions;
    private int _next;

    /// <summary>Replays <paramref name="decisions"/> in order.</summary>
    public ScriptedDecider(IReadOnlyList<int> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        _decisions = decisions;
    }

    /// <summary>How many decisions have been consumed so far.</summary>
    public int Consumed => _next;

    /// <inheritdoc/>
    public bool AdoptOpeningThrow(OpeningRoll roll) => Next() != 0;

    /// <inheritdoc/>
    public int ChoosePlay(Player player, ImmutableArray<Play> plays)
    {
        int index = Next();
        if (index < 0 || index >= plays.Length)
        {
            throw new InvalidOperationException(
                $"decision {_next - 1} is {index}, which is not one of the {plays.Length} legal "
                + "plays offered.");
        }

        return index;
    }

    private int Next()
    {
        if (_next >= _decisions.Count)
        {
            throw new InvalidOperationException(
                $"the game asked for decision {_next} and only {_decisions.Count} were scripted.");
        }

        return _decisions[_next++];
    }
}
