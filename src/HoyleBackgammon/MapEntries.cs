using RulesKernel.Identity;
using RulesKernel.Provenance;

namespace HoyleBackgammon;

/// <summary>
/// The twenty-eight entries of <c>corpus-map.json</c>, one static per entry, with the
/// citations copied verbatim from the map.
/// </summary>
/// <remarks>
/// This class is the join between the map and the code. Nothing here paraphrases a rule; it
/// only says where each rule is written. Where implementing an entry showed the map to be
/// wrong, the finding is in <c>MAP-FINDINGS.md</c> and is cross-referenced from the rule,
/// never silently corrected here.
/// <para>
/// The map this engine now cites is the corrected one (factory commit <c>de59930</c>), which
/// was written in answer to findings 1, 2, 3 and 5 of this build. Four entries are new here
/// — <c>player-count</c>, <c>point-designations</c>, <c>direction-of-travel</c> and
/// <c>inner-table-handedness</c> — and <c>starting-position</c> is no longer declined. See
/// <c>docs/decisions/0003</c>.
/// </para>
/// </remarks>
public static class MapEntries
{
    /// <summary>The corpus id all entries cite.</summary>
    public const string SourceId = "hoyle-1909";

    /// <summary>
    /// The pinned baseline: the whole Project Gutenberg text including its licence header
    /// and footer, which is what <c>corpus/hoyle.txt</c> in this repository contains and
    /// what <c>scripts/validate.sh</c> re-checks on every run.
    /// </summary>
    public static SourceBaselineId Baseline { get; } = new(
        sourceId: SourceId,
        contentHash: "5d505fa9f6202340eb55313b8ef607b816087a860d3d51b1bf92b5f65240645e",
        hashDerivation: "gutenberg-plain-text-including-boilerplate");

    // The Board and Men runs across three pages and the map cites each rule to the page it is
    // stated on. One constant a page, not one a section: the correspondence check in
    // scripts/validate.sh compares these strings with the map's.
    private const string BoardAndMen271 = "BACKGAMMON / The Board and Men / p. 271";
    private const string BoardAndMen272 = "BACKGAMMON / The Board and Men / p. 272";
    private const string BoardAndMen273 = "BACKGAMMON / The Board and Men / p. 273";
    private const string Playing = "BACKGAMMON / Playing / p. 273";
    private const string BearingOff = "BACKGAMMON / Bearing off the Men / p. 275";

    private static MapEntry Entry(string id, string name, string citation) =>
        new(id, name, new SourceLocator(SourceId, citation));

    /// <summary>Backgammon is played by two persons.</summary>
    public static MapEntry PlayerCount { get; } =
        Entry("player-count", "Backgammon is played by two persons", BoardAndMen271);

    /// <summary>Thirty men, fifteen to a side.</summary>
    public static MapEntry MenCount { get; } =
        Entry("men-count", "Thirty men, fifteen to a side", BoardAndMen271);

    /// <summary>The board is two tables, inner and outer.</summary>
    public static MapEntry BoardTables { get; } =
        Entry("board-tables", "The board is two tables, inner and outer", BoardAndMen272);

    /// <summary>How the twenty-four points are named and numbered.</summary>
    public static MapEntry PointDesignations { get; } =
        Entry(
            "point-designations",
            "How the twenty-four points are named and numbered",
            BoardAndMen272);

    /// <summary>
    /// Which physical compartment of the board is the inner table. Declined: beyond the
    /// plain-text adapter, and nothing in this engine asks. See <see cref="BeyondAdapter"/>.
    /// </summary>
    public static MapEntry InnerTableHandedness { get; } =
        Entry(
            "inner-table-handedness",
            "Which physical compartment of the board is the inner table",
            BoardAndMen272);

    /// <summary>The starting arrangement of the men.</summary>
    public static MapEntry StartingPosition { get; } =
        Entry("starting-position", "The starting arrangement of the men", BoardAndMen273);

    /// <summary>Deciding who begins.</summary>
    public static MapEntry OpeningRoll { get; } =
        Entry("opening-roll", "Deciding who begins", Playing);

    /// <summary>The opening thrower may keep the throw or throw again.</summary>
    public static MapEntry OpeningThrowerOption { get; } =
        Entry("opening-thrower-option", "The opening thrower may keep the throw or throw again", Playing);

