using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A country's whole order of battle sorted into the theatres its units stand in, at its pass's gate: each unit whose
/// commander stands in another theatre, and each unit at the top, put under the headquarters of the theatre it stands in.
/// The original's <c>CEU3AI_SortUnitsIntoTheatres</c> and <c>CEU3AI_GetUnitTheatreProvince</c>. See
/// <c>docs/mechanics/agents.md</c>, section 2.1.
/// </summary>
public static class CTheatreSort
{
	/// <summary>The nearest headquarters is looked for from this distance.</summary>
	private const long Far = 1_000_000;

	public static void Sort(CGameState state, CCountry country)
	{
		var listed = new List<CUnit>();
		foreach (CUnit unit in PostOrder(country))
		{
			if (unit.Kind == EUnitKind.Theatre || unit.IsRemoved || TheatreAt(state, country, unit) is not { } theatre)
			{
				continue;
			}
			CUnit current = unit;
			CUnit? list = unit.Parent is null ? unit : null;
			while (current.Parent is { } parent && current.Kind != EUnitKind.Theatre)
			{
				int place = Place(state, country, parent);
				if (place > 0 && state.Province(place).TheatreId == theatre.Id)
				{
					current = parent;
					continue;
				}
				list = current;
				break;
			}
			if (list is not null && !listed.Contains(list))
			{
				listed.Add(list);
			}
		}
		foreach (CUnit unit in listed)
		{
			if (unit.Kind == EUnitKind.Air && HoldsCarrierWing(unit))
			{
				continue;
			}
			if (TheatreAt(state, country, unit) is { } theatre && CTheatres.Headquarters(state, theatre) is { } headquarters)
			{
				state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, headquarters.Id));
			}
		}
	}

	/// <summary>The country's units with each one's subordinates before it, the original's list after a load.</summary>
	private static List<CUnit> PostOrder(CCountry country)
	{
		var order = new List<CUnit>();
		foreach (CUnit top in country.Units)
		{
			Add(top, order);
		}
		return order;
	}

	private static void Add(CUnit unit, List<CUnit> order)
	{
		foreach (CUnit child in unit.Children)
		{
			Add(child, order);
		}
		order.Add(unit);
	}

	private static bool HoldsCarrierWing(CUnit unit)
	{
		foreach (CSubUnit wing in unit.SubUnits)
		{
			if (wing.Type.Flag(Combat.CAirFire.IsCag))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The country's theatre stamped on where the unit counts as standing, or null.</summary>
	private static CTheatre? TheatreAt(CGameState state, CCountry country, CUnit unit)
	{
		int place = Place(state, country, unit);
		return place > 0 && CTheatreProvinces.Of(state, place) is { } theatre && theatre.Country == country.Tag ? theatre : null;
	}

	/// <summary>
	/// Where a unit counts as standing: a land unit aboard a fleet by where the fleet is going on the country's land, else
	/// its base; a carrying fleet by where it is going on the country's land; a unit with a base by its base, whoever's; a
	/// land unit by where it is going or where it is, on the country's land; else the headquarters a search finds.
	/// </summary>
	private static int Place(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Transport is { } fleet)
		{
			if (fleet.IsMoving && Ours(state, country, fleet.Path[^1]))
			{
				return fleet.Path[^1];
			}
			if (fleet.Base > 0)
			{
				return fleet.Base;
			}
		}
		if (unit.Kind == EUnitKind.Navy && unit.Passengers.Count > 0 && unit.IsMoving && Ours(state, country, unit.Path[^1]))
		{
			return unit.Path[^1];
		}
		if (unit.Base > 0)
		{
			return unit.Base;
		}
		if (unit.IsMoving && Ours(state, country, unit.Path[^1]))
		{
			return unit.Path[^1];
		}
		if (Ours(state, country, unit.Location))
		{
			return unit.Location;
		}
		return Search(state, country, CAgentUnits.DestinationOrPlace(unit));
	}

	private static bool Ours(CGameState state, CCountry country, int province)
	{
		return province > 0 && !state.Province(province).IsSea && state.Province(province).Controller == country.Tag;
	}

	/// <summary>
	/// The original's search: the headquarters of the first theatre covering the province's area where the country may
	/// operate in it; otherwise the nearest headquarters - each distance measured from the last best one found.
	/// </summary>
	private static int Search(CGameState state, CCountry country, int from)
	{
		if (from <= 0)
		{
			return 0;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? area = network.AreaOf(from);
		bool mayOperate = state.Country(state.Province(from).Controller) is { } holder
			&& CSupplyRelations.CanOperateIn(state, country, holder);
		long best = Far;
		int current = from;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (CTheatres.Headquarters(state, theatre) is not { Location: > 0 } headquarters)
			{
				continue;
			}
			if (area is not null && mayOperate && Covers(state, country, theatre, area))
			{
				return headquarters.Location;
			}
			long distance = CMapDistance.Between(state.Province(current), state.Province(headquarters.Location), state.MapWidth);
			if (distance < best)
			{
				best = distance;
				current = headquarters.Location;
			}
		}
		return current;
	}

	/// <summary>The original's <c>CTheatre_CoversArea</c>: any area of the theatre's own country, or one of its focus areas.</summary>
	public static bool Covers(CGameState state, CCountry country, CTheatre theatre, CSupplyArea area)
	{
		return area.Controller == theatre.Country || CFocusAreas.Of(state, country, theatre).Contains(area);
	}
}
