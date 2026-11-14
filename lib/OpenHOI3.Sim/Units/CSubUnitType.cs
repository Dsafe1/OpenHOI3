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

	/// <summary>
	/// Its place in the original's database of types, from 1 - the place 0 is the database's empty type: the
	/// <c>units</c> files in the byte order of their names, upper case first. A battle's <c>size</c> in a save is
	/// written in this order. See <c>docs/mechanics/savegame.md</c>, section 7.
	/// </summary>
	public int DatabaseIndex { get; set; }

	/// <summary>What a brigade of this type may be changed into, in the order <c>unit_upgrades.txt</c> leaves them.</summary>
	public List<CSubUnitType> UpgradeTargets { get; } = new();

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
	/// Its file's <c>available_trigger</c>, script a country must answer to build one, asked of the country alone; none, or
	/// a block with nothing in it, lets any country (<c>docs/mechanics/builders.md</c>, section 2).
	/// </summary>
	public Script.CTrigger? AvailableTrigger { get; set; }

	/// <summary>
	/// Its file's <c>extra_amphibious_defence</c>: for each kind of brigade it names, in thousandths, what a landing of that
	/// kind is meant to add to the share of the type's <c>amphibious_invasion_defence</c> it gets - an assault ship's for
	/// armour. Kept as read; the original asks it of a fleet's own summed table, which never holds one, so it does nothing
	/// (<c>docs/mechanics/transport.md</c>, section 9).
	/// </summary>
	public Dictionary<string, CFixedPoint> ExtraAmphibiousDefence { get; } = new(StringComparer.OrdinalIgnoreCase);

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
