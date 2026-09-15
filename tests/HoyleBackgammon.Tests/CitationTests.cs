using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace HoyleBackgammon.Tests;

/// <summary>
/// Every citation the engine carries resolves in <c>corpus/hoyle.txt</c>: the page exists, it lies
/// inside the section named, and an entry whose evidence quotes the corpus quotes that page.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the hand-built gate's "Citations resolve in the corpus" step (tag
/// <c>hand-built-v1</c>, <c>scripts/validate.sh</c>), which the generated gate does not carry. The
/// generated gate hashes the corpus and proves <see cref="MapEntries"/> is the map's; nothing
/// there checks that a citation points at the passage. The mistake this catches is the one the
/// retracted map made: a real sentence of the corpus attached to a page it is not on
/// (MAP-FINDINGS.md, finding 1).
/// </para>
/// <para>
/// The citations are read from <see cref="MapEntries"/>, which is what the code cites. The
/// evidence, which no generated type carries, is read from the map the engine was built from:
/// the restored package's <c>corpus-map.json</c>, which the test project copies beside this
/// assembly from the engine project's <c>RulesFactoryMap</c> item.
/// </para>
/// <para>
/// "Quotes the corpus" is not guessed at. Evidence found in the corpus is a quotation and must
/// be on the page cited; evidence not found is a summary, and nothing below the page is checked
/// for it. Which entries summarise is pinned, so a new one is a change someone sees.
/// </para>
/// </remarks>
public class CitationTests
{
    /// <summary>The chapter's sections, in order. "The Board and Men" is the untitled span from the chapter heading to the first printed one.</summary>
    private static readonly (string Name, string? Heading)[] Sections =
    [
        ("The Board and Men", null),
        ("Playing", "PLAYING."),
        ("Bearing off the Men", "BEARING OFF THE MEN."),
        ("Hints for Play", "HINTS FOR PLAY."),
    ];

    /// <summary>What may sit between two words of a quotation without being part of it: a line break, a {NNN} page marker, or a [NN] footnote reference.</summary>
    private const string Gap = @"\s*(?:(?:\{\d+\}|\[\d+\])\s*)*";

    private static readonly Regex CitationShape = new(@"^BACKGAMMON / (.+) / p\. (\d+)$", RegexOptions.CultureInvariant);

    // Universal newlines, as the gate's Python read it: the corpus is CRLF, and ^...$ must see lines.
    private static readonly string Text = File.ReadAllText(EngineTree.PathOf("corpus", "hoyle.txt")).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static readonly Dictionary<string, JsonElement> MapEntriesById = ReadMap();

