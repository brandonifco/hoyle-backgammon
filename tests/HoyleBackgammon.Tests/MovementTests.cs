using Tabletop.Dice;
using Xunit;

namespace HoyleBackgammon.Tests;

public class LegalDestinationTests
{
    // "a man can only be played to a point which is either vacant or occupied by one or more
    // men of the player, or by one man only of the adversary."
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(15, false)]
    public void An_adversarys_men_on_the_destination(int adversaryMen, bool permitted)
    {
        // White's pip 4 is Black's pip 21.
        var position = Board.Of(
            Board.Men().At(10, 1).RestAt(24),
            Board.Men().At(21, adversaryMen).RestAt(2));

        Assert.Equal(permitted, Movement.IsPermittedDestination(position, Player.White, 4));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void The_players_own_men_never_refuse_him(int ownMen)
    {
        var position = Board.Of(
            Board.Men().At(10, 1).At(4, ownMen).RestAt(24),
            Board.Men().RestAt(2));

        Assert.True(Movement.IsPermittedDestination(position, Player.White, 4));
    }

    [Fact]
    public void A_destination_off_the_board_is_not_a_permitted_point()
    {
        var position = Corpus.StartingPosition.Position;

        Assert.False(Movement.IsPermittedDestination(position, Player.White, 0));
        Assert.False(Movement.IsPermittedDestination(position, Player.White, 25));
    }
}

public class EntitlementTests
{
    [Fact]
    public void Each_die_entitles_one_man_to_move_that_many_points()
    {
        Assert.Equal(new[] { 6, 3 }, Movement.Entitlement(new DiceThrow(3, 6)));
        Assert.Equal(new[] { 6, 3 }, Movement.Entitlement(new DiceThrow(6, 3)));
    }

    // "Suppose, for example, that he throws two aces; he may move one or more men forward to
    // an aggregate extent of four points. If he throw double deuces, he may move to an
    // aggregate extent of eight points; if double threes, twelve points, and so on."
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 12)]
    [InlineData(6, 24)]
    public void Doublets_are_played_twice_over(int face, int aggregate)
    {
        var entitlement = Movement.Entitlement(new DiceThrow(face, face));

        Assert.Equal(4, entitlement.Length);
        Assert.All(entitlement, number => Assert.Equal(face, number));
        Assert.Equal(aggregate, entitlement.Sum());
    }
}

public class MoveByPipTests
{
    [Fact]
    public void Six_trois_moves_one_man_six_and_then_the_same_or_another_man_three()
    {
        // "Thus if he threw 'six trois', he is entitled to move one man six points onward, and
        // then the same or another man three points onward."
        var plays = Legal.Plays(Corpus.StartingPosition.Position, Player.White, new DiceThrow(6, 3));

        var sameMan = plays.Single(p => p.Moves.SequenceEqual(new[]
        {
            Move(24, 18, 6), Move(18, 15, 3),
        }));
        var anotherMan = plays.Single(p => p.Moves.SequenceEqual(new[]
        {
            Move(24, 18, 6), Move(13, 10, 3),
        }));

        Assert.Equal(9, sameMan.PipsUsed);
        Assert.Equal(9, anotherMan.PipsUsed);
        Assert.Equal(1, sameMan.Result.Men(Player.White, 15));
        Assert.Equal(1, anotherMan.Result.Men(Player.White, 10));
    }

    [Fact]
    public void A_man_may_not_be_played_off_the_board_before_bearing_off_begins()
    {
        // One man on the trois point, the rest still far out, so the player is not eligible.
        var position = Board.Of(
            Board.Men().At(3, 1).RestAt(24),
            Board.Men().RestAt(2));

        Assert.Empty(Movement.MovesForDie(position, Player.White, 5)
            .Where(m => m.From == 3));
    }

    internal static Move Move(int from, int to, int die) =>
        new(from, to, die, MoveKind.Ordinary, TakesUpBlot: false);
}

public class EnterFromBarTests
{
    // White has a man up. Black holds his ace and deuce points (White's 24 and 23) with two
    // men each, so entry with an ace or a deuce is refused and anything else lets him in.
    private static Position WithAManUp() => Board.Of(
        Board.Men().At(Geometry.BarPip, 1).At(8, 6).RestAt(6),
        Board.Men().At(1, 2).At(2, 2).RestAt(20));

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void Entry_is_available_only_where_the_adversarys_table_is_open(int die, bool available)
    {
        var moves = Movement.MovesForDie(WithAManUp(), Player.White, die);

        Assert.Equal(available, moves.Length == 1);
        if (available)
        {
            var move = Assert.Single(moves);
            Assert.Equal(Geometry.BarPip, move.From);
            Assert.Equal(Geometry.BarPip - die, move.To);
            Assert.Equal(MoveKind.Entry, move.Kind);
            Assert.Equal(MapEntries.EnterFromBar, move.Authority);
        }
    }

