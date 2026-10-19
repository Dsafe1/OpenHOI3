using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// One of the AI's invasions: the divisions, transports and escorts taken for it, the enemy province it lands on, the
/// friendly port it sails from and where its fleets meet. The original's <c>CAIInvasion</c>. See
/// <c>docs/mechanics/moving.md</c>, section 16.
/// </summary>
public sealed class CInvasion
{
	public CInvasion(CCountryTag country, int objective)
	{
		Country = country;
		Objective = objective;
	}

	public CCountryTag Country { get; }

	/// <summary>The province it lands on.</summary>
	public int Objective { get; set; }

	/// <summary>The friendly port it sails from.</summary>
	public int EmbarkationPort { get; set; }

	/// <summary>Where its fleets meet and its troops board.</summary>
	public int Rendezvous { get; set; }

	/// <summary>Its divisions.</summary>
	public List<CUnit> LandForce { get; } = new();

	/// <summary>Its transports and escorts.</summary>
	public List<CUnit> Fleets { get; } = new();

	/// <summary>Its air units.</summary>
	public List<CUnit> Air { get; } = new();

	/// <summary>Over for good.</summary>
	public bool IsFinished { get; set; }

	public void Add(CUnit unit)
	{
		List<CUnit> list = unit.Kind switch
		{
			EUnitKind.Navy => Fleets,
			EUnitKind.Air => Air,
			_ => LandForce,
		};
		if (list.Contains(unit))
		{
			return;
		}
		list.Add(unit);
		unit.Invasion = this;
		if (list == LandForce)
		{
			unit.Staging = 0;
		}
	}

	public void Remove(CUnit unit)
	{
		LandForce.Remove(unit);
		Fleets.Remove(unit);
		Air.Remove(unit);
		if (ReferenceEquals(unit.Invasion, this))
		{
			unit.Invasion = null;
		}
	}

	/// <summary>Every unit let go.</summary>
	public void Release()
	{
		foreach (CUnit unit in new List<CUnit>(LandForce))
		{
			Remove(unit);
		}
		foreach (CUnit unit in new List<CUnit>(Fleets))
		{
			Remove(unit);
		}
		foreach (CUnit unit in new List<CUnit>(Air))
		{
			Remove(unit);
		}
	}
}
