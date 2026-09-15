using System.Collections.Immutable;
using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>
/// Turning a throw into the set of plays the corpus allows.
/// </summary>
public static class LegalPlays
{
    /// <summary>
    /// Every play <paramref name="player"/> may make with <paramref name="entitlement"/>, or an
    /// unresolved result where the corpus does not settle which of two incomparable plays is
    /// compelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="MapEntries.MustPlayWholeThrow"/>: "Any part of a throw which cannot be
    /// played is lost to the thrower, but every player is compelled to play the whole of his
    /// throw if it is possible to do so."
    /// </para>
    /// <para>
    /// Read as an obligation to play a set of numbers that cannot be extended: if the whole
    /// throw can be played, it must be, and nothing less than a maximal set is ever
    /// permissible, because a smaller set is precisely one that could be extended. That
    /// settles doublets — a line playing three of the four numbers strictly contains one
    /// playing two, so the three-number line is compelled — and it settles a throw where only
    /// one die can ever be played.
    /// </para>
    /// <para>
    /// What it does not settle is the map's recorded ambiguity: where either die alone can be
    /// played but not both, the two candidate plays are incomparable, neither can be extended,
    /// and the text gives no rule for choosing. The entry's fate is <c>unresolved</c>, so this
    /// returns <see cref="UnresolvedReason.RequiresInterpretation"/> rather than silently
    /// adopting the modern convention of compelling the higher die.
    /// </para>
    /// <para>
    /// <b>A second decline, not this rule's.</b> Where a line of the throw reaches a position in
    /// which <see cref="BearingOff.HasReEnteredMidBearOff"/> holds with a number still to play
    /// -- from the start of the throw, or after entering the man that was hit -- which rules
    /// govern that number turns on whether bearing off lasts once a man is hit and re-enters,
    /// and the corpus does not say. That is <see cref="MapEntries.BearingOffEligible"/>'s
    /// question (<c>fate: unresolved</c> since <c>RulesFactory.Maps.HoyleBackgammon</c> 3.0.0,
    /// blind-mapping resolution rows 12 and 13), so the throw returns
    /// <see cref="UnresolvedReason.RequiresInterpretation"/> citing that entry, and is decided
    /// before any question of which plays are compelled.
    /// </para>
    /// <para>
    /// A man up does not lift the compulsion. The map records no <c>enter-from-bar</c>
    /// suspension of <c>must-play-whole-throw</c> since 3.0.0 (row 66): the lines searched below
    /// begin with entry whenever a man is up, because <see cref="Movement.MovesForDie"/> offers
    /// nothing else, and are held to the same maximal filter as any other.
    /// </para>
    /// <para>
    /// <b>The enumeration contract.</b> <see cref="Game.Play"/> records a choice as an index
    /// into this list, so its length and order are part of replay as much as the dice are,
    /// and changing either changes what every recorded index means. They are fixed by, and
    /// only by, the following:
    /// </para>
    /// <list type="number">
    /// <item>The distinct numbers of the entitlement are tried highest first
    /// (<see cref="Movement.Entitlement"/> already orders a throw higher first; this sorts
    /// again, so the order does not depend on the caller).</item>
    /// <item>For each number, moves come in <see cref="Movement.MovesForDie"/>'s order —
    /// highest origin first.</item>
    /// <item>The search is depth-first: at each step every number still available is tried in
    /// that order, and each move is followed to the end before the next is tried. A line is
    /// appended when it cannot be extended, so the list is in depth-first pre-order of
    /// lines.</item>
    /// <item>A memo on (position reached, how many of each number used) prunes any line that
    /// reaches a state an earlier line already reached. Of several orderings of moves that
    /// arrive at the same state, <em>only the first enumerated survives</em>; this is what
    /// fixes the length, and it is why rules 1–3 decide which moves are written down.</item>
    /// <item>Lines not using the compelled numbers are then removed, keeping the order of the
    /// rest.</item>
    /// </list>
    /// <para>
    /// No hash-set or dictionary iteration order reaches the list. <c>PlayEnumerationTests</c>
    /// pins the result for six-trois and double deuces from the starting position.
    /// </para>
    /// </remarks>
    /// <param name="position">The position before the throw is played.</param>
    /// <param name="player">The player to move.</param>
    /// <param name="entitlement">The numbers the throw entitles, from <see cref="Movement.Entitlement"/>.</param>
    /// <returns>
    /// The plays, in a fixed enumeration order, never empty: a throw with nothing playable
    /// yields exactly one play, which moves no man.
    /// </returns>
    public static Resolution<ImmutableArray<Play>> For(
        Position position, Player player, ImmutableArray<int> entitlement)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (entitlement.IsDefaultOrEmpty)
        {
            throw new ArgumentException("a throw entitles at least one number.", nameof(entitlement));
        }

