using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// A corner of the alignment triangle - fascism, democracy, communism - as <c>common/ideologies.txt</c>
/// declares it: its position, the ideologies in it, and the faction it has, if any. See
/// <c>docs/mechanics/alignment.md</c>, section 1.
/// </summary>
public sealed class CIdeologyGroup
{
	public CIdeologyGroup(string name, long x, long y)
	{
		Name = name;
		X = x;
		Y = y;
	}

	public string Name { get; }

	/// <summary>Its position, thousandths.</summary>
	public long X { get; }

	public long Y { get; }

	/// <summary>Its ideologies, by name.</summary>
	public HashSet<string> Ideologies { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Its faction's tag - <c>axis</c> - or nothing for a group with none.</summary>
	public string Faction { get; init; } = "";

	/// <summary>The faction's <c>neutrality</c>: how much more a member may have and still declare war.</summary>
	public CFixedPoint FactionNeutrality { get; init; }

	/// <summary>The static modifier the faction's influence puts on a country - <c>align_towards_axis</c>.</summary>
	public string Influence { get; init; } = "";
}