    private static Dictionary<string, JsonElement> ReadMap()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "rules-factory-map", "corpus-map.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("entries").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("id").GetString()!, entry => entry.Clone(), StringComparer.Ordinal);
    }

    private static int OnlyOccurrence(string pattern, string what, int low, int high)
    {
        var found = Regex.Matches(Text[low..high], pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        Assert.True(found.Count == 1, $"{what}: expected exactly one occurrence, found {found.Count}");
        return low + found[0].Index;
    }

    private static Dictionary<string, (int Low, int High)> SectionSpans()
    {
        int chapter = OnlyOccurrence(@"^BACKGAMMON\.$", "the chapter heading", 0, Text.Length);
        int after = OnlyOccurrence(@"^BAGATELLE\.$", "the heading after the chapter", 0, Text.Length);
        Assert.True(after > chapter, "the chapter after BACKGAMMON comes before it");

        // The book reuses these headings (a table of contents, other games' PLAYING sections), so
        // every search but the chapter's own is confined to the chapter.
        var starts = Sections
            .Select(s => s.Heading is null ? chapter : OnlyOccurrence("^" + Regex.Escape(s.Heading) + "$", $"the {s.Name} heading", chapter, after))
            .ToList();
        Assert.Equal(starts.Order(), starts);

        starts.Add(after);
        return Sections.Select((s, i) => (s.Name, Span: (starts[i], starts[i + 1])))
            .ToDictionary(s => s.Name, s => s.Span, StringComparer.Ordinal);
    }

    /// <summary>Page N runs from its {N} marker to the next page's.</summary>
    private static (int Low, int High)? PageSpan(int page)
    {
        int here = Text.IndexOf("{" + page + "}", StringComparison.Ordinal);
        int next = Text.IndexOf("{" + (page + 1) + "}", StringComparison.Ordinal);
        return here < 0 || next <= here ? null : (here, next);
    }

    /// <summary>Where a quotation sits in the corpus, or null. Ellipsis joins fragments that must appear in order.</summary>
    private static (int Low, int High)? Locate(string quotation)
    {
        var flat = Regex.Replace(Regex.Replace(quotation, @"\{\d+\}", " "), @"\s+", " ").Trim();
        var fragments = flat.Split("...").Select(f => f.Trim()).Where(f => f.Length > 0).ToList();
        if (fragments.Count == 0)
        {
            return null;
        }

        var pattern = string.Join(".*?", fragments.Select(f => string.Join(Gap, f.Split(' ').Select(Regex.Escape))));
        var found = Regex.Match(Text, pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return found.Success ? (found.Index, found.Index + found.Length) : null;
    }

    private static IEnumerable<MapEntry> LocatedEntries() =>
        typeof(MapEntries).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(MapEntry))
            .Select(property => (MapEntry)property.GetValue(null)!);

    [Fact]
    public void Every_citation_names_a_page_inside_the_section_it_names_and_quoted_evidence_is_on_that_page()
    {
        var sections = SectionSpans();
        var problems = new List<string>();
        var located = LocatedEntries().ToList();
        Assert.NotEmpty(located);

        foreach (var entry in located)
        {
            var citation = CitationShape.Match(entry.Locator.Citation);
            if (!citation.Success)
            {
                problems.Add($"{entry.Id}: '{entry.Locator.Citation}' is not a citation this check can read");
                continue;
            }

            var section = citation.Groups[1].Value;
            int page = int.Parse(citation.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (!sections.TryGetValue(section, out var bounds))
            {
                problems.Add($"{entry.Id}: cites section '{section}', which the chapter has not got");
                continue;
            }

            if (PageSpan(page) is not { } span)
            {
                problems.Add($"{entry.Id}: cites p. {page}, whose marker the corpus has not got");
                continue;
            }

            if (span.High <= bounds.Low || span.Low >= bounds.High)
            {
                problems.Add($"{entry.Id}: cites '{section} / p. {page}', but p. {page} lies outside that section");
                continue;
            }

            if (Locate(MapEntriesById[entry.Id].GetProperty("evidence").GetString()!) is { } where
                && (where.High <= span.Low || where.Low >= span.High))
            {
                problems.Add($"{entry.Id}: its evidence is a sentence of the corpus, but not one the p. {page} it cites reaches");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Every_located_entry_quotes_the_corpus_rather_than_summarising_it()
    {
        // Pinned, so that an entry whose evidence stops being found in the corpus, and so drops
        // out of the page check above, is seen. In map 4.0.0 every located entry quotes.
        var summarised = LocatedEntries()
            .Where(entry => Locate(MapEntriesById[entry.Id].GetProperty("evidence").GetString()!) is null)
            .Select(entry => entry.Id);

        Assert.Empty(summarised);
    }

    [Fact]
    public void The_map_the_citations_are_checked_against_is_the_one_the_engine_was_built_from()
    {
        // Every entry the registry holds, and no other, with the same citation.
        Assert.Equal(Registry.Entries.Select(e => e.Id).Order(StringComparer.Ordinal), MapEntriesById.Keys.Order(StringComparer.Ordinal));
        foreach (var entry in LocatedEntries())
        {
            Assert.Equal(entry.Locator.Citation, MapEntriesById[entry.Id].GetProperty("locator").GetProperty("citation").GetString());
        }
    }
}
