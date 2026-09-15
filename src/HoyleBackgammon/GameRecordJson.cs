using System.Globalization;
using System.Text;
using System.Text.Json;
using RulesKernel.Identity;
using RulesKernel.Provenance;
using Tabletop.Dice;

namespace HoyleBackgammon;

/// <summary>
/// The map package this engine was produced from, as its embedded <c>provenance.json</c> records it.
/// </summary>
/// <param name="PackageId">The package id, e.g. <c>RulesFactory.Maps.HoyleBackgammon</c>.</param>
/// <param name="Version">The package version, e.g. <c>5.0.0</c>.</param>
public sealed record MapPackage(string PackageId, string Version)
{
    /// <summary>
    /// The map package named by <see cref="EngineProvenance"/>: the <c>map.packageId</c> and
    /// <c>map.version</c> of the provenance <c>factory produce</c> wrote and the build embedded.
    /// Read from those bytes, never typed out here, so it cannot drift from what the engine was
    /// produced from.
    /// </summary>
    /// <exception cref="InvalidOperationException">The embedded provenance has no map package.</exception>
    public static MapPackage FromProvenance { get; } = ReadProvenance();

    private static MapPackage ReadProvenance()
    {
        using var provenance = JsonDocument.Parse(EngineProvenance.ReadBytes());
        if (provenance.RootElement.TryGetProperty("map", out var map)
            && map.TryGetProperty("packageId", out var id) && id.GetString() is { Length: > 0 } packageId
            && map.TryGetProperty("version", out var v) && v.GetString() is { Length: > 0 } version)
        {
            return new MapPackage(packageId, version);
        }

        throw new InvalidOperationException(
            $"the embedded {EngineProvenance.ResourceName} names no map package (map.packageId and map.version)");
    }
}

