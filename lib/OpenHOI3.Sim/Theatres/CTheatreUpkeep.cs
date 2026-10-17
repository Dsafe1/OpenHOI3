using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// A country's theatres looked after, every hour: each one's headquarters adopted or made, its priority, and in the
/// country's own hour the theatre-level units no theatre takes. The original's <c>MaintainTheatres</c>. See
/// <c>docs/mechanics/theatres.md</c>, section 9.
/// </summary>
public static class CTheatreUpkeep
{
	/// <summary>What an area holding the acting capital adds to a theatre's priority.</summary>
	private const int CapitalArea = 10000;

	/// <summary>A theatre's priority gains a whole for this many provinces.</summary>
	private const int ProvincesPerWhole = 30;

	/// <summary>At war, a theatre's priority times this, in thousandths; with the acting capital's area, the second.</summary>
	private const long AtWar = 1500;

	private const long AtWarWithCapital = 2000;

	public static void Maintain(CGameState state, CCountry country, bool isMyHour)
	{
		if (country.Tag.Value == "REB")
		{
			return;
		}
		List<CTheatre> theatres = country.Strategy.Theatres;
		int total = 0;
		foreach (CTheatre theatre in theatres.ToArray())
		{
			if (CTheatres.Headquarters(state, theatre) is null)
			{
				Headquarters(state, country, theatre);
			}
			if (isMyHour || theatre.Priority < 1)
			{
				int priority = Priority(state, country, theatre);
				total = unchecked(total + priority);
				theatre.Priority = priority;
			}
		}
		foreach (CTheatre theatre in theatres)
		{
			if (total > 0)
			{
				theatre.Priority = theatre.Priority * 1000 / total;
			}
			else
			{
				theatre.Priority = theatres.Count == 0 ? -1 : 1000000L / (theatres.Count * 1000L);
			}
		}
		if (isMyHour)
		{
			Unclaimed(state, country);
		}
	}

	/// <summary>
	/// The first theatre-level land unit of the country's, in the order they were made, standing in the theatre and heading
	/// no other theatre, becomes its headquarters; failing that, a new one is made where a province can take it.
	/// </summary>
	private static void Headquarters(CGameState state, CCountry country, CTheatre theatre)
	{
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Kind != EUnitKind.Theatre || unit.IsRemoved || unit.Location <= 0
				|| state.Province(unit.Location).TheatreId != theatre.Id)
			{
				continue;
			}
			CTheatre? heads = CTheatres.FindFor(state, unit);
			if (heads is null || ReferenceEquals(heads, theatre))
			{
				theatre.Headquarters = unit.Id;
				MarkAgent(country, theatre);
				return;
			}
		}
		if (CTheatreHeadquarters.PickProvince(state, country, theatre) is { } province)
		{
			CTheatreHeadquarters.Make(state, country, theatre, province);
			MarkAgent(country, theatre);
		}
	}

	/// <summary>A theatre given a headquarters has its agent set its area again (<c>agents.md</c>, section 1).</summary>
	private static void MarkAgent(CCountry country, CTheatre theatre)
	{
		if (Agents.CAgentUnits.AgentFor(country, theatre.Id) is { } agent)
		{
			agent.AreaDirty = true;
		}
	}

	/// <summary>
	/// IC x 1000 of each front-worthy enemy faced, the acting capital's area, a whole for each thirty provinces, and more
	/// at war - in the original's 32-bit arithmetic.
	/// </summary>
	private static int Priority(CGameState state, CCountry country, CTheatre theatre)
	{
		int ic = 0;
		bool hot = false;
		var seen = new List<CCountryTag?>();
		foreach (CFront front in theatre.Fronts)
		{
			CCountryTag? enemy = front.Enemy;
			if (seen.Contains(enemy))
			{
				continue;
			}
			seen.Add(enemy);
			if (enemy is not { } tag || !CFrontWorthy.Is(state, country, tag))
			{
				continue;
			}
			ic = unchecked(ic + (state.Country(tag)?.TotalIc ?? 0) * 1_000_000);
			if (tag != country.Tag && CCountryLists.IsEnemy(state, country, tag))
			{
				hot = true;
			}
		}
		int priority = ic;
		CSupplyArea? capital = CSupplyNetworks.Refresh(state).AreaOf(country.ActingCapital);
		bool capitalIn = capital is not null && theatre.Areas.Contains(capital);
		if (capitalIn)
		{
			priority = unchecked(ic + CapitalArea);
		}
		if (theatre.AllProvinces.Count > 0)
		{
			priority = unchecked(priority + theatre.AllProvinces.Count / ProvincesPerWhole * 1000);
		}
		if (hot)
		{
			long times = capitalIn ? AtWarWithCapital : AtWar;
			priority = (int)(times * priority / 1000);
		}
		return priority;
	}

	/// <summary>
	/// In the country's hour, each theatre-level unit that heads no theatre: for a country nobody plays, or before the game
	/// runs, disbanded; for a played country in a running game, given a theatre of its own.
	/// </summary>
	private static void Unclaimed(CGameState state, CCountry country)
	{
		bool disband = !state.Played.Contains(country.Tag) || !state.IsRunning;
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Kind != EUnitKind.Theatre || unit.IsRemoved || CTheatres.FindFor(state, unit) is not null)
			{
				continue;
			}
			if (disband)
			{
				state.Outbox.Add(new Commands.CDisbandCommand(unit.Id));
			}
			else
			{
				CTheatre theatre = CTheatreProvinces.New(state, country.Tag);
				theatre.Headquarters = unit.Id;
				country.Strategy.Theatres.Add(theatre);
			}
		}
	}
}
