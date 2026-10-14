namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A party a country may have, as <c>common/ideologies.txt</c> declares it: its name, its number - the
/// file's order counted from 1, number 0 being the null ideology nothing names - its group and its
/// colour. See <c>docs/mechanics/politics.md</c>, section 1.
/// </summary>
public sealed class CIdeology
{
	public CIdeology(string name, int index, Diplomacy.CIdeologyGroup? group, CColor colour)
	{
		Name = name;
		Index = index;
		Group = group;
		Colour = colour;
	}

	public string Name { get; }

	/// <summary>Its number, from 1; 0 for the null ideology.</summary>
	public int Index { get; }

	/// <summary>The corner of the triangle it belongs to; none for the null ideology.</summary>
	public Diplomacy.CIdeologyGroup? Group { get; }

	public CColor Colour { get; }

	public override string ToString()
	{
		return Name;
	}
}
