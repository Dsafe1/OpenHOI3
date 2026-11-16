using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Events;

/// <summary>
/// The events a country's days ask: the candidates made again each month - and nowhere else, so a loaded game has none
/// until its month turns - the day's roll on each, and the hour's look at what has waited too long for a person.
/// See <c>docs/mechanics/events.md</c>, sections 1, 2 and 7.
/// </summary>
public static class CEventPass
{
	/// <summary>
	/// Every country's candidates made again: for each event in file order and each country that owns
	/// a province, those with an option, not only triggered, not fired once already, whose trigger
	/// holds looking ahead the days of this month.
	/// </summary>
	public static void RebuildCandidates(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.EventCandidates.Clear();
		}
		uint seed = (uint)state.Random.Next();
		var first = new CDate(state.Date.Year, state.Date.Month, 1);
		int monthDays = first.AddMonths(1).DayNumber - first.DayNumber;
		List<CCountry> owners = Owners(state);
		foreach (CEvent candidate in state.Events)
		{
			if (candidate.Options.Count == 0 || candidate.IsTriggeredOnly || candidate.IsProvinceEvent
				|| (candidate.FireOnlyOnce && state.FiredOnce.Contains(candidate)))
			{
				continue;
			}
			foreach (CCountry country in owners)
			{
				CEventScope scope = CEventScope.Start(country.Tag, randomState: seed, lookAheadDays: monthDays);
				if (CConditions.Holds(state, candidate.Trigger, scope))
				{
					country.EventCandidates.Add(candidate);
				}
			}
		}
	}

	/// <summary>
	/// The day's pass: each candidate of each country that owns a province is asked again and, where
	/// it holds, fires on a roll under its chance of the day.
	/// </summary>
	public static void RunDaily(CGameState state, CScriptReport? report = null)
	{
		uint seed = (uint)state.Random.Next();
		foreach (CCountry country in Owners(state))
		{
			CEventScope scope = CEventScope.Start(country.Tag, randomState: seed);
			foreach (CEvent candidate in country.EventCandidates.ToArray())
			{
				if (!CConditions.Holds(state, candidate.Trigger, scope, report))
				{
					continue;
				}
				float chance = CEventChance.OfDay(state, candidate.MeanTimeToHappen, scope, report);
				double roll = (state.Random.Next() & 0x7FFFFFFF) / 2147483648.0;
				if (chance > roll)
				{
					CEventFiring.Fire(state, candidate, scope, report);
				}
			}
		}
	}

	/// <summary>An event a person has left unanswered past its time answers itself with its first option.</summary>
	public static void RunHourly(CGameState state)
	{
		foreach (COpenEvent open in state.OpenEvents.ToArray())
		{
			if (state.TotalHours > open.DeadlineHour)
			{
				state.OpenEvents.Remove(open);
				state.Outbox.Add(new CSelectEventOptionCommand(open.Event, open.Scope, 0));
			}
		}
	}

	/// <summary>The countries that own a province, in the order they were declared.</summary>
	private static List<CCountry> Owners(CGameState state)
	{
		var owned = new HashSet<CCountryTag>();
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner)
			{
				owned.Add(owner);
			}
		}
		var owners = new List<CCountry>();
		foreach (CCountry country in state.Countries)
		{
			if (owned.Contains(country.Tag))
			{
				owners.Add(country);
			}
		}
		return owners;
	}
}
