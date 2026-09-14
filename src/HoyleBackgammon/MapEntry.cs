using RulesKernel.Provenance;

namespace HoyleBackgammon;

/// <summary>
/// One entry of <c>corpus-map.json</c>, as the code refers to it.
/// </summary>
/// <remarks>
/// Every rule this engine implements names the entry it implements and the entry names the
/// passage. A reader gets from a line of code to a page of Hoyle without opening the map.
/// </remarks>
/// <param name="Id">The map entry's stable slug.</param>
/// <param name="Name">The entry's name, as the map records it.</param>
/// <param name="Locator">Corpus id plus citation, as the map records it.</param>
public sealed record MapEntry(string Id, string Name, SourceLocator Locator)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Id} [{Locator}]";
}