    [Fact]
    public void While_a_man_is_up_the_play_of_his_other_men_is_suspended()
    {
        var position = WithAManUp();

        // He has men on his six and eight points that a trois could otherwise move.
        Assert.True(position.Men(Player.White, 8) > 0);
        Assert.True(position.Men(Player.White, 6) > 0);

        var moves = Movement.MovesForDie(position, Player.White, 3);

        Assert.All(moves, move => Assert.Equal(Geometry.BarPip, move.From));
    }

    // Two men up, and the adversary's table wide open. Every other bar case in this file has
    // exactly one man on the bar, which let the whole rule be satisfied by "is there a man up
    // at all" or by "is there exactly one" indifferently -- and the second is wrong: it would
    // leave a player with two men up moving his other men freely, which is the opposite of
    // what enter-from-bar says. These pin the count out of it.
    // Black's men are all on his own thirteen point, which is White's twelve, so White's inner
    // entry zone (his 19 through 24) is wide open and every number enters.
    private static Position WithTwoMenUp() => Board.Of(
        Board.Men().At(Geometry.BarPip, 2).At(8, 6).RestAt(6),
        Board.Men().RestAt(13));

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Two_men_up_suspends_the_others_exactly_as_one_does(int die)
    {
        var position = WithTwoMenUp();

        Assert.Equal(2, position.OnBar(Player.White));
        Assert.True(Movement.MustEnterFromBar(position, Player.White));

        // He has six men on his eight point and seven on his six point that any of these
        // numbers could otherwise move.
        Assert.True(position.Men(Player.White, 8) > 0);
        Assert.True(position.Men(Player.White, 6) > 0);

        var move = Assert.Single(Movement.MovesForDie(position, Player.White, die));
        Assert.Equal(Geometry.BarPip, move.From);
        Assert.Equal(MoveKind.Entry, move.Kind);
    }

    [Fact]
    public void The_second_man_up_is_still_up_when_the_first_has_entered()
    {
        // A throw of six trois with two men up: both numbers go on entering, and nothing else
        // moves. The play is the evidence -- were the second man not gating, the trois would
        // be free to play a man from the eight point.
        var plays = Legal.Plays(WithTwoMenUp(), Player.White, new DiceThrow(6, 3));

        Assert.All(plays, play => Assert.Equal(2, play.Moves.Length));
        Assert.All(plays, play => Assert.All(
            play.Moves, move => Assert.Equal(Geometry.BarPip, move.From)));

        Assert.All(plays, play =>
        {
            Assert.Equal(0, play.Result.OnBar(Player.White));
            Assert.Equal(1, play.Result.Men(Player.White, 19));
            Assert.Equal(1, play.Result.Men(Player.White, 22));
        });
        Assert.NotEmpty(plays);
    }

    // The one case on which the corpus's two statements of where a man may be played
    // disagree, and the only test in this repository that distinguishes them. White has a man
    // up and two of his own men on his pip 24 -- the ace point in Black's inner table, which
    // is where the corpus's own starting arrangement puts them, and which an ace enters on.
    private static Position WithOwnPointInTheAdversarysHomeTable() => Board.Of(
        Board.Men().At(Geometry.BarPip, 1).At(24, 2).RestAt(6),
        Board.Men().RestAt(13));

    [Fact]
    public void Entry_is_permitted_on_a_point_the_entering_players_own_men_hold()
    {
        // legal-destination admits a point "occupied by one or more men of the player";
        // enter-from-bar, three sentences later, admits only "a vacant point or blot". The
        // destination here is neither vacant nor a blot, so under the strict reading no move
        // exists and this test fails. rules-factory/docs/decisions/0006 rules that the
        // general qualification governs entry and the entry sentence is shorthand for it.
        var position = WithOwnPointInTheAdversarysHomeTable();

        Assert.Equal(Quarter.AdversaryInner, Geometry.QuarterOf(24));
        Assert.True(position.HasMadePoint(Player.White, 24));
        Assert.False(position.HasBlot(Player.White, 24));
        Assert.Equal(0, position.AdversaryMen(Player.White, 24));

        var move = Assert.Single(Movement.MovesForDie(position, Player.White, 1));

        Assert.Equal(Geometry.BarPip, move.From);
        Assert.Equal(24, move.To);
        Assert.Equal(MoveKind.Entry, move.Kind);
        Assert.False(move.TakesUpBlot);
        Assert.Equal(3, position.Apply(Player.White, Geometry.BarPip, 24).Men(Player.White, 24));
    }

