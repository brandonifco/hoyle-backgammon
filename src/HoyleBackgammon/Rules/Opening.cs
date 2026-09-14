using System.Collections.Immutable;
using RulesKernel.Randomness;
using Tabletop.Dice;

namespace HoyleBackgammon;

/// <summary>
/// The throw for the right to begin, and its outcome.
/// </summary>
/// <param name="Attempts">
/// Every pair thrown, in order, each holding White's die and Black's. A tie throws again, so
/// all but the last are ties.
/// </param>
/// <param name="Opener">The player who threw the higher number and has the right to begin.</param>
public sealed record OpeningRoll(ImmutableArray<DiceThrow> Attempts, Player Opener)
{
    /// <summary>The deciding pair — the two dice whose points the opener may adopt.</summary>
    public DiceThrow Deciding => Attempts[^1];
}

/// <summary>Beginning a game.</summary>
public static class Opening
{
    /// <summary>
    /// Throws for the right to begin.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.OpeningRoll"/>: "The game is commenced by each player throwing on
    /// the centre of the board a single die, the higher throw of the two giving the right to
    /// begin. In the event of a tie, the players throw again."
    /// <para>
    /// Which player throws his single die first is not stated and does not matter to the
    /// outcome; it matters to replay, because it fixes the order draws are taken in. This
    /// engine throws White's die then Black's, and that is an engine convention, not a rule.
    /// </para>
    /// </remarks>
    public static OpeningRoll RollForTheRight(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var attempts = ImmutableArray.CreateBuilder<DiceThrow>();
        while (true)
        {
            var attempt = DicePair.OfSixes.Throw(source);
            attempts.Add(attempt);
            if (attempt.First != attempt.Second)
            {
                var opener = attempt.First > attempt.Second ? Player.White : Player.Black;
                return new OpeningRoll(attempts.ToImmutable(), opener);
            }
        }
    }

    /// <summary>
    /// The opener's first throw: either the deciding pair adopted as it stands, or a fresh
    /// throw of both dice.
    /// </summary>
    /// <remarks>
    /// <see cref="MapEntries.OpeningThrowerOption"/>: "The thrower of the higher number may
    /// either adopt the points shown by the two dice as his own throw, or throw again."
    /// <para>
    /// "The two dice" are the two single dice just thrown for the right to begin — one each —
    /// so the first throw of the game is a two-dice throw either way, and
    /// <see cref="MapEntries.ThrowTwoDice"/>'s "all subsequent throws are with both dice"
    /// never has to reach back to it. An adopted pair can never be doublets, because a tie
    /// would have been thrown again.
    /// </para>
    /// </remarks>
    /// <param name="roll">The roll for the right to begin.</param>
    /// <param name="adopt">Whether the opener adopts the deciding pair.</param>
    /// <param name="source">Consulted only when he throws again.</param>
    public static DiceThrow OpeningThrow(OpeningRoll roll, bool adopt, IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(source);
        return adopt ? roll.Deciding : Throw(source);
    }

    /// <summary>
    /// An ordinary throw of both dice.
    /// <see cref="MapEntries.ThrowTwoDice"/>: "All subsequent throws are with both dice."
    /// </summary>
    /// <remarks>
    /// Six faces a die, from <see cref="MapEntries.DieFaces"/>: the corpus names twenty-one
    /// throws and calls them "all the possible throws", and twenty-one unordered pairs fix six
    /// faces. It never says the dice are fair, so this is the support, not the distribution.
    /// </remarks>
    public static DiceThrow Throw(IRandomSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return DicePair.OfSixes.Throw(source);
    }
}
