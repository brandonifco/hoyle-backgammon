using System.Reflection;
using System.Text.RegularExpressions;
using RulesKernel.Resolution;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// Every unresolved result the hand-written code can return agrees with the correspondence table
/// of <c>rules-factory/docs/corpus-map.md</c>, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the hand-built gate's "Map correspondence" step (tag <c>hand-built-v1</c>,
/// <c>scripts/validate.sh</c>), which the generated gate does not carry. The generated
/// <c>CorrespondenceTests</c> prove the registry: every entry that is not <c>implemented</c>
/// declines through it with its row's reason. They cannot see a decline an implemented entry's
/// rule returns from inside the engine, which is where every <c>RequiresInterpretation</c> this
/// engine gives comes from. These tests read the source for those.
/// </para>
/// <para>
/// Both directions are failures. A reason the code returns that its entry's row does not predict
/// means something was implemented without being mapped. An entry whose row predicts a reason no
/// code path returns means the map asserts something the code disproves.
/// </para>
/// <para>
/// The rows come from <see cref="Registry.Entries"/>, which rules-factory generates from the
/// package map merged with the overlay (the gate holds it to a fresh regeneration). The reason
/// each row predicts is transcribed here from the table rather than read from the generated
/// registry: an expectation taken from the thing under test proves nothing.
/// </para>
/// <para>
/// Row 7 (two implemented entries with no entry for their combination, UnsupportedInteraction)
/// is about a pair, and about interactions the map does not enumerate, so no per-entry predicate
/// expresses it. What is checked for an UnsupportedInteraction site is row 7's place in the
/// order: it is reached only when rows 1 to 6 do not match, and its entry is implemented.
/// </para>
/// <para>
/// <b>A question the owner has fully ruled on owes no decline.</b> rules-factory decision 0027 lets an
/// overlay item carry <c>rulings</c> and <c>declines</c>, and <c>declines: []</c> declares that every part of
/// the entry's open question has an owner's ruling. Row 6 still predicts <c>RequiresInterpretation</c> for
/// such an entry, because the map is unchanged, but the engine has declared that it declines nothing there. So
/// for an entry the overlay declares fully ruled the check is the other way round: no code path may return
/// <c>RequiresInterpretation</c> citing it (<c>docs/decisions/0010</c>). The overlay is read here, not the
/// generated registry, because the rulings are never merged into the map.
/// </para>
/// <para>
/// <c>src/HoyleBackgammon/Generated/</c> is not read. The registry's own declines are the
/// generated code's, and the generated tests prove them.
/// </para>
/// </remarks>
public class MapCorrespondenceTests
{
    private static readonly Regex Site = new(
        @"new UnresolvedResult\(\s*UnresolvedReason\.(\w+)\s*,(?:(?!new UnresolvedResult).)*?MapEntries\.(\w+)\.Locator\s*\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex Construction = new(@"new UnresolvedResult\(", RegexOptions.CultureInvariant);

    private sealed record Decline(string File, UnresolvedReason Reason, string EntryId);

    /// <summary>The entries <c>corpus-map.overlay.json</c> declares fully ruled: <c>rulings</c> and <c>declines: []</c>.</summary>
    private static HashSet<string> FullyRuled()
    {
        using var overlay = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(EngineTree.PathOf("corpus-map.overlay.json")));
        return overlay.RootElement.EnumerateObject()
            .Where(item => item.Value.TryGetProperty("rulings", out var rulings) && rulings.GetArrayLength() > 0
                && item.Value.TryGetProperty("declines", out var declines) && declines.GetArrayLength() == 0)
            .Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<FileInfo> HandWrittenSources() =>
        new DirectoryInfo(EngineTree.PathOf("src")).EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(EngineTree.Root.FullName, file.FullName)
                .Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj" or "Generated"))
            .OrderBy(file => file.FullName, StringComparer.Ordinal);

    /// <summary>The source with doc comments removed (several name a reason in prose) and whitespace collapsed.</summary>
    private static string Code(FileInfo file) =>
        Regex.Replace(
            string.Join(' ', File.ReadAllLines(file.FullName).Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal))),
            @"\s+",
            " ");

