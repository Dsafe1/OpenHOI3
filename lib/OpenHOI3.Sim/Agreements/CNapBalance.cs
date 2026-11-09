using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// A non-aggression pact's balance of brigades along the border, which is what decides whether it may be broken in its
/// three windows: measured when a pact is made in a running game, and each country's day on every seventh day of the year
/// or while a pact has never been measured. See <c>docs/mechanics/agreements.md</c>, section 6.
/// </summary>
public static class CNapBalance
{
	/// <summary>A pact not yet measured [exe].</summary>
	public const long Unmeasured = -1000;

	/// <summary>The balance where the second party has nothing on the border, and what a balance is turned round against [exe].</summary>
	private const long NoneOfTheirs = 1000000;

	/// <summary>How often a measured pact is measured again, in days of the year [exe].</summary>
	private const int DaysBetween = 7;

	/// <summary>A pact's balance as a country sees it: turned round for its second party, and -1 for that party at nought [exe].</summary>
	public static long As(CNap nap, CCountryTag country)
	{
		if (country != nap.Second)
		{
			return nap.Balance;
		}
		return nap.Balance == 0 ? -1 : NoneOfTheirs / nap.Balance;
	}

	/// <summary>A pact just made: measured at once in a running game, and left unmeasured while one is being made.</summary>
	public static void Made(CGameState state, CNap nap)
	{
		if (!state.IsBeingMade)
		{
			Measure(state, nap);
		}
	}

	/// <summary>
	/// The head of a country's day: its pacts as first party, with a country that owns land, measured - all of them while
	/// a game is being made, and in a running one on a day of the year divisible by seven or where never measured.
	/// </summary>
	public static void UpdateDaily(CGameState state)
	{
		if (state.Naps.Count == 0)
		{
			return;
		}
		CDate today = state.Date;
		bool everyPact = state.IsBeingMade || (today.DayNumber - new CDate(today.Year, 1, 1).DayNumber) % DaysBetween == 0;
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CNap nap in state.Naps)
		{
			if ((everyPact || nap.Balance < 0) && nap.First.Value != "REB" && nap.Second.Value != "REB" && facts.OwnedCount(nap.Second) > 0)
			{
				Measure(state, nap);
			}
		}
	}

	/// <summary>
	/// Over each front province of the first party's land that has a passable edge to land the second holds: the second's
	/// brigades in that land, for each edge, and the first's in the province, once for each front it is on.
	/// </summary>
	public static void Measure(CGameState state, CNap nap)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		long ours = 0;
		long theirs = 0;
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller != nap.First)
			{
				continue;
			}
			foreach (List<int> front in CAreaFronts.Of(state, area))
			{
				foreach (int id in front)
				{
					bool faces = false;
					foreach (CProvinceEdge edge in state.Province(id).Edges)
					{
						if (edge.Kind != EProvinceEdgeKind.Impassable && state.Province(edge.To).Controller == nap.Second)
						{
							faces = true;
							theirs += Brigades(state, edge.To, nap.Second);
						}
					}
					if (faces)
					{
						ours += Brigades(state, id, nap.First);
					}
				}
			}
		}
		nap.OurPower = (int)ours;
		nap.TheirPower = (int)theirs;
		nap.Balance = theirs == 0 ? NoneOfTheirs : ours * CFixedPoint.Scale / theirs;
	}

	private static long Brigades(CGameState state, int province, CCountryTag owner)
	{
		long brigades = 0;
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (unit.Kind is not (EUnitKind.Navy or EUnitKind.Air) && unit.Owner == owner)
			{
				brigades += unit.SubUnits.Count;
			}
		}
		return brigades;
	}
}