    /// <summary>All subsequent throws use both dice.</summary>
    public static MapEntry ThrowTwoDice { get; } =
        Entry("throw-two-dice", "All subsequent throws use both dice", Playing);

    /// <summary>Each die moves one man that many points.</summary>
    public static MapEntry MoveByPip { get; } =
        Entry("move-by-pip", "Each die moves one man that many points", Playing);

    /// <summary>The twenty-four points are one course with two ends.</summary>
    public static MapEntry DirectionOfTravel { get; } =
        Entry(
            "direction-of-travel",
            "The twenty-four points are one course with two ends",
            Playing);

    /// <summary>Doublets are played twice over.</summary>
    public static MapEntry Doublets { get; } =
        Entry("doublets", "Doublets are played twice over", Playing);

    /// <summary>A man may be played only to a permitted point.</summary>
    public static MapEntry LegalDestination { get; } =
        Entry("legal-destination", "A man may be played only to a permitted point", Playing);

    /// <summary>Two men make a point.</summary>
    public static MapEntry MadePoint { get; } =
        Entry("made-point", "Two men make a point", Playing);

    /// <summary>A single man is a blot and may be hit.</summary>
    public static MapEntry BlotHit { get; } =
        Entry("blot-hit", "A single man is a blot and may be hit", Playing);

    /// <summary>A man on the bar re-enters before any other man moves.</summary>
    public static MapEntry EnterFromBar { get; } =
        Entry("enter-from-bar", "A man on the bar re-enters before any other man moves", Playing);

    /// <summary>Play is wholly suspended against a full home table.</summary>
    public static MapEntry FullTableSuspension { get; } =
        Entry("full-table-suspension", "Play is wholly suspended against a full home table", Playing);

    /// <summary>The whole throw must be played if it can be. Ambiguous; fate unresolved.</summary>
    public static MapEntry MustPlayWholeThrow { get; } =
        Entry("must-play-whole-throw", "The whole throw must be played if it can be", Playing);

    /// <summary>Bearing off begins when all men are home.</summary>
    public static MapEntry BearingOffEligible { get; } =
        Entry("bearing-off-eligible", "Bearing off begins when all men are home", BearingOff);

    /// <summary>Each throw may move within the table or remove a man.</summary>
    public static MapEntry BearingOffMoveOrRemove { get; } =
        Entry("bearing-off-move-or-remove", "Each throw may move within the table or remove a man", BearingOff);

    /// <summary>An unusable number bears off from the highest occupied point.</summary>
    public static MapEntry BearingOffHighest { get; } =
        Entry("bearing-off-highest", "An unusable number bears off from the highest occupied point", BearingOff);

    /// <summary>Doublets bear off or move, or both.</summary>
    public static MapEntry BearingOffDoublets { get; } =
        Entry("bearing-off-doublets", "Doublets bear off or move, or both", BearingOff);

    /// <summary>First to remove all men wins.</summary>
    public static MapEntry WinCondition { get; } =
        Entry("win-condition", "First to remove all men wins", BearingOff);

    /// <summary>A win is a hit, a gammon, or a backgammon.</summary>
    public static MapEntry GameValue { get; } =
        Entry("game-value", "A win is a hit, a gammon, or a backgammon", BearingOff);

    /// <summary>What each result pays. Ambiguous for a backgammon; fate unresolved.</summary>
    public static MapEntry StakeMultiplier { get; } =
        Entry("stake-multiplier", "What each result pays", BearingOff);

    /// <summary>Who throws first in the following game.</summary>
    public static MapEntry NextGameOpening { get; } =
        Entry("next-game-opening", "Who throws first in the following game", BearingOff);

    /// <summary>Doubling. Declined: out of scope, absent from a 1909 corpus.</summary>
    public static MapEntry DoublingCube { get; } =
        Entry("doubling-cube", "Doubling", "(absent)");

    /// <summary>Opening play advice. Declined: out of scope, non-normative.</summary>
    public static MapEntry StrategyAdvice { get; } =
        Entry("strategy-advice", "Opening play advice", "BACKGAMMON / Hints for Play / p. 277");
}
