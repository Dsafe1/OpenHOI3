using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// One law, which is a modifier that belongs to a group a country picks one from; with what changing to it costs,
/// the conditions it may be chosen under, and its number in the law database. See
/// <c>docs/mechanics/politics.md</c>, section 9.
/// </summary>
public sealed class CLaw
{
	public CLaw(string group, CModifier modifier)
	{
		Group = group;
		Modifier = modifier;
	}

	public string Group { get; }

	public string Name => Modifier.Name;

	public CModifier Modifier { get; }

	/// <summary>Its <c>cost</c>: what changing to it costs in thirty days of money from the country's whole IC.</summary>
	public CFixedPoint Cost { get; init; }

	/// <summary>Its <c>allow</c> block, asked of a country; none answers yes.</summary>
	public Script.CTrigger? Allow { get; set; }

	/// <summary>Its number in the law database: every group's laws in the file's order, from 1 - 0 is the null law.</summary>
	public int Index { get; set; }

	public override string ToString()
	{
		return $"{Group} = {Name}";
	}
}
