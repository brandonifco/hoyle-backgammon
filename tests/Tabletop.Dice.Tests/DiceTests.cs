using System.Reflection;
using RulesKernel.Randomness;
using Xunit;

namespace Tabletop.Dice.Tests;

internal sealed class ScriptedSource(params uint[] raw) : IRandomSource
{
    private int _next;

    public int Drawn => _next;

    public uint NextUInt32() =>
        _next < raw.Length
            ? raw[_next++]
            : throw new InvalidOperationException($"draw {_next} was not scripted.");
}

public class DieTests
{
    [Theory]
    [InlineData(0u, 1)]
    [InlineData(1u, 2)]
    [InlineData(5u, 6)]
    [InlineData(6u, 1)]
    [InlineData(4294967291u, 6)]
    public void Face_is_the_raw_value_plus_one(uint raw, int expectedFace)
    {
        // The kernel's UniformInt.Below returns [0, bound); face f of a d(n) is raw f - 1.
        // 4294967291 is the last accepted word below the acceptance limit, and 4294967291 % 6
        // is 5, so it is a six.
        Assert.Equal(expectedFace, Die.D6.Throw(new ScriptedSource(raw)));
    }

    [Fact]
    public void A_rejected_word_costs_a_draw_and_is_not_a_face()
    {
        // 2^32 % 6 == 4, so UniformInt.Below rejects the top four words to stay unbiased.
        // 4294967292 is the first of them.
        var source = new ScriptedSource(4294967292u, 3u);

        int face = Die.D6.Throw(source);

        Assert.Equal(4, face);
        Assert.Equal(2, source.Drawn);
    }

    [Fact]
    public void A_throw_of_a_d6_costs_exactly_one_draw_when_the_word_is_accepted()
    {
        var source = new ScriptedSource(17u);

        Die.D6.Throw(source);

        Assert.Equal(1, source.Drawn);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    public void Every_face_of_a_die_is_reachable_and_none_beyond(int faces)
    {
        var die = new Die(faces);
        var seen = new HashSet<int>();

        for (uint raw = 0; raw < faces; raw++)
        {
            seen.Add(die.Throw(new ScriptedSource(raw)));
        }

        Assert.Equal(Enumerable.Range(1, faces).ToHashSet(), seen);
    }

    [Fact]
    public void A_die_needs_at_least_one_face() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Die(0));

    [Fact]
    public void A_default_die_refuses_to_be_thrown() =>
        Assert.Throws<InvalidOperationException>(() => default(Die).Throw(new ScriptedSource(0u)));
}

public class DiceThrowTests
{
    [Fact]
    public void A_pair_throws_its_first_die_before_its_second()
    {
        var source = new ScriptedSource(5u, 0u);

        var thrown = DicePair.OfSixes.Throw(source);

        Assert.Equal(6, thrown.First);
        Assert.Equal(1, thrown.Second);
        Assert.Equal(2, source.Drawn);
    }

    [Theory]
    [InlineData(6, 2, false, 6, 2)]
    [InlineData(2, 6, false, 6, 2)]
    [InlineData(4, 4, true, 4, 4)]
    public void Higher_lower_and_doublets(int first, int second, bool doublets, int higher, int lower)
    {
        var thrown = new DiceThrow(first, second);

        Assert.Equal(doublets, thrown.IsDoublets);
        Assert.Equal(higher, thrown.Higher);
        Assert.Equal(lower, thrown.Lower);
    }

    [Fact]
    public void Throw_order_is_kept_rather_than_normalised() =>
        Assert.NotEqual(new DiceThrow(6, 2), new DiceThrow(2, 6));
}

public class PackIsRulesetAgnosticTests
{
    private static readonly string[] RulesetVocabulary =
    [
        "backgammon", "hoyle", "blot", "gammon", "checker", "board", "point", "pip", "bar",
        "men", "stake", "table", "home", "player",
    ];

    // There used to be a test here asserting that this assembly does not reference
    // HoyleBackgammon. It could only have failed if somebody added the project reference, and
    // that reference is circular -- the engine references the pack -- so the build rejects it
    // as MSB4006 before any test runs. It restated a guarantee the compiler already gives,
    // which is worse than no test, because it looked like evidence for the claim in
    // Tabletop.Dice.csproj's comment and was not. The comment is the claim; the compiler is
    // the enforcement; the vocabulary test below is the part that needed a test.

    [Fact]
    public void No_public_name_in_the_pack_is_ruleset_vocabulary()
    {
        var offenders = new List<string>();

        foreach (var type in typeof(Die).Assembly.GetExportedTypes())
        {
            Check(type.Name, offenders);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Check($"{type.Name}.{member.Name}", offenders);
            }
        }

        Assert.Empty(offenders);
    }

    private static void Check(string name, List<string> offenders)
    {
        string last = name[(name.IndexOf('.', StringComparison.Ordinal) + 1)..];
        foreach (string word in RulesetVocabulary)
        {
            if (last.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{name} contains '{word}'");
            }
        }
    }
}
