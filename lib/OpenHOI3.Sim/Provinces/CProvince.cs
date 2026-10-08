using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// One province: what it is, and who holds it.
///
/// Id 0 is a real entry and means "no province" - it is what a point on the map that
/// belongs to nothing resolves to - so a province array is indexed by id directly, with
/// nothing subtracted. See <c>docs/mechanics/map.md</c>, section 2.
/// </summary>
public sealed class CProvince
{
	private CFixedPoint[] _buildingLevels = System.Array.Empty<CFixedPoint>();

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

	/// <summary>What the province could yield in a day. Only resources are ever set.</summary>
	public CGoodsPool MaxProducing { get; } = new();

	/// <summary>
	/// What it yields in a day now. The same as the most it could until something
	/// damages it.
	/// </summary>
	public CGoodsPool CurrentProducing { get; } = new();

	/// <summary>
	/// What is lying in the province. In a capital this is the country's stockpile: see
	/// <c>docs/mechanics/stockpile.md</c>, section 1.
	/// </summary>
	public CGoodsPool Pool { get; } = new();

	public CFixedPoint Manpower { get; set; }

	public CFixedPoint Leadership { get; set; }

	/// <summary>Victory points.</summary>
	public int Points { get; set; }

	/// <summary>
	/// What the ground is. The map gives every province one, and its history file may
	/// replace it. Null only for the "no province" entry or a map with no terrain data.
	/// </summary>
	public CTerrain? Terrain { get; set; }

	/// <summary>
	/// The strategic resource found here, by name, or an empty string. Whoever controls
	/// the province has what the resource is worth: see <see cref="CCountryModifiers"/>.
	/// </summary>
	public string StrategicResource { get; set; } = "";

	/// <summary>The continent it is on, by the name the map gives it. Empty for the sea.</summary>
	public string Continent { get; init; } = "";

	/// <summary>
	/// The sum of everything that currently applies to the province. Rebuilt, never
	/// adjusted: see <see cref="CProvinceModifiers"/>.
	/// </summary>
	public CModifier Modifier { get; } = new();

	/// <summary>
	/// How much of a building there is here. There is one level per building for now:
	/// the original keeps a maximum and a current level, which differ only under damage.
	/// </summary>
	public CFixedPoint BuildingLevel(CBuilding building)
	{
		return building.Index < _buildingLevels.Length ? _buildingLevels[building.Index] : CFixedPoint.Zero;
	}

	public void SetBuildingLevel(CBuilding building, CFixedPoint level)
	{
		if (building.Index >= _buildingLevels.Length)
		{
			System.Array.Resize(ref _buildingLevels, building.Index + 1);
		}
		_buildingLevels[building.Index] = level;
	}

	/// <summary>Ids of the provinces this one touches, ascending. Filled from the map.</summary>
	public IReadOnlyList<int> Neighbours { get; set; } = System.Array.Empty<int>();

	public override string ToString()
	{
		return $"{Id} ({Name})";
	}
}
