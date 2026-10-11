namespace OpenHOI3.Interface.Modifiers;

/// <summary>
/// How a kind of modifier is written: the key its name is localised from, whether it is a
/// percentage, and whether more of it is better. See <c>docs/mechanics/modifiers.md</c>,
/// section 8.
/// </summary>
/// <param name="Key">The kind as the script files write it: <c>global_ic</c>.</param>
/// <param name="DisplayKey">The localisation key of its name: <c>MODIFIER_GLOBAL_IC</c>.</param>
/// <param name="Percent">Written times a hundred to one place with a <c>%</c>; else to two places.</param>
/// <param name="MoreIsBetter">A positive value is green; else it is red.</param>
public sealed record CModifierKind(string Key, string DisplayKey, bool Percent, bool MoreIsBetter);
