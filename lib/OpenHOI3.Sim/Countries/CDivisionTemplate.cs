using System.Collections.Generic;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A division a country keeps ready to order: a name and the brigades it is made of.
/// A country's file gives it some under <c>default_templates</c>, and the division
/// designer shows a button for each. See <c>docs/mechanics/builders.md</c>, section 5.
/// </summary>
public sealed class CDivisionTemplate
{
	public CDivisionTemplate(string name)
	{
		Name = name;
	}

	/// <summary>The key its file gives it, which is also the key of its text: <c>GER_panzer_1936</c>.</summary>
	public string Name { get; }

	/// <summary>The unit types of its brigades, by key, in order.</summary>
	public List<string> Brigades { get; } = new();

	public override string ToString()
	{
		return Name;
	}
}
