using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The words a new unit's name is made of when its country's own lists have run out:
/// three patterns, the four ordinal endings and each unit type's short name. They are the
/// game's texts, which the simulation does not read; whoever loads the game hands them
/// over. See <c>docs/mechanics/builders.md</c>, section 8.
/// </summary>
public sealed class CUnitNameTexts
{
	public const string DivisionKey = "DIVISION_NAME";
	public const string BrigadeKey = "REGIMENT_NAME";
	public const string SubUnitKey = "SUBUNIT_NAME";
	public const string TheatreKey = "THEATRE_NAME";
	public const string NamedTheatreKey = "NAMED_THEATHRE";
	public const string ArmyGroupKey = "ARMYGROUP_NAME";
	public const string ArmyKey = "ARMY_NAME";
	public const string CorpsKey = "CORPS_NAME";
	public const string NavyKey = "NAVY_NAME";
	public const string AirKey = "AIR_NAME";
	public const string ShortSuffix = "_short";

	/// <summary>The keys of the endings, by the last digit of the number: <c>TH ST ND RD</c> and then <c>TH</c>.</summary>
	public static readonly string[] OrdinalKeys = { "TH", "ST", "ND", "RD" };

	/// <summary>A division's: <c>$NUM$$ORDER$ $TYPE$ Division</c> in English.</summary>
	public string Division { get; init; } = DivisionKey;

	/// <summary>A land brigade's.</summary>
	public string Brigade { get; init; } = BrigadeKey;

	/// <summary>A ship's or a wing's.</summary>
	public string SubUnit { get; init; } = SubUnitKey;

	/// <summary>A theatre headquarters' numbered: <c>$NUM$$ORDER$ Theatre</c> in English.</summary>
	public string Theatre { get; init; } = TheatreKey;

	/// <summary>A theatre headquarters' first try, after a province: <c>$PROV$ HQ</c> in English.</summary>
	public string NamedTheatre { get; init; } = NamedTheatreKey;

	/// <summary>
	/// The numbered names of the commands, fleets and air groups the AI makes: <c>$NUM$$ORDER$ Army Group</c>, <c>Army</c>,
	/// <c>Corps</c>, <c>Navy</c> and <c>Air wing</c> in English (<c>docs/mechanics/agents.md</c>, sections 4.4 and 5.4).
	/// </summary>
	public string ArmyGroup { get; init; } = ArmyGroupKey;

	public string Army { get; init; } = ArmyKey;

	public string Corps { get; init; } = CorpsKey;

	public string Navy { get; init; } = NavyKey;

	public string Air { get; init; } = AirKey;

	/// <summary>The endings for a number ending in 0, 1, 2 and 3; 0's serves every other digit.</summary>
	public string[] Ordinals { get; init; } = { "TH", "ST", "ND", "RD" };

	/// <summary>Each unit type's short name, by its key: <c>Inf</c>.</summary>
	public Dictionary<string, string> ShortNames { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>A type's short name, or the key it would have been looked up by.</summary>
	public string ShortName(CSubUnitType type)
	{
		return ShortNames.TryGetValue(type.Key, out string? name) ? name : type.Key + ShortSuffix;
	}
}
