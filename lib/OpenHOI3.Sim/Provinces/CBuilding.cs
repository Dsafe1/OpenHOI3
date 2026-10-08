namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A kind of building, as <c>common/buildings.txt</c> declares it: industry,
/// infrastructure, a naval base and so on.
///
/// A building has exactly one modifier - what one level of it is worth to its province,
/// such as <c>ic = 1</c> on industry. See <c>docs/mechanics/ic.md</c>, section 1.
/// </summary>
public sealed class CBuilding
{
	public CBuilding(int index, string key, int maxLevel, string modifierKey, CFixedPoint modifierValue)
	{
		Index = index;
		Key = key;
		MaxLevel = maxLevel;
		ModifierKey = modifierKey;
		ModifierValue = modifierValue;
	}

	/// <summary>Its position in the file, which is also its place in a province's levels.</summary>
	public int Index { get; }

	/// <summary>The name the files use for it, and the localisation key.</summary>
	public string Key { get; }

	public int MaxLevel { get; }

	/// <summary>The modifier one level contributes, or an empty string when it has none.</summary>
	public string ModifierKey { get; }

	/// <summary>How much of that modifier one level is worth.</summary>
	public CFixedPoint ModifierValue { get; }

	public override string ToString()
	{
		return Key;
	}
}
