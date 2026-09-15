using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// The four fields that decide whether two runs of this engine are comparable at all.
/// </summary>
/// <remarks>
/// Every assertion here is against a <em>literal</em>, never against the constant the engine
/// builds <see cref="Game.Identity"/> from. Asserting against the constant is the shape that
/// makes a replay-identity test vacuous: the value and the expectation move together, so any
/// change to the identity — a ruleset renamed, a version bumped, a schema number altered, a
/// generator swapped — passes. A change to an identity field is a decision about whether
/// existing recorded games are still comparable with new ones, and a decision is exactly the
/// kind of thing a test should make somebody type out again.
/// <para>
/// So: when one of these fails, the fix is not to update the literal until it goes green. It
/// is to decide whether the change was meant, and to record it where decisions are recorded.
/// </para>
/// </remarks>
public class IdentityTests
{
    [Fact]
    public void The_ruleset_is_the_1909_backgammon_of_this_corpus_at_version_six()
    {
        // Version 6 since Brandon's rulings of 2026-09-15 on the first parts of must-play-whole-throw's and
        // bearing-off-eligible's questions and on all of game-value's: every throw is played and every finish
        // valued, and each result that relies on a ruling names it (docs/decisions/0010). Version 5 since Brandon's rulings of 2026-09-15 on the second parts of two questions map 6.0.0
        // leaves open: a throw version 4 declined because its orders reach the same position under
        // different rules, or because it brings the last man home with a number left, is played and names
        // the ruling (docs/decisions/0009). Version 4 since map 6.0.0 declined both (docs/decisions/0008;
        // finding 18, rules-factory#125). Version 3 since map 4.0.0: a win against a loser with nothing off
        // and a man in the winner's home table declines where version 2 valued it a backgammon
        // (docs/decisions/0005; finding 17, rules-factory#102). Version 2 began with map 3.0.0 (docs/decisions/0004).
        Assert.Equal("hoyle-1909-backgammon", Game.Identity.Ruleset.Id);
        Assert.Equal(6, Game.Identity.Ruleset.Version);
    }

    [Fact]
    public void The_replay_schema_is_version_four()
    {
        // The shape of a GameRecord: which fields a replay carries and what they mean. Version 4 since the record
        // names the owner's rulings its value relies on, valueRulings (docs/decisions/0010). Version 3 since each
        // turn names the owner's rulings its play relies on (docs/decisions/0009). Version 2 since the record
        // carries its identity and map and has a canonical serialisation of its own (docs/decisions/0006).
        // A recorded game from another schema cannot be read against this number.
        Assert.Equal(4, Game.Identity.ReplaySchema.Version);
    }

    [Fact]
    public void The_pinned_corpus_is_the_gutenberg_text_including_its_boilerplate()
    {
        var baseline = Assert.Single(Game.Identity.SourceBaselines);

        Assert.Equal("hoyle-1909", baseline.SourceId);
        Assert.Equal(
            "5d505fa9f6202340eb55313b8ef607b816087a860d3d51b1bf92b5f65240645e",
            baseline.ContentHash);

        // The derivation is half of the claim and the half that is easy to lose: the same
        // bytes hashed after stripping the Project Gutenberg header and footer give a
        // different digest, so a digest without the recipe that produced it is not checkable.
        // scripts/validate.sh re-derives exactly this recipe over corpus/hoyle.txt.
        Assert.Equal("gutenberg-plain-text-including-boilerplate", baseline.HashDerivation);
    }

    [Fact]
    public void The_generator_is_named_and_is_the_kernels_pcg32()
    {
        // IsDeterministicWithoutRandomness being false says only that *some* generator was
        // named. Two runs of different generators are both "not deterministic without
        // randomness" and are not comparable with each other, so the name is the fact that
        // matters and it is asserted by value.
        Assert.False(Game.Identity.IsDeterministicWithoutRandomness);
        Assert.NotNull(Game.Identity.RandomAlgorithm);
        Assert.Equal("pcg_setseq_64_xsh_rr_32", Game.Identity.RandomAlgorithm!.Value.Name);
    }
}
