using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A war nobody declared: two sides who fight as enemies only in the regions listed, with no diplomatic state of its own.
/// See <c>docs/mechanics/war.md</c>, section 15.
/// </summary>
public sealed class CUndeclaredWar
{
	public List<CCountryTag> Attackers { get; } = new();

	public List<CCountryTag> Defenders { get; } = new();

	/// <summary>The regions it covers, by name, in the order they were added; one added twice is here twice.</summary>
	public List<string> Regions { get; } = new();

	public bool Joins(CCountryTag one, CCountryTag other)
	{
		return (Attackers.Contains(one) && Defenders.Contains(other)) || (Attackers.Contains(other) && Defenders.Contains(one));
	}
}
