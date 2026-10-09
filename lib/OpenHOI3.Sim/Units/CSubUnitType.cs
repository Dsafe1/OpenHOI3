using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// One kind of brigade, ship or wing, as a file under <c>units/</c> declares it. The
/// original calls all three a sub unit. See <c>docs/mechanics/units.md</c>, section 1.
/// </summary>
public sealed class CSubUnitType
{
	public const string MaxStrength = "max_strength";
	public const string DefaultOrganisation = "default_organisation";
	public const string Officers = "officers";
	public const string CombatWidth = "combat_width";
	public const string TransportWeight = "transport_weight";

	public CSubUnitType(string key, EUnitBranch branch)
	{
		Key = key;
		Branch = branch;
	}

	/// <summary>The name the files know it by: <c>infantry_brigade</c>.</summary>
	public string Key { get; }

	public EUnitBranch Branch { get; }

	/// <summary>Its figures before any technology.</summary>
	public CSubUnitStats Stats { get; } = new();

	/// <summary>What its file answers yes or no to, by key: <c>is_armor</c>, <c>capital</c>, <c>active</c>.</summary>
	public Dictionary<string, bool> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The picture its counter is drawn with.</summary>
	public string Sprite { get; init; } = "";

	/// <summary>The combined arms group it counts towards, or an empty string.</summary>
	public string UnitGroup { get; init; } = "";

	/// <summary>The practical that gains when one is built, or an empty string.</summary>
	public string OnCompletion { get; init; } = "";

	/// <summary>The countries that may build it; empty for any.</summary>
	public List<string> UsableBy { get; } = new();

	/// <summary>
	/// The technologies that improve it, in the order they are declared: every one with
	/// a block of this type's name. A brigade keeps a level of each.
	/// </summary>
	public List<CTechnology> Technologies { get; } = new();

	public bool Flag(string key)
	{
		return Flags.TryGetValue(key, out bool value) && value;
	}

	public override string ToString()
	{
		return Key;
	}
}
