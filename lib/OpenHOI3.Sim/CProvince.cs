using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// One province: what it is, and who holds it.
///
/// Id 0 is a real entry and means "no province" - it is what a point on the map that
/// belongs to nothing resolves to - so a province array is indexed by id directly, with
/// nothing subtracted. See <c>docs/mechanics/map.md</c>, section 2.
/// </summary>
public sealed class CProvince
{
	public CProvince(int id, string name, bool isSea)
	{
		Id = id;
		Name = name;
		IsSea = isSea;
	}

	public int Id { get; }

	/// <summary>Localised name; falls back to the localisation key when it is missing.</summary>
	public string Name { get; }

	public bool IsSea { get; }

	/// <summary>The country it belongs to, or null: every sea province, and unclaimed land.</summary>
	public CCountryTag? Owner { get; set; }

	/// <summary>The country whose forces hold it, which differs from the owner under occupation.</summary>
	public CCountryTag? Controller { get; set; }

	/// <summary>The countries that claim it as rightfully theirs, in the order the claims were made.</summary>
	public List<CCountryTag> Cores { get; } = new();

	/// <summary>
	/// What the ground is. The map gives every province one, and its history file may
	/// replace it. Null only for the "no province" entry or a map with no terrain data.
	/// </summary>
	public CTerrain? Terrain { get; set; }

	/// <summary>Ids of the provinces this one touches, ascending. Filled from the map.</summary>
	public IReadOnlyList<int> Neighbours { get; set; } = System.Array.Empty<int>();

	public override string ToString()
	{
		return $"{Id} ({Name})";
	}
}
