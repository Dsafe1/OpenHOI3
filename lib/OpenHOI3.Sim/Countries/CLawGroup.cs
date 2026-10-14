using System.Collections.Generic;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A group of <c>common/laws.txt</c> a country holds one law of, numbered from 1 in the file's order, with its laws in
/// their order. See <c>docs/mechanics/politics.md</c>, section 9.
/// </summary>
public sealed class CLawGroup
{
	public CLawGroup(string name, int index)
	{
		Name = name;
		Index = index;
	}

	public string Name { get; }

	/// <summary>Its number, from 1; 0 is the null group.</summary>
	public int Index { get; }

	public List<CLaw> Laws { get; } = new();

	public override string ToString()
	{
		return Name;
	}
}
