using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The original's <c>RunMonthlyRebelFactionPass</c> and <c>RebelFaction_GrantIndependence</c>: once a month, after the
/// rest of the month's pass, a faction freeing a country may win it its independence. Defection never happens. See
/// <c>docs/mechanics/rebels.md</c>, section 12.
/// </summary>
public static class CRebelFactionMonth
{
	/// <summary>The chance of independence a month, in hundredths, once the land has been held long enough.</summary>
	public const int Chance = 15;

	public static void RunMonthly(CGameState state)
	{
		// A faction the month ends is marked and taken away by the next day's pass, as the original's is.
		foreach (CRebelFaction faction in new List<CRebelFaction>(state.RebelFactions))
		{
			Run(state, faction);
		}
	}

	/// <summary>
	/// One faction's month. Without a country to free, the original walks the defection of its provinces and always
	/// finds nobody to defect to, so nothing is done. With one: its candidates, the earliest day they were all taken
	/// plus <c>defect_delay</c> months, and then fifteen in a hundred.
	/// </summary>
	public static void Run(CGameState state, CRebelFaction faction)
	{
		if (faction.Independence is null)
		{
			return;
		}
		List<CProvince> candidates = Candidates(state, faction);
		if (candidates.Count == 0)
		{
			return;
		}
		CDate earliest = state.EndDate;
		foreach (CProvince province in candidates)
		{
			if (province.RebelsTook is { } taken && taken < earliest)
			{
				earliest = taken;
			}
		}
		if (earliest.AddMonths(faction.Type.DefectDelay) > state.Date)
		{
			return;
		}
		// The candidates over the same set counted again, always one, times fifteen in a hundred.
		if (state.Random.Next() % 100 < Chance)
		{
			GrantIndependence(state, faction);
		}
	}

	/// <summary>
	/// The original's <c>RebelFaction_IsIndependenceProvince</c> over the faction's list: not its owner's capital,
	/// wanted, and a core of the country to be freed - or any, where the type's <c>defection</c> is not <c>none</c>.
	/// </summary>
	public static List<CProvince> Candidates(CGameState state, CRebelFaction faction)
	{
		var found = new List<CProvince>();
		foreach (int id in faction.Provinces)
		{
			CProvince province = state.Province(id);
			if (IsIndependenceProvince(state, faction, province))
			{
				found.Add(province);
			}
		}
		return found;
	}

	/// <summary>
	/// The original's <c>RebelFaction_CollectIndependenceProvinces</c> for a country: the provinces it owns, in the map's
	/// order, whose area is the faction's province's or one beside it, that pass the same test. None where the faction's
	/// province is in no area.
	/// </summary>
	public static List<CProvince> Candidates(CGameState state, CRebelFaction faction, CCountry country)
	{
		var found = new List<CProvince>();
		Supply.CSupplyNetwork areas = Supply.CSupplyNetworks.Refresh(state);
		if (areas.AreaOf(faction.Province) is not { } home)
		{
			return found;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != country.Tag || areas.AreaOf(province.Id) is not { } area
				|| (area != home && !home.Neighbours.Contains(area)))
			{
				continue;
			}
			if (IsIndependenceProvince(state, faction, province))
			{
				found.Add(province);
			}
		}
		return found;
	}

	/// <summary>Not its owner's capital, wanted, and a core of the country to be freed - or any, with <c>defection</c>.</summary>
	private static bool IsIndependenceProvince(CGameState state, CRebelFaction faction, CProvince province)
	{
		if (state.Country(province.Owner) is { } owner && owner.Capital == province.Id)
		{
			return false;
		}
		if (!CRebelFactions.IsHostile(state, faction, province))
		{
			return false;
		}
		return faction.Type.Defection || (faction.Independence is { } freed && province.Cores.Contains(freed));
	}

	/// <summary>
	/// The faction's provinces released to the country it frees; every army of the faction that country's, out of its
	/// battles; the faction's government the country's; a revolutionary war on each country that owned any of them; and
	/// the faction gone. Given a country - <c>release_vassal = REB</c> in its scope - the provinces are that country's
	/// beside the faction's (<see cref="Candidates(CGameState, CRebelFaction, CCountry)"/>) and no war is declared.
	/// </summary>
	public static bool GrantIndependence(CGameState state, CRebelFaction faction, CCountry? by = null)
	{
		if (faction.Independence is not { } freed || state.Country(freed) is not { } country)
		{
			return false;
		}
		List<CProvince> provinces = by is null ? Candidates(state, faction) : Candidates(state, faction, by);
		if (provinces.Count == 0)
		{
			return false;
		}
		var owners = new List<CCountryTag>();
		foreach (CProvince province in provinces)
		{
			if (province.Owner is { } owner && !owners.Contains(owner))
			{
				owners.Add(owner);
			}
		}
		CRelease.Release(state, provinces, freed);
		foreach (int id in new List<int>(faction.Armies))
		{
			faction.Armies.Remove(id);
			if (CRebelFactions.FindUnit(state, id) is { } unit)
			{
				HandOver(state, unit, country);
			}
		}
		if (faction.Government is { } government)
		{
			country.GovernmentType = government.Name;
		}
		foreach (CCountryTag owner in owners)
		{
			if (by is null && state.Country(owner) is { } former)
			{
				CDeclarations.Revolution(state, country, former);
			}
		}
		faction.WantsRemoval = true;
		return true;
	}

	/// <summary>A rebel army becomes the new country's: out of its battles, falling back where it held its ground.</summary>
	private static void HandOver(CGameState state, CUnit unit, CCountry country)
	{
		CCombats.LeaveForMarch(state, unit);
		if (state.Country(unit.Owner) is { } old)
		{
			old.Units.Remove(unit);
		}
		unit.Owner = country.Tag;
		country.Units.Add(unit);
		state.ForgetPositions();
	}
}
