namespace HoyleBackgammon;

/// <summary>Which of the four quarters of the board a point sits in, relative to one player.</summary>
public enum Quarter
{
    /// <summary>The player's own inner (home) table: his pips 1 through 6.</summary>
    OwnInner,

    /// <summary>The player's own outer table: his pips 7 through 12.</summary>
    OwnOuter,

    /// <summary>The adversary's outer table: his pips 13 through 18.</summary>
    AdversaryOuter,

    /// <summary>The adversary's inner (home) table: his pips 19 through 24.</summary>
    AdversaryInner,
}

/// <summary>
/// The board's fixed geometry: twenty-four points read as one course per player.
/// </summary>
/// <remarks>
/// <para>
/// <b>The representation.</b> A point is named by the number of pips a man standing on it
/// still has to travel, counted for the player who owns the man: 24 at the start of his
/// journey, 1 on his own ace point, 0 once borne off. The bar is pip 25, so entering with a
/// die of <c>f</c> lands on pip <c>25 - f</c> and every move — entry, ordinary play and
/// bearing off alike — is <c>to = from - die</c>. This is a representation, not a rule.
/// </para>
/// <para>
/// <b>What the corpus fixes and what it does not.</b> The two ends of the course are stated
/// (<see cref="UnmappedRules.DirectionOfTravel"/>): a man begins on the ace point of the
/// adversary's home table and finishes on the like point of his own. The four quarters
/// follow from the point designations (<see cref="UnmappedRules.PointDesignations"/>): inner
/// tables number from the far end inward, so the adversary's ace point is his inner table's
/// farthest point (pip 24) and his six point is next the bar (pip 19); outer tables number
/// from the bar outward, so the adversary's outer bar point is pip 18 and his outer six point
/// pip 13, which adjoins the player's own outer six point (pip 12), and the player's own
/// outer bar point (pip 7) adjoins his own six point (pip 6) across the bar.
/// </para>
/// <para>
/// What the corpus does <em>not</em> fix, for lack of a readable Fig. 1, is which physical
/// compartment is the inner table — the text says only that with the men placed as in Fig. 1
/// the right hand is the inner table. This engine never needs to know: every position it
/// holds is expressed in the player-relative pips above. See <c>board-tables</c> in
/// <c>MAP-FINDINGS.md</c>.
/// </para>
/// </remarks>
public static class Geometry
{
    /// <summary>The number of points on the board.</summary>
    public const int PointCount = 24;

    /// <summary>The pip a man on the bar occupies, so that entry is an ordinary move.</summary>
    public const int BarPip = 25;

    /// <summary>The highest pip of a player's own inner (home) table.</summary>
    public const int HomeTableHighestPip = 6;

    /// <summary>The pip a man reaches when it is borne off.</summary>
    public const int BorneOffPip = 0;

    /// <summary>
    /// The same physical point, named from the other player's end of the course. Implements
    /// <see cref="UnmappedRules.DirectionOfTravel"/>: the two courses run in opposite
    /// directions over one set of twenty-four points, so the pips at a point sum to 25.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pip"/> is not a point.</exception>
    public static int Mirror(int pip)
    {
        ThrowIfNotAPoint(pip);
        return PointCount + 1 - pip;
    }

    /// <summary>
    /// True if <paramref name="pip"/> names one of the twenty-four points. The bar (25) and
    /// borne off (0) are states a man can be in, not points.
    /// </summary>
    public static bool IsPoint(int pip) => pip is >= 1 and <= PointCount;

    /// <summary>Which quarter of the board a pip sits in, for the player whose pips these are.</summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pip"/> is not a point.</exception>
    public static Quarter QuarterOf(int pip)
    {
        ThrowIfNotAPoint(pip);
        return pip switch
        {
            <= 6 => Quarter.OwnInner,
            <= 12 => Quarter.OwnOuter,
            <= 18 => Quarter.AdversaryOuter,
            _ => Quarter.AdversaryInner,
        };
    }

    /// <summary>
    /// The point's name in Hoyle's vocabulary — "cinque point", "bar point" and so on —
    /// qualified by whose table it is in. Implements
    /// <see cref="UnmappedRules.PointDesignations"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="pip"/> is not a point.</exception>
    public static string NameOf(int pip)
    {
        var quarter = QuarterOf(pip);

        // Inner tables number from the end of the board inward (ace farthest from the bar,
        // six next the bar); outer tables number from the bar outward, and their ace point is
        // called the bar point.
        int ordinal = quarter switch
        {
            Quarter.OwnInner => pip,
            Quarter.OwnOuter => pip - 6,
            Quarter.AdversaryOuter => 19 - pip,
            _ => 25 - pip,
        };

        bool outer = quarter is Quarter.OwnOuter or Quarter.AdversaryOuter;
        string designation = ordinal switch
        {
            1 => outer ? "bar" : "ace",
            2 => "deuce",
            3 => "trois",
            4 => "quatre",
            5 => "cinque",
            _ => "six",
        };

        string table = quarter switch
        {
            Quarter.OwnInner => "his inner table",
            Quarter.OwnOuter => "his outer table",
            Quarter.AdversaryOuter => "the adversary's outer table",
            _ => "the adversary's inner table",
        };

        return $"the {designation} point in {table}";
    }

    private static void ThrowIfNotAPoint(int pip)
    {
        if (!IsPoint(pip))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pip), pip, $"pip must name one of the {PointCount} points.");
        }
    }
}