    [Fact]
    public void A_blot_hit_begins_its_journey_anew_however_far_advanced()
    {
        // Black's blot stands on his pip 2 -- one point from home -- and is White's pip 23.
        var position = Board.Of(
            Board.Men().At(24, 1).RestAt(6),
            Board.Men().At(2, 1).RestAt(13));

        var after = position.Apply(Player.White, 24, 23);

        Assert.Equal(1, after.OnBar(Player.Black));
        Assert.Equal(0, after.Men(Player.Black, 2));
        Assert.True(Movement.MustEnterFromBar(after, Player.Black));

        // And it re-enters in White's inner table, which is Black's 24 down to 19.
        Assert.All(
            Movement.MovesForDie(after, Player.Black, 4),
            move => Assert.Equal(Quarter.AdversaryInner, Geometry.QuarterOf(move.To)));
    }
}

public class FullTableSuspensionTests
{
    private static Position AgainstAHomeTable(int menOnTheSixPoint) => Board.Of(
        Board.Men().At(Geometry.BarPip, 1).RestAt(8),
        Board.Men().At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2)
            .At(6, menOnTheSixPoint).RestAt(10));

    [Fact]
    public void Six_points_held_by_two_or_more_suspends_his_play_altogether()
    {
        var position = AgainstAHomeTable(2);

        Assert.True(Movement.IsWhollySuspended(position, Player.White));
        Assert.False(Movement.IsWhollySuspended(position, Player.Black));

        // And there is in fact no number he could enter on.
        for (int die = 1; die <= 6; die++)
        {
            Assert.Empty(Movement.MovesForDie(position, Player.White, die));
        }
    }

    [Fact]
    public void A_single_man_on_one_of_the_six_points_does_not_make_the_table_full()
    {
        var position = AgainstAHomeTable(1);

        Assert.False(Movement.IsWhollySuspended(position, Player.White));

        // A six enters on the point holding the blot, and takes it up.
        var move = Assert.Single(Movement.MovesForDie(position, Player.White, 6));
        Assert.Equal(19, move.To);
        Assert.True(move.TakesUpBlot);
    }

    [Fact]
    public void A_point_held_by_the_entering_players_own_men_does_not_make_the_table_full()
    {
        // "Completely full--i.e., each point occupied by two or more men" is unqualified, and
        // taken literally this table answers it: all six of Black's home points carry two or
        // more men. One of them carries WHITE's. rules-factory/docs/decisions/0006 rules that
        // "full" means full of the adverse player's men, which is the only reading consistent
        // with the general rule governing entry -- a point a man may enter on cannot be one
        // that shuts him out. Under the literal reading White is suspended and this fails.
        // Black holds his own pips 2 through 6 (White's 23 down to 19); White holds his 24,
        // which is Black's ace point.
        var position = Board.Of(
            Board.Men().At(Geometry.BarPip, 1).At(24, 2).RestAt(6),
            Board.Men().At(2, 2).At(3, 2).At(4, 2).At(5, 2).At(6, 2).RestAt(13));

        // Every point of the table is occupied by two or more men, whosever they are.
        for (int pip = 19; pip <= 24; pip++)
        {
            Assert.True(
                position.Men(Player.White, pip) + position.AdversaryMen(Player.White, pip) >= 2,
                $"pip {pip} is not occupied by two or more men");
        }

        Assert.True(Movement.MustEnterFromBar(position, Player.White));
        Assert.False(Movement.IsWhollySuspended(position, Player.White));

        // And the open point is the one his own men hold, entered with an ace. Every other
        // number is refused by two of Black's men, which is what leaves the ace alone to
        // carry the reading.
        var move = Assert.Single(Movement.MovesForDie(position, Player.White, 1));
        Assert.Equal(24, move.To);
        for (int die = 2; die <= 6; die++)
        {
            Assert.Empty(Movement.MovesForDie(position, Player.White, die));
        }
    }

    [Fact]
    public void Suspension_needs_a_man_on_the_bar()
    {
        var position = Board.Of(
            Board.Men().RestAt(8),
            Board.Men().At(1, 2).At(2, 2).At(3, 2).At(4, 2).At(5, 2).At(6, 2).RestAt(10));

        Assert.False(Movement.IsWhollySuspended(position, Player.White));
    }
}
