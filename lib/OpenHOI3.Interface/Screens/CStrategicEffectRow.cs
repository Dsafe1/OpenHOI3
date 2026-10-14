using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// One row of the Strategic Effects window: the entry, whether the country has it, and how many of its <c>trigger</c>
/// conditions are met out of how many. See <c>docs/mechanics/strategiceffects.md</c>, section 3.
/// </summary>
public sealed record CStrategicEffectRow(CTriggeredModifier Entry, bool Present, int Met, int Counted);
