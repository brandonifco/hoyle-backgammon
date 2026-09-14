using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace HoyleBackgammon;

/// <summary>
/// Where every man stands. Immutable; each move produces a new position.
/// </summary>
/// <remarks>
/// Held as two arrays of counts, one per player, each indexed by that player's own pip
/// numbering (see <see cref="Geometry"/>): index 0 is men borne off, 1 through 24 the points,
/// and 25 the bar. The same physical point is index <c>p</c> for one player and
/// <see cref="Geometry.Mirror"/> of <c>p</c> for the other.
/// <para>
/// Nothing in this type knows the starting arrangement. The corpus's is declined
/// (<c>starting-position</c>), so a position only ever comes from a caller — see
/// <see cref="AssertedPosition"/>.
/// </para>
/// </remarks>
public sealed class Position : IEquatable<Position>
{
    private const int SlotCount = Geometry.BarPip + 1;

    private readonly ImmutableArray<byte> _white;
    private readonly ImmutableArray<byte> _black;

    private Position(ImmutableArray<byte> white, ImmutableArray<byte> black)
    {
        _white = white;
        _black = black;
    }

    /// <summary>
    /// Builds a position from each player's men, keyed by that player's own pip numbering
    /// (0 borne off, 1-24 the points, 25 the bar).
    /// </summary>
    /// <remarks>
    /// Enforces <see cref="MapEntries.MenCount"/> — fifteen men a side, thirty in all — and
    /// the geometric fact that one point cannot hold men of both players, which is
    /// <see cref="MapEntries.LegalDestination"/> read as an invariant rather than as a test
    /// on a move.
    /// </remarks>
    /// <exception cref="ArgumentException">If either side does not hold exactly fifteen men,
    /// or if a point holds men of both players.</exception>
    public static Position Create(
        IReadOnlyDictionary<int, int> white, IReadOnlyDictionary<int, int> black)
    {
        ArgumentNullException.ThrowIfNull(white);
        ArgumentNullException.ThrowIfNull(black);

        var w = ToSlots(white, nameof(white));
        var b = ToSlots(black, nameof(black));

        for (int pip = 1; pip <= Geometry.PointCount; pip++)
        {
            if (w[pip] > 0 && b[Geometry.Mirror(pip)] > 0)
            {
                throw new ArgumentException(
                    $"both players hold men on {Geometry.NameOf(pip)}; one point cannot be "
                    + "occupied by men of both sides.",
                    nameof(white));
            }
        }

        return new Position([.. w], [.. b]);
    }

    private static byte[] ToSlots(IReadOnlyDictionary<int, int> men, string parameterName)
    {
        var slots = new byte[SlotCount];
        int total = 0;
        foreach (var (pip, count) in men)
        {
            if (pip is < 0 or > Geometry.BarPip)
            {
                throw new ArgumentException(
                    $"pip {pip} is neither a point (1-{Geometry.PointCount}), the bar "
                    + $"({Geometry.BarPip}), nor borne off (0).",
                    parameterName);
            }

            if (count < 0)
            {
                throw new ArgumentException($"pip {pip} has a negative count.", parameterName);
            }

            slots[pip] = checked((byte)(slots[pip] + count));
            total += count;
        }

        // men-count: "thirty 'men,' fifteen white and fifteen black (or red)".
        if (total != MenPerPlayer)
        {
            throw new ArgumentException(
                $"{parameterName} holds {total} men; {MapEntries.MenCount.Id} requires exactly "
                + $"{MenPerPlayer} a side [{MapEntries.MenCount.Locator}].",
                parameterName);
        }

        return slots;
    }

    /// <summary>
    /// Fifteen. <see cref="MapEntries.MenCount"/>: "thirty 'men,' fifteen white and fifteen
    /// black (or red)".
    /// </summary>
    public const int MenPerPlayer = 15;

    /// <summary>Thirty. <see cref="MapEntries.MenCount"/>.</summary>
    public const int TotalMen = MenPerPlayer * 2;

    private ImmutableArray<byte> Slots(Player player) =>
        player == Player.White ? _white : _black;

    /// <summary>How many of <paramref name="player"/>'s men stand on his pip <paramref name="pip"/>.</summary>
    public int Men(Player player, int pip)
    {
        if (pip is < 0 or > Geometry.BarPip)
        {
            throw new ArgumentOutOfRangeException(nameof(pip), pip, "not a slot.");
        }

        return Slots(player)[pip];
    }

