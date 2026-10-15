using System.Collections.Generic;

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

	/// <summary><c>onmap</c>: whether a province that has any of it shows its picture on the map.</summary>
	public bool IsOnMap { get; init; }

	/// <summary>
	/// <c>damage_factor</c>: how much of a bomb's worth it loses, where 1 is all of it and
	/// what a building has unless its file says otherwise. See
	/// <c>docs/mechanics/bombing.md</c>, section 7.
	/// </summary>
	public CFixedPoint DamageFactor { get; init; } = CFixedPoint.One;

	/// <summary><c>repair</c>: whether what it has lost comes back by the day. It does unless its file says no.</summary>
	public bool IsRepaired { get; init; } = true;

	/// <summary>
	/// <c>capital</c>: whether the production screen looks for a button of its name, and whether a line of it ordered
	/// with no province waits to be placed (<c>docs/mechanics/buildinglines.md</c>, section 1).
	/// </summary>
	public bool IsCapital { get; init; }

	/// <summary><c>cost</c>: IC a day a line of it takes, before the discount.</summary>
	public CFixedPoint Cost { get; init; }

	/// <summary><c>time</c>: days a line of it takes, before the discount, in thousandths.</summary>
	public CFixedPoint Time { get; init; }

	/// <summary><c>on_completion</c>: the technology category whose discount prices it and that a finished one teaches.</summary>
	public string OnCompletion { get; init; } = "";

	/// <summary><c>completion_size</c>: the practical a finished one gives its category.</summary>
	public CFixedPoint CompletionSize { get; init; }

	/// <summary><c>port</c>: it may go only in a province with a port.</summary>
	public bool IsPort { get; init; }

	/// <summary>
	/// <c>show_for_province</c>: it has a row in the province view. Yes unless its file says no, as only the underground's
	/// does (<c>docs/mechanics/provinceview.md</c>, section 4).
	/// </summary>
	public bool ShowForProvince { get; init; } = true;

	/// <summary><c>prerequisites</c>, resolved: buildings a province must have before it may take this one.</summary>
	public List<CBuilding> Prerequisites { get; } = new();

	/// <summary><c>not_if_x_exists</c>, resolved: buildings a province must not have.</summary>
	public List<CBuilding> Excluding { get; } = new();

	public override string ToString()
	{
		return Key;
	}
}
