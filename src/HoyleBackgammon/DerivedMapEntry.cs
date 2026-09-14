using System.Collections.Immutable;

namespace HoyleBackgammon;

/// <summary>
/// A derived entry of <c>corpus-map.json</c>: a fact the corpus entails and never states.
/// </summary>
/// <remarks>
/// It has no locator, because no passage contains it; the passages of the entries it is
/// derived from are its citation (<c>rules-factory/docs/decisions/0012</c>). It is a type of
/// its own rather than a <see cref="MapEntry"/> with an optional locator so that nothing can
/// decline citing it: an unresolved result names a passage, and this has none.
/// </remarks>
/// <param name="Id">The map entry's stable slug.</param>
/// <param name="Name">The entry's name, as the map records it.</param>
/// <param name="DerivedFrom">The entries it is derived from, in the map's order.</param>
public sealed record DerivedMapEntry(string Id, string Name, ImmutableArray<MapEntry> DerivedFrom)
{
    /// <inheritdoc/>
    public override string ToString() =>
        $"{Id} [derived from {string.Join(", ", DerivedFrom.Select(e => e.Id))}]";
}
