using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// One of <c>common/triggered_modifiers.txt</c>'s entries: a modifier a country has while its <c>potential</c> and its
/// <c>trigger</c> both hold, and the frame of the strip it is shown with. See <c>docs/mechanics/modifiers.md</c>,
/// section 5.
/// </summary>
public sealed class CTriggeredModifier
{
	public CTriggeredModifier(string name)
	{
		Name = name;
		Modifier = new CModifier(name);
	}

	/// <summary>Its key, which is also the key of its name and, after <c>desc_</c>, of its description.</summary>
	public string Name { get; }

	/// <summary>Every key of its block that is not one of its own.</summary>
	public CModifier Modifier { get; }

	/// <summary>The frame of the Strategic Effects window's strip, nought where its block gives none.</summary>
	public int Icon { get; set; }

	/// <summary>Whether it may be had at all: what the Strategic Effects window lists by.</summary>
	public CTrigger Potential { get; } = CTrigger.All();

	/// <summary>Whether it is had, where <see cref="Potential"/> holds.</summary>
	public CTrigger Trigger { get; } = CTrigger.All();
}
