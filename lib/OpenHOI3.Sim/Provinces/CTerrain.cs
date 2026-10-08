namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A kind of terrain: one of the categories <c>map/terrain.txt</c> declares, such as
/// plains, forest or ocean.
///
/// Only what the map needs so far. The numbers a category carries - movement cost,
/// attack, attrition - belong with the systems that use them and arrive with those.
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

	public override string ToString()
	{
		return Name;
	}
}
