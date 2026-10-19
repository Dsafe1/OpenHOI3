using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// One of the AI's unit agents: one a theatre, bound to the theatre's headquarters, keeping what it works out between
/// hours. The original's <c>CAIUnit</c>. See <c>docs/mechanics/agents.md</c>, sections 1 and 3.
/// </summary>
public sealed class CAgent
{
	public CAgent(CCountryTag country, int theatre)
	{
		Country = country;
		Theatre = theatre;
	}

	public CCountryTag Country { get; }

	/// <summary>The theatre it works in, by number.</summary>
	public int Theatre { get; set; }

	/// <summary>The theatre's headquarters it is bound to, or null.</summary>
	public CUnit? Unit { get; set; }

	/// <summary>Its area of operations is to be set again at its next hour.</summary>
	public bool AreaDirty { get; set; } = true;

	/// <summary>It has posted its plan once: the original's <c>stance_set</c>.</summary>
	public bool PlanPosted { get; set; }

	/// <summary>Its next hour runs the full pass, whatever the hour.</summary>
	public bool FullPassPending { get; set; } = true;

	/// <summary>Its subordinates are sorted again at its next hour.</summary>
	public bool ReplanSubordinates { get; set; } = true;

	/// <summary>Its fleets are looked at in its next hour.</summary>
	public bool ReplanNaval { get; set; } = true;

	/// <summary>Its air groups are looked at again in its next hour.</summary>
	public bool RegroupAirPending { get; set; } = true;

	/// <summary>A province of its area has a neighbour whose controller its country is at war with.</summary>
	public bool FrontierAtWar { get; set; }

	/// <summary>A province of its area has a neighbour whose controller is a war candidate of its country's, or at war with it.</summary>
	public bool FrontierWarCandidate { get; set; }

	/// <summary>Its land units, of every level.</summary>
	public List<CUnit> Land { get; } = new();

	/// <summary>Its land units above a division.</summary>
	public List<CUnit> NonDivisions { get; } = new();

	public List<CUnit> Fleets { get; } = new();

	/// <summary>Its fleets that carry troops and either carry something or need no port.</summary>
	public List<CUnit> TransportFleets { get; } = new();

	/// <summary>Its air units, kept in order by range.</summary>
	public List<CUnit> Air { get; } = new();

	/// <summary>Its air units that carry troops.</summary>
	public List<CUnit> AirTransports { get; } = new();

	/// <summary>Its area of operations as it last set it.</summary>
	public List<int> Area { get; } = new();

	/// <summary>The front-worthy enemy's provinces beside its area with infrastructure enough.</summary>
	public List<int> FrontProvinces { get; } = new();

	/// <summary>The other countries' areas its theatre borders most.</summary>
	public List<CSupplyArea> FocusAreas { get; } = new();

	/// <summary>Where its headquarters is wanted, or 0.</summary>
	public int CommandProvince { get; set; }

	/// <summary>The share of its exposed positions its units cover, in thousandths.</summary>
	public long Coverage { get; set; }

	/// <summary>Its last powers and division counts.</summary>
	public long DivisionNeed { get; set; }

	public long DivisionCount { get; set; }

	public long OurPower { get; set; }

	public long TheirPower { get; set; }

	/// <summary>The stance pass's figure: the their power of its distinct enemies at war.</summary>
	public long EnemyPower { get; set; }

	// ---- the third piece: docs/mechanics/moving.md

	/// <summary>Its divisions committed to the line.</summary>
	public List<CUnit> Committed { get; } = new();

	/// <summary>Its divisions held back.</summary>
	public List<CUnit> HeldBack { get; } = new();

	/// <summary>Its committed divisions on a friend's soil within its capital's land.</summary>
	public int OnAlliedSoil { get; set; }

	/// <summary>Its area's provinces with their scores, best first.</summary>
	public List<CPosition> Positions { get; } = new();

	/// <summary>The scores' sum, in thousandths.</summary>
	public long PositionSum { get; set; }

	/// <summary>Friendly land two steps out from its area.</summary>
	public List<int> SecondRing { get; } = new();

	/// <summary>Every province of the areas it has chosen as targets.</summary>
	public HashSet<int> TargetProvinces { get; } = new();

	/// <summary>The enemy provinces on its ways to its target areas.</summary>
	public List<int> HostileRoutes { get; } = new();

	/// <summary>Each front province's nearness to the plan's objectives, 0 to 100.</summary>
	public Dictionary<int, long> Nearness { get; } = new();

	/// <summary>The provinces it wants held, best first - the original's <c>RebuildHoldProvinces</c>.</summary>
	public List<CHoldProvince> HoldProvinces { get; } = new();

	/// <summary>The hold provinces have been made.</summary>
	public bool HoldProvincesMade { get; set; }

	/// <summary>Its provinces an enemy is bound for from the sea.</summary>
	public List<int> Threatened { get; } = new();

	/// <summary>The province of every fleet of its country under an invasion order.</summary>
	public List<int> InvasionTargets { get; } = new();

	/// <summary>Where the enemy's fleets handed to it stood at the last count of the aircraft wanted.</summary>
	public List<int> EnemySightings { get; } = new();

	/// <summary>The air bases its air groups may go to, nearest the targets first, by role.</summary>
	public List<(int Province, float Score)> AirBasesDefault { get; } = new();

	public List<(int Province, float Score)> AirBasesStrategic { get; } = new();

	public List<(int Province, float Score)> AirBasesNaval { get; } = new();

	public List<(int Province, float Score)> AirBasesTransport { get; } = new();

	/// <summary>The four air base lists have been made since the last replan.</summary>
	public bool AirBasesMade { get; set; }

	/// <summary>The enemy's fleets handed to it, nearest by its ports.</summary>
	public List<CUnit> EnemyFleets { get; } = new();

	/// <summary>Its own country's and its fellow fighters' fleets handed to it.</summary>
	public List<CUnit> FriendlyFleets { get; } = new();

	/// <summary>The enemy's convoys its ports cover.</summary>
	public List<Convoys.CConvoy> EnemyConvoys { get; } = new();

	/// <summary>The sea provinces where its side's convoys were raided in the last month.</summary>
	public List<int> RaidedWaters { get; } = new();

	/// <summary>The transports of the enemy's convoys it is to hunt, over twenty.</summary>
	public int SubmarineWant { get; set; }

	/// <summary>Where its transports gather: its theatre's largest area's largest naval base, with troops for the sea.</summary>
	public int SealiftPort { get; set; }

	/// <summary>The weight of its troops listed for the sea.</summary>
	public long SealiftWeight { get; set; }

	public override string ToString()
	{
		return $"agent {Country} theatre {Theatre} ({Unit?.Name ?? "unbound"})";
	}
}