/// <summary>
/// The canonical serialisation of a <see cref="GameRecord"/>: replay schema 2, the format
/// <c>docs/decisions/0006</c> defines.
/// </summary>
/// <remarks>
/// JSON in the canonical form of RFC 8785 (JCS), restricted to what a record holds: objects,
/// arrays, strings, integers, booleans and null. Object members are sorted by the UTF-16 code
/// units of their names; there is no insignificant whitespace; a string escapes only <c>"</c>,
/// <c>\</c> and the C0 controls (<c>\b \t \n \f \r</c> by name, the rest as lower-case
/// <c>\u00xx</c>) and writes every other character as itself; an integer is written in decimal
/// with no sign unless negative and no leading zeros. The bytes are UTF-8 without a byte-order
/// mark, and a string that is not well-formed UTF-16 is refused rather than replaced. Nothing
/// here goes through <see cref="JsonSerializer"/> or a runtime encoder whose escaping could move
/// between framework versions.
/// </remarks>
internal static class GameRecordJson
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Serialise(GameRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var text = new StringBuilder();
        Write(text, Of(record));
        return Utf8.GetBytes(text.ToString());
    }

    private static Obj Of(GameRecord record) => new()
    {
        ["identity"] = Of(record.Identity),
        ["map"] = new Obj { ["packageId"] = record.Map.PackageId, ["version"] = record.Map.Version },
        ["start"] = new Obj
        {
            ["position"] = Of(record.Start.Position),
            ["assertedBy"] = record.Start.AssertedBy,
            ["justification"] = record.Start.Justification is { } locator ? Of(locator) : null,
        },
        ["openingRoll"] = record.OpeningRoll is { } roll
            ? new Obj
            {
                ["attempts"] = roll.Attempts.Select(a => (object?)Of(a)).ToList(),
                ["opener"] = Name(roll.Opener),
            }
            : null,
        ["openingThrowAdopted"] = record.OpeningThrowAdopted,
        ["turns"] = record.Turns.Select(t => (object?)Of(t)).ToList(),
        ["winner"] = Name(record.Winner),
        ["value"] = Name(record.Value),
        ["next"] = Name(record.Next),
    };

    private static Obj Of(ReplayCompatibilityIdentity identity) => new()
    {
        ["ruleset"] = new Obj { ["id"] = identity.Ruleset.Id, ["version"] = identity.Ruleset.Version },
        ["replaySchema"] = identity.ReplaySchema.Version,
        ["sourceBaselines"] = identity.SourceBaselines.Select(b => (object?)new Obj
        {
            ["sourceId"] = b.SourceId,
            ["contentHash"] = b.ContentHash,
            ["hashDerivation"] = b.HashDerivation,
            ["asOf"] = b.AsOf?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }).ToList(),
        ["randomAlgorithm"] = identity.RandomAlgorithm?.Name,
    };

    private static Obj Of(SourceLocator locator) => new()
    {
        ["sourceId"] = locator.SourceId,
        ["citation"] = locator.Citation,
    };

    private static List<object?> Of(DiceThrow thrown) => [thrown.First, thrown.Second];

    /// <summary>Each player's men by his own pip, index 0 borne off, 1-24 the points, 25 the bar.</summary>
    private static Obj Of(Position position)
    {
        var sides = new Obj();
        foreach (var player in Players.Both)
        {
            var men = new List<object?>(Geometry.BarPip + 1);
            for (int pip = Geometry.BorneOffPip; pip <= Geometry.BarPip; pip++)
            {
                men.Add(position.Men(player, pip));
            }

            sides[Name(player)] = men;
        }

        return sides;
    }

    private static Obj Of(Turn turn) => new()
    {
        ["player"] = Name(turn.Player),
        ["thrown"] = turn.Thrown is { } thrown ? Of(thrown) : null,
        ["moves"] = turn.Play is { } play
            ? play.Moves.Select(m => (object?)new Obj
            {
                ["from"] = m.From,
                ["to"] = m.To,
                ["die"] = m.Die,
                ["kind"] = Name(m.Kind),
                ["authority"] = m.Authority.Id,
                ["takesUpBlot"] = m.TakesUpBlot,
            }).ToList()
            : null,
        ["position"] = Of(turn.Position),
    };

    private static string Name<T>(T value)
        where T : struct, Enum => value.ToString();

    private static void Write(StringBuilder text, object? value)
    {
        switch (value)
        {
            case null:
                text.Append("null");
                break;
            case bool b:
                text.Append(b ? "true" : "false");
                break;
            case int i:
                text.Append(i.ToString(CultureInfo.InvariantCulture));
                break;
            case string s:
                WriteString(text, s);
                break;
            case Obj obj:
                text.Append('{');
                bool firstMember = true;
                foreach (var (name, member) in obj)
                {
                    if (!firstMember)
                    {
                        text.Append(',');
                    }

                    firstMember = false;
                    WriteString(text, name);
                    text.Append(':');
                    Write(text, member);
                }

                text.Append('}');
                break;
            case List<object?> list:
                text.Append('[');
                for (int k = 0; k < list.Count; k++)
                {
                    if (k > 0)
                    {
                        text.Append(',');
                    }

                    Write(text, list[k]);
                }

                text.Append(']');
                break;
            default:
                throw new InvalidOperationException($"the canonical serialisation has no form for {value.GetType()}");
        }
    }

    private static void WriteString(StringBuilder text, string value)
    {
        text.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\t': text.Append("\\t"); break;
                case '\n': text.Append("\\n"); break;
                case '\f': text.Append("\\f"); break;
                case '\r': text.Append("\\r"); break;
                case < ' ': text.Append("\\u00").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture)); break;
                default: text.Append(c); break;
            }
        }

        text.Append('"');
    }

    /// <summary>A JSON object whose members enumerate in RFC 8785 order, whatever order they were added in.</summary>
    private sealed class Obj : SortedDictionary<string, object?>
    {
        public Obj()
            : base(StringComparer.Ordinal)
        {
        }
    }
}