    /// <summary>
    /// How many of the adversary's men stand on the point <paramref name="player"/> calls
    /// <paramref name="pip"/>.
    /// </summary>
    public int AdversaryMen(Player player, int pip) =>
        Geometry.IsPoint(pip) ? Slots(player.Adversary())[Geometry.Mirror(pip)] : 0;

    /// <summary>How many of <paramref name="player"/>'s men are on the bar.</summary>
    public int OnBar(Player player) => Men(player, Geometry.BarPip);

    /// <summary>How many of <paramref name="player"/>'s men have been borne off.</summary>
    public int BorneOff(Player player) => Men(player, Geometry.BorneOffPip);

    /// <summary>
    /// True when <paramref name="player"/> has two or more men on <paramref name="pip"/> —
    /// he has "made" the point. <see cref="MapEntries.MadePoint"/>.
    /// </summary>
    public bool HasMadePoint(Player player, int pip) => Men(player, pip) >= 2;

    /// <summary>
    /// True when <paramref name="player"/> has exactly one man on <paramref name="pip"/> — a
    /// blot. <see cref="MapEntries.BlotHit"/>.
    /// </summary>
    public bool HasBlot(Player player, int pip) => Men(player, pip) == 1;

    /// <summary>
    /// The result of moving one of <paramref name="player"/>'s men from <paramref name="from"/>
    /// to <paramref name="to"/>, taking up an adversary blot on the destination.
    /// </summary>
    /// <remarks>
    /// Applies <see cref="MapEntries.BlotHit"/>: a lone adversary man on the destination is
    /// "taken up (placed on the bar between the two tables)". This method does not ask whether
    /// the move is legal; that is <see cref="Movement"/>'s and <see cref="BearingOff"/>'s work.
    /// </remarks>
    public Position Apply(Player player, int from, int to)
    {
        if (Men(player, from) == 0)
        {
            throw new InvalidOperationException($"no man of {player}'s stands on pip {from}.");
        }

        int landing = Math.Max(to, Geometry.BorneOffPip);
        var mine = Slots(player).ToBuilder();
        var theirs = Slots(player.Adversary()).ToBuilder();

        mine[from]--;
        mine[landing]++;

        if (Geometry.IsPoint(landing) && theirs[Geometry.Mirror(landing)] == 1)
        {
            theirs[Geometry.Mirror(landing)] = 0;
            theirs[Geometry.BarPip]++;
        }

        return player == Player.White
            ? new Position(mine.ToImmutable(), theirs.ToImmutable())
            : new Position(theirs.ToImmutable(), mine.ToImmutable());
    }

    /// <summary>The pips on which <paramref name="player"/> has at least one man, highest first.</summary>
    public IEnumerable<int> OccupiedPoints(Player player)
    {
        for (int pip = Geometry.PointCount; pip >= 1; pip--)
        {
            if (Men(player, pip) > 0)
            {
                yield return pip;
            }
        }
    }

    /// <inheritdoc/>
    public bool Equals(Position? other) =>
        other is not null
        && _white.AsSpan().SequenceEqual(other._white.AsSpan())
        && _black.AsSpan().SequenceEqual(other._black.AsSpan());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Position);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (byte b in _white)
        {
            hash.Add(b);
        }

        foreach (byte b in _black)
        {
            hash.Add(b);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// A stable, replayable rendering: White's men by his own pips, then Black's. Used in
    /// tests as the evidence that two runs produced the same game, not merely the same winner.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        foreach (var player in Players.Both)
        {
            if (text.Length > 0)
            {
                text.Append(" | ");
            }

            text.Append(player == Player.White ? "W" : "B").Append(':');
            for (int pip = Geometry.BarPip; pip >= 0; pip--)
            {
                int men = Men(player, pip);
                if (men == 0)
                {
                    continue;
                }

                string slot = pip switch
                {
                    Geometry.BarPip => "bar",
                    Geometry.BorneOffPip => "off",
                    _ => pip.ToString(CultureInfo.InvariantCulture),
                };
                text.Append(CultureInfo.InvariantCulture, $" {slot}={men}");
            }
        }

        return text.ToString();
    }
}
