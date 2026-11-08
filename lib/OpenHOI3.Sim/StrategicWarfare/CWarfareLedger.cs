using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.StrategicWarfare;

/// <summary>
/// The played country's records of what its wars have cost it and its enemies, one a country it has fought, kept when the war ends. The original's
/// <c>GameStrategicWarfare</c>, saved at the top level as <c>strategic_warfare</c>. See <c>docs/mechanics/strategicwarfare.md</c>.
/// </summary>
public sealed class CWarfareLedger
{
	/// <summary>Whose records they are.</summary>
	public CCountryTag? Player { get; set; }

	/// <summary>The records, in the countries' own order.</summary>
	public List<CWarfareRecord> Records { get; } = new();

	public CWarfareRecord? Of(CCountryTag enemy) => Records.FirstOrDefault(record => record.Enemy == enemy);

	public void Clear()
	{
		Records.Clear();
	}

	/// <summary>Whole years from the start to the defines' end date, and one more.</summary>
	public static int YearsOf(CGameState state, CDate start)
	{
		return Math.Max(1, state.EndDate.Year - start.Year + 1);
	}

	/// <summary>A new record for an enemy, placed by the country's order.</summary>
	public CWarfareRecord Add(CGameState state, CCountryTag enemy)
	{
		var record = new CWarfareRecord(enemy, state.TotalHours, YearsOf(state, state.Date));
		Records.Add(record);
		var order = new Dictionary<CCountryTag, int>();
		for (int index = 0; index < state.Countries.Count; index++)
		{
			order[state.Countries[index].Tag] = index;
		}
		Records.Sort((a, b) => order.GetValueOrDefault(a.Enemy).CompareTo(order.GetValueOrDefault(b.Enemy)));
		return record;
	}

	/// <summary>
	/// The first thing the day does: the played country's records are started afresh where the played country has changed; each running record moves a day on and ends where its
	/// war has, an ended one starts again where its war has come back, zeroed; and every enemy with no record gets one.
	/// </summary>
	public void DailyUpdate(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		CCountryTag? played = state.Played.Count == 0 ? null : state.Played.First();
		if (played != Player)
		{
			Clear();
			Player = played;
		}
		if (Player is not { } player || state.Country(player) is not { } country)
		{
			return;
		}
		List<CCountry> enemies = CCountryLists.Enemies(state, country);
		foreach (CWarfareRecord record in Records)
		{
			bool atWar = enemies.Any(enemy => enemy.Tag == record.Enemy);
			if (!record.Ended)
			{
				foreach (CWarfareCategory category in record.Categories)
				{
					category.AdvanceDay();
				}
				if (!atWar)
				{
					record.Ended = true;
					record.EndHour = state.TotalHours;
				}
			}
			else if (atWar)
			{
				foreach (CWarfareCategory category in record.Categories)
				{
					category.Zero();
				}
				record.Ended = false;
				record.StartHour = state.TotalHours;
			}
		}
		foreach (CCountry enemy in enemies)
		{
			if (Of(enemy.Tag) is null)
			{
				Add(state, enemy.Tag);
			}
		}
	}
}