    private static Dictionary<string, string> EntryIdByMember() =>
        typeof(MapEntries).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(MapEntry))
            .ToDictionary(property => property.Name, property => ((MapEntry)property.GetValue(null)!).Id, StringComparer.Ordinal);

    private static List<Decline> Declines()
    {
        var byMember = EntryIdByMember();
        var found = new List<Decline>();
        foreach (var file in HandWrittenSources())
        {
            foreach (Match match in Site.Matches(Code(file)))
            {
                var reason = Enum.Parse<UnresolvedReason>(match.Groups[1].Value);
                var member = match.Groups[2].Value;
                Assert.True(byMember.ContainsKey(member), $"{file.Name} cites MapEntries.{member}, which declares no located entry of that name");
                found.Add(new Decline(file.Name, reason, byMember[member]));
            }
        }

        return found;
    }

    /// <summary>The reason a row predicts, from the table in rules-factory/docs/corpus-map.md; null where it predicts none.</summary>
    private static UnresolvedReason? Predicted(CorrespondenceRow row) => row switch
    {
        CorrespondenceRow.ScopeOut => UnresolvedReason.OutsideCurrentScope,
        CorrespondenceRow.NotBuilt => UnresolvedReason.UnsupportedRule,
        CorrespondenceRow.DefinedElsewhere => UnresolvedReason.MissingRulesData,
        CorrespondenceRow.BeyondAdapter => UnresolvedReason.MissingRulesData,
        CorrespondenceRow.ValueDependencyUnimplemented => UnresolvedReason.MissingRulesData,
        CorrespondenceRow.UnresolvedAmbiguity => UnresolvedReason.RequiresInterpretation,
        CorrespondenceRow.Assertion => null,
        CorrespondenceRow.None => null,
        _ => throw new InvalidOperationException($"row {row} is not in the table these tests transcribe"),
    };

    [Fact]
    public void Every_unresolved_result_in_the_hand_written_code_has_the_shape_this_check_reads()
    {
        // A site the check cannot read is a site it cannot vouch for, so the count is asserted
        // rather than assumed: a literal UnresolvedReason and a MapEntries.<Entry>.Locator.
        int constructions = HandWrittenSources().Sum(file => Construction.Matches(Code(file)).Count);
        var declines = Declines();

        Assert.NotEmpty(declines);
        Assert.Equal(constructions, declines.Count);
    }

    [Fact]
    public void Every_unresolved_result_in_the_hand_written_code_is_the_reason_its_entrys_row_predicts()
    {
        var problems = new List<string>();
        foreach (var decline in Declines())
        {
            var entry = Registry.Entry(decline.EntryId);
            var where = $"{decline.File}: {decline.Reason} citing {decline.EntryId}";
            if (decline.Reason == UnresolvedReason.UnsupportedInteraction)
            {
                if (entry.Row is not (CorrespondenceRow.None or CorrespondenceRow.Assertion))
                {
                    problems.Add($"{where}: row 7 is reached only when rows 1-6 do not match, and the entry matches {entry.Row}");
                }
                else if (entry.Status != EntryStatus.Implemented)
                {
                    problems.Add($"{where}: row 7 is about two implemented entries, and the entry is {entry.Status}");
                }

                continue;
            }

            var predicted = Predicted(entry.Row);
            if (predicted != decline.Reason)
            {
                problems.Add($"{where}: the entry matches {entry.Row}, which predicts {predicted?.ToString() ?? "no decline"}");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Every_entry_whose_row_predicts_a_decline_has_a_hand_written_code_path_returning_it()
    {
        var declines = Declines().Where(d => d.Reason != UnresolvedReason.UnsupportedInteraction).ToList();
        var fullyRuled = FullyRuled();
        var problems = new List<string>();
        foreach (var entry in Registry.Entries)
        {
            var cited = declines.Where(d => d.EntryId == entry.Id).Select(d => d.Reason).ToHashSet();
            var predicted = Predicted(entry.Row);
            if (fullyRuled.Contains(entry.Id))
            {
                if (predicted != UnresolvedReason.RequiresInterpretation)
                {
                    problems.Add($"{entry.Id}: the overlay declares it fully ruled, and it matches {entry.Row}, not an open question");
                }

                problems.AddRange(cited.Select(reason =>
                    $"{entry.Id}: the overlay declares it fully ruled (declines: []), but the code returns {reason} citing it"));
                continue;
            }

            if (predicted is null)
            {
                problems.AddRange(cited.Select(reason =>
                    $"{entry.Id}: matches {entry.Row}, which predicts no decline, but the code returns {reason} citing it"));
                continue;
            }

            if (!cited.Contains(predicted.Value))
            {
                problems.Add($"{entry.Id}: matches {entry.Row}, so the engine must be able to return {predicted} citing it, and no code path does");
            }

            problems.AddRange(cited.Where(reason => reason != predicted).Select(reason =>
                $"{entry.Id}: matches {entry.Row}, which predicts {predicted}, but the code also returns {reason} citing it"));
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void The_entries_the_overlay_declares_fully_ruled_are_the_three_the_owner_ruled_on()
    {
        // Pinned, so the exemption above cannot widen unnoticed: a fourth entry declared fully ruled is a new
        // decision record, not an edit to the overlay alone (docs/decisions/0010).
        Assert.Equal(
            ["bearing-off-eligible", "game-value", "must-play-whole-throw"],
            FullyRuled().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Nothing_inside_the_engine_calls_BeyondAdapter()
    {
        // BeyondAdapter's remarks say no rule here reaches it: every position is player-relative,
        // so nothing asks which physical compartment of a board is whose. The honesty of the
        // inner-table-handedness decline rests on that, so it is a fact rather than a remark.
        var callers = HandWrittenSources()
            .Where(file => file.Name != "BeyondAdapter.cs")
            .Where(file => Regex.IsMatch(Code(file), @"\bBeyondAdapter\s*\."))
            .Select(file => file.Name);

        Assert.Empty(callers);
    }
}
