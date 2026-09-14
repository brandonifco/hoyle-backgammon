using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// The point designations, which the map has no entry for (finding 2), read back in full.
/// A finite table is evidenced whole, not sampled.
/// </summary>
public class GeometryTests
{
    [Theory]
    // The player's own inner table: ace point farthest from the bar, six point next the bar.
    [InlineData(1, "the ace point in his inner table")]
    [InlineData(2, "the deuce point in his inner table")]
    [InlineData(3, "the trois point in his inner table")]
    [InlineData(4, "the quatre point in his inner table")]
    [InlineData(5, "the cinque point in his inner table")]
    [InlineData(6, "the six point in his inner table")]
    // His own outer table, numbered from the bar outward; its ace point is the bar point.
    [InlineData(7, "the bar point in his outer table")]
    [InlineData(8, "the deuce point in his outer table")]
    [InlineData(9, "the trois point in his outer table")]
    [InlineData(10, "the quatre point in his outer table")]
    [InlineData(11, "the cinque point in his outer table")]
    [InlineData(12, "the six point in his outer table")]
    // The adversary's outer table, likewise numbered from the bar outward.
    [InlineData(13, "the six point in the adversary's outer table")]
    [InlineData(14, "the cinque point in the adversary's outer table")]
    [InlineData(15, "the quatre point in the adversary's outer table")]
    [InlineData(16, "the trois point in the adversary's outer table")]
    [InlineData(17, "the deuce point in the adversary's outer table")]
    [InlineData(18, "the bar point in the adversary's outer table")]
    // The adversary's inner table, numbered from its far end inward.
    [InlineData(19, "the six point in the adversary's inner table")]
    [InlineData(20, "the cinque point in the adversary's inner table")]
    [InlineData(21, "the quatre point in the adversary's inner table")]
    [InlineData(22, "the trois point in the adversary's inner table")]
    [InlineData(23, "the deuce point in the adversary's inner table")]
    [InlineData(24, "the ace point in the adversary's inner table")]
    public void Every_point_has_its_corpus_name(int pip, string name) =>
        Assert.Equal(name, Geometry.NameOf(pip));

    [Fact]
    public void The_two_courses_run_opposite_over_one_set_of_points()
    {
        for (int pip = 1; pip <= Geometry.PointCount; pip++)
        {
            Assert.Equal(pip, Geometry.Mirror(Geometry.Mirror(pip)));
            Assert.Equal(25, pip + Geometry.Mirror(pip));
        }
    }

    [Fact]
    public void A_mans_own_ace_point_faces_the_adversarys_ace_point()
    {
        // "The movement of the men of each player is from the ace point in his opponent's
        // home table towards the like point in his own."
        Assert.Equal("the ace point in the adversary's inner table", Geometry.NameOf(24));
        Assert.Equal("the ace point in his inner table", Geometry.NameOf(Geometry.Mirror(24)));
    }

    [Theory]
    [InlineData(1, Quarter.OwnInner)]
    [InlineData(6, Quarter.OwnInner)]
    [InlineData(7, Quarter.OwnOuter)]
    [InlineData(12, Quarter.OwnOuter)]
    [InlineData(13, Quarter.AdversaryOuter)]
    [InlineData(18, Quarter.AdversaryOuter)]
    [InlineData(19, Quarter.AdversaryInner)]
    [InlineData(24, Quarter.AdversaryInner)]
    public void Each_quarter_holds_six_points(int pip, Quarter quarter) =>
        Assert.Equal(quarter, Geometry.QuarterOf(pip));

    [Fact]
    public void Each_table_is_marked_with_twelve_points_six_at_either_end()
    {
        var counts = Enumerable.Range(1, Geometry.PointCount)
            .GroupBy(Geometry.QuarterOf)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, counts.Count);
        Assert.All(counts.Values, count => Assert.Equal(6, count));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void The_bar_and_the_borne_off_are_not_points(int pip)
    {
        Assert.False(Geometry.IsPoint(pip));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry.NameOf(pip));
    }
}
