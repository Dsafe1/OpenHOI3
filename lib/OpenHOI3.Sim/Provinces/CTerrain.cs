namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A kind of terrain: one of the categories <c>map/terrain.txt</c> declares, such as
/// plains, forest or ocean.
///
/// Only what the map, movement and a battle need so far. The other numbers a category
/// carries - attrition, what it does to the weather - belong with the systems that use
/// them and arrive with those.
/// </summary>
public sealed class CTerrain
{
	public CTerrain(string name, bool isWater, CColor color)
	{
		Name = name;
		IsWater = isWater;
		Color = color;
	}

	/// <summary>As written in the file, which is also the localisation key.</summary>
	public string Name { get; }

	public bool IsWater { get; }

	/// <summary>The colour the file gives the category, for drawing a terrain map.</summary>
	public CColor Color { get; }

	/// <summary>
	/// What one pixel of it adds to the distance between two provinces: the file's
	/// <c>movement_cost</c>. It is spent when the map is measured and nowhere else - see
	/// <c>docs/mechanics/movement.md</c>, section 2.
	/// </summary>
	public CFixedPoint MovementCost { get; init; }

	/// <summary>
	/// What fighting on it does to an attacker and to a defender, before a unit's own
	/// way with the ground is added: the file's <c>attack</c> and <c>defence</c>. See
	/// <c>docs/mechanics/landcombat.md</c>, section 4.
	/// </summary>
	public CFixedPoint Attack { get; init; }

	public CFixedPoint Defence { get; init; }

	/// <summary>The file's <c>humidity</c>. Ground with any counts as jungle when it is hot.</summary>
	public CFixedPoint Humidity { get; init; }

	public override string ToString()
	{
		return Name;
	}
}
