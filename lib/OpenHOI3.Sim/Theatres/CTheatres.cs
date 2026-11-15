using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// When every country's theatres are made, refreshed or looked after, and what the rest of the game asks of them. See
/// <c>docs/mechanics/theatres.md</c>, section 1.
/// </summary>
public static class CTheatres
{
	/// <summary>
	/// Each country's hour, after the units': marked theatres made from nothing - refreshed in a running game that has
	/// some - and otherwise looked after, its own hour of the day doing the most.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			RunCountryHour(state, country);
		}
	}

	/// <summary>
	/// One country's part of that, which the hour runs straight after the country's own units and before the next
	/// country's: so a province the next country takes in the hour is still this one's as its theatres are made - Wake
	/// Island, which Japan takes in the first hour of the Day of Infamy, among the United States' (section 1).
	/// </summary>
	public static void RunCountryHour(CGameState state, CCountry country)
	{
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		if (country.Strategy.TheatresDirty)
		{
			if (state.IsRunning && country.Strategy.Theatres.Count > 0)
			{
				CTheatreRefresh.Refresh(state, country);
			}
			else
			{
				CTheatreBuilder.Build(state, country);
			}
		}
		else
		{
			CTheatreUpkeep.Maintain(state, country, isMyHour: hour == (country.Index + 1) % CGameState.HoursPerDay);
		}
	}

	/// <summary>
	/// Made again at once, where the strategy is made again at once in a running game: from nothing for a country nobody
	/// plays, refreshed for a played one with theatres (section 1, 4).
	/// </summary>
	public static void RebuildAtOnce(CGameState state, CCountry country)
	{
		if (state.IsRunning && country.Strategy.Theatres.Count > 0 && state.Played.Contains(country.Tag))
		{
			CTheatreRefresh.Refresh(state, country);
		}
		else
		{
			CTheatreBuilder.Build(state, country);
		}
	}

	/// <summary>
	/// A province changing hands in an area marks the old and new holders' theatres and those of every owner of a province
	/// beside it.
	/// </summary>
	public static void ControllerChanged(CGameState state, CProvince province, CCountryTag? old, CCountryTag? controller)
	{
		if (controller is null || CSupplyFigures.MostInfrastructure(state, province) <= CSupplyNetworks.LeastInfrastructure)
		{
			return;
		}
		Mark(state, old);
		Mark(state, controller);
		foreach (CProvinceEdge edge in province.Edges)
		{
			Mark(state, state.Province(edge.To).Owner);
		}
	}

	private static void Mark(CGameState state, CCountryTag? tag)
	{
		if (state.Country(tag) is { } country)
		{
			country.Strategy.TheatresDirty = true;
		}
	}

	/// <summary>Whether a unit is a land unit: a theatre's, an army group's, an army's, a corps' or a division.</summary>
	public static bool IsLand(CUnit unit)
	{
		return unit.Kind <= EUnitKind.Division;
	}

	/// <summary>
	/// The theatre whose headquarters heads the unit's order of battle - the original's <c>FindTheatreForUnit</c> - among
	/// its owner's.
	/// </summary>
	public static CTheatre? FindFor(CGameState state, CUnit unit)
	{
		CUnit top = unit;
		while (top.Parent is not null)
		{
			top = top.Parent;
		}
		if (state.Country(unit.Owner) is not { } owner)
		{
			return null;
		}
		foreach (CTheatre theatre in owner.Strategy.Theatres)
		{
			if (theatre.Headquarters == top.Id)
			{
				return theatre;
			}
		}
		return null;
	}

	/// <summary>The theatre's headquarters, where it is still in the game.</summary>
	public static CUnit? Headquarters(CGameState state, CTheatre theatre)
	{
		if (theatre.Headquarters == 0 || state.Country(theatre.Country) is not { } country)
		{
			return null;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == theatre.Headquarters && !unit.IsRemoved)
			{
				return unit;
			}
		}
		return null;
	}

	/// <summary>
	/// A dropped theatre's headquarters taken out of the game (ours: at once, where the original queues it for the end of
	/// the hour).
	/// </summary>
	public static void RemoveHeadquarters(CGameState state, CTheatre theatre)
	{
		if (Headquarters(state, theatre) is { } unit)
		{
			Remove(state, unit);
		}
	}

	/// <summary>
	/// A unit out of the game; where it is a theatre-level unit, what it commanded is left an orphan for the country's next
	/// new headquarters.
	/// </summary>
	public static void Remove(CGameState state, CUnit unit)
	{
		var children = new List<CUnit>(unit.Children);
		bool theatre = unit.Kind == EUnitKind.Theatre;
		CCombatRetreat.RemoveFromGame(state, unit);
		if (theatre)
		{
			foreach (CUnit child in children)
			{
				child.IsOrphaned = true;
			}
		}
	}

	/// <summary>
	/// A unit put under another of the same owner and a higher level: out of where it was, onto the end of the other's.
	/// </summary>
	public static void Attach(CCountry country, CUnit unit, CUnit parent)
	{
		if (unit.Owner != parent.Owner || parent.Kind >= unit.Kind)
		{
			return;
		}
		if (unit.Parent is { } old)
		{
			old.Children.Remove(unit);
		}
		else
		{
			country.Units.Remove(unit);
		}
		unit.Parent = parent;
		parent.Children.Add(unit);
	}
}
