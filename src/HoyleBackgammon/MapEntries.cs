using RulesKernel.Identity;
using RulesKernel.Provenance;

namespace HoyleBackgammon;

/// <summary>
/// The thirty entries of <c>corpus-map.json</c>, one static per entry, with the
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
/// <para>
/// A twenty-ninth followed from <c>rules-factory/docs/decisions/0005</c>: a judgement the
/// corpus deliberately delegates is an assertion and an entry of its own, so
/// <c>agreed-backgammon-multiple</c> was split out of <c>stake-multiplier</c>. The same
/// migration recorded the conflict <c>rules-factory/docs/decisions/0006</c> settles, on
/// <c>legal-destination</c>, <c>enter-from-bar</c> and <c>full-table-suspension</c>.
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

    // Each section runs across several pages and the map cites each rule to the page it is
    // stated on. One constant a page, not one a section: the correspondence check in
    // scripts/validate.sh compares these strings with the map's.
    private const string BoardAndMen271 = "BACKGAMMON / The Board and Men / p. 271";
    private const string BoardAndMen272 = "BACKGAMMON / The Board and Men / p. 272";
    private const string BoardAndMen273 = "BACKGAMMON / The Board and Men / p. 273";
    private const string Playing273 = "BACKGAMMON / Playing / p. 273";
    private const string Playing274 = "BACKGAMMON / Playing / p. 274";
    private const string Playing275 = "BACKGAMMON / Playing / p. 275";
    private const string BearingOff275 = "BACKGAMMON / Bearing off the Men / p. 275";
    private const string BearingOff276 = "BACKGAMMON / Bearing off the Men / p. 276";
    private const string BearingOff277 = "BACKGAMMON / Bearing off the Men / p. 277";
    private const string HintsForPlay277 = "BACKGAMMON / Hints for Play / p. 277";

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
        Entry("opening-roll", "Deciding who begins", Playing273);

    /// <summary>The opening thrower may keep the throw or throw again.</summary>
    public static MapEntry OpeningThrowerOption { get; } =
        Entry("opening-thrower-option", "The opening thrower may keep the throw or throw again", Playing273);

    /// <summary>All subsequent throws use both dice.</summary>
    public static MapEntry ThrowTwoDice { get; } =
        Entry("throw-two-dice", "All subsequent throws use both dice", Playing273);

    /// <summary>Each die moves one man that many points.</summary>
    public static MapEntry MoveByPip { get; } =
        Entry("move-by-pip", "Each die moves one man that many points", Playing273);

    /// <summary>The twenty-four points are one course with two ends.</summary>
    public static MapEntry DirectionOfTravel { get; } =
        Entry(
            "direction-of-travel",
            "The twenty-four points are one course with two ends",
            Playing273);

    /// <summary>Doublets are played twice over.</summary>
    public static MapEntry Doublets { get; } =
        Entry("doublets", "Doublets are played twice over", Playing274);

    /// <summary>A man may be played only to a permitted point.</summary>
    public static MapEntry LegalDestination { get; } =
        Entry("legal-destination", "A man may be played only to a permitted point", Playing274);

    /// <summary>Two men make a point.</summary>
    public static MapEntry MadePoint { get; } =
        Entry("made-point", "Two men make a point", Playing274);

    /// <summary>A single man is a blot and may be hit.</summary>
    public static MapEntry BlotHit { get; } =
        Entry("blot-hit", "A single man is a blot and may be hit", Playing274);

    /// <summary>A man on the bar re-enters before any other man moves.</summary>
    public static MapEntry EnterFromBar { get; } =
        Entry("enter-from-bar", "A man on the bar re-enters before any other man moves", Playing274);

    /// <summary>Play is wholly suspended against a full home table.</summary>
    public static MapEntry FullTableSuspension { get; } =
        Entry("full-table-suspension", "Play is wholly suspended against a full home table", Playing274);

    /// <summary>The whole throw must be played if it can be. Ambiguous; fate unresolved.</summary>
    public static MapEntry MustPlayWholeThrow { get; } =
        Entry("must-play-whole-throw", "The whole throw must be played if it can be", Playing275);

    /// <summary>Bearing off begins when all men are home.</summary>
    public static MapEntry BearingOffEligible { get; } =
        Entry("bearing-off-eligible", "Bearing off begins when all men are home", BearingOff275);

    /// <summary>Each throw may move within the table or remove a man.</summary>
    public static MapEntry BearingOffMoveOrRemove { get; } =
        Entry("bearing-off-move-or-remove", "Each throw may move within the table or remove a man", BearingOff275);

    /// <summary>An unusable number bears off from the highest occupied point.</summary>
    public static MapEntry BearingOffHighest { get; } =
        Entry("bearing-off-highest", "An unusable number bears off from the highest occupied point", BearingOff276);

    /// <summary>Doublets bear off or move, or both.</summary>
    public static MapEntry BearingOffDoublets { get; } =
        Entry("bearing-off-doublets", "Doublets bear off or move, or both", BearingOff276);

    /// <summary>First to remove all men wins.</summary>
    public static MapEntry WinCondition { get; } =
        Entry("win-condition", "First to remove all men wins", BearingOff276);

    /// <summary>
    /// A win is a hit, a gammon, or a backgammon. Ambiguous: the three named results do not
    /// cover every finish, and the fate of the one they miss is unresolved.
    /// </summary>
    public static MapEntry GameValue { get; } =
        Entry("game-value", "A win is a hit, a gammon, or a backgammon", BearingOff276);

    /// <summary>
    /// The multiple the players agreed a backgammon pays. An assertion: the corpus names the
    /// decider and bounds the figure, so the engine demands it rather than declining.
    /// </summary>
    public static MapEntry AgreedBackgammonMultiple { get; } =
        Entry(
            "agreed-backgammon-multiple",
            "The multiple the players agreed a backgammon pays",
            BearingOff277);

    /// <summary>What each result pays. Clear: the delegated figure is its own entry.</summary>
    public static MapEntry StakeMultiplier { get; } =
        Entry("stake-multiplier", "What each result pays", BearingOff276);

    /// <summary>Who throws first in the following game.</summary>
    public static MapEntry NextGameOpening { get; } =
        Entry("next-game-opening", "Who throws first in the following game", BearingOff277);

    /// <summary>
    /// The faces a die bears, and the throws a pair of them can show. The corpus never writes
    /// "six" of a die; it names all twenty-one throws of a pair and calls them all the possible
    /// throws, which fixes six faces. Stated in a section <see cref="StrategyAdvice"/> declines.
    /// </summary>
    public static MapEntry DieFaces { get; } =
        Entry(
            "die-faces",
            "The faces a die bears, and the throws a pair of them can show",
            HintsForPlay277);

    /// <summary>Doubling the stake during play. Declined: out of scope, absent from a 1909 corpus.</summary>
    public static MapEntry DoublingCube { get; } =
        Entry("doubling-cube", "Doubling the stake during play", BoardAndMen272);

    /// <summary>The advisory principles of play. Declined: out of scope, non-normative.</summary>
    public static MapEntry StrategyAdvice { get; } =
        Entry("strategy-advice", "The advisory principles of play", HintsForPlay277);
}