        var values = entitlement.Distinct().OrderDescending().ToImmutableArray();
        var available = new int[values.Length];
        foreach (int pip in entitlement)
        {
            available[values.IndexOf(pip)]++;
        }

        var terminals = new List<Terminal>();
        var seen = new HashSet<Node>();
        var search = new Search();
        Explore(position, player, values, available, new int[values.Length], [], terminals, seen, search);

        if (search.ReachedReEntryMidBearOff)
        {
            return Resolution<ImmutableArray<Play>>.FromUnresolved(new UnresolvedResult(
                UnresolvedReason.RequiresInterpretation,
                "play a number for a player who had begun to bear off and whose man, hit, has "
                + "re-entered: whether he may go on bearing off the men still at home",
                MapEntries.BearingOffEligible.Locator));
        }

        var maximal = terminals
            .Select(t => t.Used)
            .Distinct(UsedComparer.Instance)
            .Where(used => !terminals.Any(other => StrictlyContains(other.Used, used)))
            .ToList();

        if (maximal.Count > 1)
        {
            return Resolution<ImmutableArray<Play>>.FromUnresolved(new UnresolvedResult(
                UnresolvedReason.RequiresInterpretation,
                "choose between plays that use incomparable parts of a throw that cannot be "
                + "played whole",
                MapEntries.MustPlayWholeThrow.Locator));
        }

        var compelled = maximal[0];
        var plays = terminals
            .Where(t => UsedComparer.Instance.Equals(t.Used, compelled))
            .Select(t => new Play(t.Moves, t.Position))
            .ToImmutableArray();

        return Resolution<ImmutableArray<Play>>.FromValue(plays);
    }

    private static bool StrictlyContains(int[] candidate, int[] used)
    {
        bool strictly = false;
        for (int i = 0; i < used.Length; i++)
        {
            if (candidate[i] < used[i])
            {
                return false;
            }

            strictly |= candidate[i] > used[i];
        }

        return strictly;
    }

    private static void Explore(
        Position position,
        Player player,
        ImmutableArray<int> values,
        int[] remaining,
        int[] used,
        ImmutableArray<Move> moves,
        List<Terminal> terminals,
        HashSet<Node> seen,
        Search search)
    {
        if (search.ReachedReEntryMidBearOff || !seen.Add(new Node(position, (int[])used.Clone())))
        {
            return;
        }

        if (remaining.Any(r => r > 0) && BearingOff.HasReEnteredMidBearOff(position, player))
        {
            search.ReachedReEntryMidBearOff = true;
            return;
        }

        bool extended = false;
        for (int i = 0; i < values.Length; i++)
        {
            if (remaining[i] == 0)
            {
                continue;
            }

            foreach (var move in Movement.MovesForDie(position, player, values[i]))
            {
                extended = true;
                remaining[i]--;
                used[i]++;
                Explore(
                    position.Apply(player, move.From, move.To),
                    player,
                    values,
                    remaining,
                    used,
                    moves.Add(move),
                    terminals,
                    seen,
                    search);
                used[i]--;
                remaining[i]++;
            }
        }

        if (!extended)
        {
            terminals.Add(new Terminal((int[])used.Clone(), moves, position));
        }
    }

    private sealed class Search
    {
        public bool ReachedReEntryMidBearOff { get; set; }
    }

    private sealed record Terminal(int[] Used, ImmutableArray<Move> Moves, Position Position);

    private sealed record Node(Position Position, int[] Used)
    {
        public bool Equals(Node? other) =>
            other is not null
            && Position.Equals(other.Position)
            && Used.AsSpan().SequenceEqual(other.Used.AsSpan());

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            hash.Add(Position);
            foreach (int u in Used)
            {
                hash.Add(u);
            }

            return hash.ToHashCode();
        }
    }

    private sealed class UsedComparer : IEqualityComparer<int[]>
    {
        public static readonly UsedComparer Instance = new();

        public bool Equals(int[]? x, int[]? y) =>
            x is not null && y is not null && x.AsSpan().SequenceEqual(y.AsSpan());

        public int GetHashCode(int[] obj)
        {
            var hash = default(HashCode);
            foreach (int value in obj)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }
    }
}
