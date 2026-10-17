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

	/// <summary>Its area of operations as it last set it: the original's +0x90.</summary>
	public List<int> Area { get; } = new();

	/// <summary>The front-worthy enemy's provinces beside its area with infrastructure enough: the original's +0xC0.</summary>
	public List<int> FrontProvinces { get; } = new();

	/// <summary>The other countries' areas its theatre borders most: the original's +0x338.</summary>
	public List<CSupplyArea> FocusAreas { get; } = new();

	/// <summary>Where its headquarters is wanted, or 0.</summary>
	public int CommandProvince { get; set; }

	/// <summary>The share of its exposed positions its units cover, in thousandths.</summary>
	public long Coverage { get; set; }

	/// <summary>Its last powers and division counts: the original's +0x354 to +0x360.</summary>
	public long DivisionNeed { get; set; }

	public long DivisionCount { get; set; }

	public long OurPower { get; set; }

	public long TheirPower { get; set; }

	/// <summary>The stance pass's figure: the their power of its distinct enemies at war.</summary>
	public long EnemyPower { get; set; }

	public override string ToString()
	{
		return $"agent {Country} theatre {Theatre} ({Unit?.Name ?? "unbound"})";
	}
}
