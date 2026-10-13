using System.Text.Json;
using RulesKernel.Identity;
using RulesKernel.Randomness;
using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// The engine is built on the RulesKernel 1.x surface, the kernel line its consumers load beside it
/// (rules-ai, Reykholt), and its identity keeps the canonical form recorded games carry (#20).
/// </summary>
/// <remarks>
/// As in <see cref="IdentityTests"/>, the expectations are literals. The identity JSON below is what
/// every record written under kernel 0.3.0 carried; moving to <see cref="EngineIdentity"/> (kernel
/// 1.0.0's name for <c>ReplayCompatibilityIdentity</c>) did not change a byte of it, so records from
/// before and after the move remain comparable. If this fails, decide whether the change was meant
/// and record it; do not retype the literal until it goes green.
/// </remarks>
public class KernelContractTests
{
    private const string CanonicalIdentity =
        "{\"randomAlgorithm\":\"pcg_setseq_64_xsh_rr_32\",\"replaySchema\":4," +
        "\"ruleset\":{\"id\":\"hoyle-1909-backgammon\",\"version\":6}," +
        "\"sourceBaselines\":[{\"asOf\":null,\"contentHash\":\"5d505fa9f6202340eb55313b8ef607b816087a860d3d51b1bf92b5f65240645e\"," +
        "\"hashDerivation\":\"gutenberg-plain-text-including-boilerplate\",\"sourceId\":\"hoyle-1909\"}]}";

    [Fact]
    public void The_engine_binds_RulesKernel_and_RulesKernel_Randomness_major_version_one()
    {
        var referenced = typeof(Game).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name is "RulesKernel" or "RulesKernel.Randomness")
            .ToDictionary(name => name.Name!, name => name.Version!.Major);

        Assert.Equal(new Dictionary<string, int> { ["RulesKernel"] = 1, ["RulesKernel.Randomness"] = 1 }, referenced);
        Assert.Equal(1, typeof(EngineIdentity).Assembly.GetName().Version!.Major);
        Assert.Equal(1, typeof(Pcg32).Assembly.GetName().Version!.Major);
    }

    [Fact]
    public void The_canonical_form_of_the_identity_every_record_carries_is_unchanged()
    {
        var start = new AssertedPosition(Setup.StartingPositionFromCorpus(), nameof(KernelContractTests), MapEntries.StartingPosition.Locator);
        var record = Assert.IsType<Resolution<GameRecord>.Resolved>(
            Game.Play(start, Pcg32.FromSeed(20260913, stream: 1), new FirstOptionDecider())).Value;

        using var json = JsonDocument.Parse(record.ToCanonicalJson());

        Assert.Equal(CanonicalIdentity, json.RootElement.GetProperty("identity").GetRawText());
    }
}
