using RulesKernel.Provenance;

namespace HoyleBackgammon;

/// <summary>
/// Passages this engine had to implement that <c>corpus-map.json</c> has no entry for.
/// </summary>
/// <remarks>
/// These are not declines and they are not omissions the engine invented around: the corpus
/// states them, and the engine cannot compute anything without them. They are collected here
/// so that "which rules are implemented without a map entry" is a query against one file
/// rather than a re-read of the engine. Each is a finding in <c>MAP-FINDINGS.md</c>.
/// <para>
/// The correspondence table in the factory's <c>docs/corpus-map.md</c> says an unresolved
/// result with no map entry means something was implemented without being mapped. This class
/// is the other half of that invariant: a <em>resolved</em> result with no map entry.
/// </para>
/// </remarks>
public static class UnmappedRules
{
    /// <summary>
    /// The twenty-four points, their names, and the two opposite numbering directions.
    /// Finding 2 in <c>MAP-FINDINGS.md</c>.
    /// </summary>
    /// <remarks>
    /// "Each table is marked with twelve 'points,' six at either end. ... The two points in
    /// the inner table farthest from the dividing partition or 'bar' are known as the 'ace'
    /// points ... The points in the outer tables are designated in like manner, but starting
    /// in this case from the dividing partition."
    /// <para>
    /// <c>board-tables</c> stops at "the board is two tables". Nothing in the map states that
    /// there are twenty-four points, that they are named ace through six twice over, or that
    /// the inner and outer tables number in opposite directions. Without all three, the
    /// starting arrangement, the bearing-off worked example and every citation that says
    /// "the cinque point" cannot be read.
    /// </para>
    /// </remarks>
    public static SourceLocator PointDesignations { get; } =
        new(MapEntries.SourceId, "BACKGAMMON / The Board and Men / pp. 271-272");

    /// <summary>
    /// The direction of travel, and hence which end of the journey each point sits at.
    /// Finding 3 in <c>MAP-FINDINGS.md</c>.
    /// </summary>
    /// <remarks>
    /// "The movement of the men of each player is from the ace point in his opponent's home
    /// table towards the like point in his own ... the object of the game being first to get
    /// all the player's men into his own inner table, and then to play them out of it again."
    /// <para>
    /// <c>move-by-pip</c> covers the distance a die moves a man. It does not state which way
    /// a man moves, and no entry does. This sentence is also the sole authority for treating
    /// the twenty-four points as one linear course.
    /// </para>
    /// </remarks>
    public static SourceLocator DirectionOfTravel { get; } =
        new(MapEntries.SourceId, "BACKGAMMON / Playing / p. 273");
}
