using Xunit;

namespace HoyleBackgammon.Tests;

public class PositionTests
{
    [Fact]
    public void Thirty_men_fifteen_to_a_side()
    {
        Assert.Equal(15, Position.MenPerPlayer);
        Assert.Equal(30, Position.TotalMen);

        var start = Corpus.StartingPosition.Position;
        foreach (var player in Players.Both)
        {
            int total = 0;
            for (int pip = 0; pip <= Geometry.BarPip; pip++)
            {
                total += start.Men(player, pip);
            }

            Assert.Equal(Position.MenPerPlayer, total);
        }
    }

    [Theory]
    [InlineData(14)]
    [InlineData(16)]
    public void A_side_with_the_wrong_number_of_men_is_refused(int men)
    {
        var error = Assert.Throws<ArgumentException>(() => Position.Create(
            white: new Dictionary<int, int> { [6] = men },
            black: new Dictionary<int, int> { [6] = 15 }));

        Assert.Contains("men-count", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void One_point_cannot_hold_men_of_both_sides()
    {
        // White's pip 6 and Black's pip 19 are the same point.
        var error = Assert.Throws<ArgumentException>(() => Position.Create(
            white: new Dictionary<int, int> { [6] = 15 },
            black: new Dictionary<int, int> { [19] = 15 }));

        Assert.Contains("both players hold men", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lone_adversary_man_on_the_destination_is_taken_up_onto_the_bar()
    {
        // Black has a blot on his pip 19, which is White's pip 6.
        var position = Board.Of(
            Board.Men().At(10, 1).RestAt(24),
            Board.Men().At(19, 1).RestAt(2));

        var after = position.Apply(Player.White, 10, 6);

        Assert.Equal(1, after.Men(Player.White, 6));
        Assert.Equal(0, after.Men(Player.Black, 19));
        Assert.Equal(1, after.OnBar(Player.Black));
    }

    [Fact]
    public void A_made_point_is_not_disturbed_by_a_man_landing_beside_it()
    {
        // Two of White's own men on the destination: his own, so nothing is taken up.
        var position = Board.Of(
            Board.Men().At(10, 1).At(6, 2).RestAt(24),
            Board.Men().RestAt(2));

        var after = position.Apply(Player.White, 10, 6);

        Assert.Equal(3, after.Men(Player.White, 6));
        Assert.Equal(0, after.OnBar(Player.Black));
    }

    [Fact]
    public void Bearing_a_man_off_puts_it_in_the_borne_off_slot()
    {
        var after = Corpus.BearingOffExample.Apply(Player.White, 4, Geometry.BorneOffPip);

        Assert.Equal(1, after.BorneOff(Player.White));
        Assert.Equal(2, after.Men(Player.White, 4));
    }

    [Fact]
    public void Two_men_make_a_point_and_one_is_a_blot()
    {
        // White's ace point in Black's table (his pip 24) starts with exactly two men, so the
        // threshold itself is under test: two make the point, and moving one leaves a blot.
        var start = Corpus.StartingPosition.Position;
        Assert.Equal(2, start.Men(Player.White, 24));

        Assert.True(start.HasMadePoint(Player.White, 24));
        Assert.False(start.HasBlot(Player.White, 24));

        var after = start.Apply(Player.White, 24, 18);
        Assert.Equal(1, after.Men(Player.White, 24));
        Assert.True(after.HasBlot(Player.White, 24));
        Assert.False(after.HasMadePoint(Player.White, 24));
        Assert.True(after.HasBlot(Player.White, 18));

        // More than two still make the point and are no blot; an empty point is neither.
        Assert.True(start.HasMadePoint(Player.White, 6));
        Assert.False(start.HasBlot(Player.White, 6));
        Assert.False(start.HasMadePoint(Player.White, 1));
        Assert.False(start.HasBlot(Player.White, 1));
    }

    [Fact]
    public void Occupied_points_read_highest_first()
    {
        Assert.Equal(new[] { 24, 13, 8, 6 }, Corpus.StartingPosition.Position.OccupiedPoints(Player.White));
    }
}
