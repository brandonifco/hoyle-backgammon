using RulesKernel.Resolution;

namespace HoyleBackgammon;

/// <summary>
/// The hand-written side of the typed contract rules-factory generates in
/// <c>Generated/Contracts.g.cs</c>: one partial method per <c>implemented</c> map entry, each a
/// thin adapter over the rule that already implements it.
/// </summary>
/// <remarks>
/// <para>
/// Each file under <c>Handlers/</c> holds one entry's handler and, where the rule needs inputs,
/// the <c>init</c> properties it reads, declared on the entry's generated request type (the
/// request types are <c>sealed partial</c>, rules-factory#93). A caller resolves an entry
/// through <see cref="EntryPoints"/> with those properties set, and the handler hands them to
/// the rule unchanged. Nothing here decides a rule: every answer, and every decline, is the
/// rule's own.
/// </para>
/// <para>
/// A request built by the dictionary dispatch (<see cref="Registry.Resolve(string, RuleRequest)"/>)
/// has every input at its default. A handler whose rule needs an input it was not given throws
/// <see cref="ArgumentException"/> naming it: a missing input is the caller's error, not a gap in
/// the corpus, so it is not an unresolved result.
/// </para>
/// <para>
/// Inside this class the members <c>OpeningRoll</c>, <c>GameValue</c> and
/// <c>AgreedBackgammonMultiple</c> hide the engine's types of the same names, so the handler
/// files name those types with <c>global::</c>.
/// </para>
/// </remarks>
internal static partial class Handlers
{
    private static Resolution<object> Value(object value) => Resolution<object>.FromValue(value);

    private static Resolution<object> Answer<T>(Resolution<T> resolution)
        where T : notnull =>
        resolution.Match(value => Resolution<object>.FromValue(value), Resolution<object>.FromUnresolved);

    private static T Demand<T>(T? input, string entryId, string name)
        where T : class =>
        input ?? throw Missing(entryId, name);

    private static T Demand<T>(T? input, string entryId, string name)
        where T : struct =>
        input ?? throw Missing(entryId, name);

    /// <summary>The agreement the caller asserted for <c>agreed-backgammon-multiple</c> (row 8).</summary>
    private static global::HoyleBackgammon.AgreedBackgammonMultiple Agreed(RuleRequest assertions) =>
        assertions.Asserted("agreed-backgammon-multiple") as global::HoyleBackgammon.AgreedBackgammonMultiple
        ?? throw new ArgumentException(
            $"the assertion 'agreed-backgammon-multiple' must be an {nameof(global::HoyleBackgammon.AgreedBackgammonMultiple)}",
            nameof(assertions));

    private static ArgumentException Missing(string entryId, string name) =>
        new($"resolving the map entry '{entryId}' needs its request's {name}, and it was not set", name);
}
