using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// Every country's base and total IC as they stood before the day's IC was worked out: what a country whose own day
/// comes later in the countries' order still holds when an earlier country's day reads it. The original runs a whole
/// day for one country before the next, while the daily pass here runs each step for every country in turn, so a step
/// that reads another country's IC asks this for the countries whose day has not come yet (<c>alignment.md</c>,
/// section 3). Taken in the day and dropped with it; nothing keeps it.
/// </summary>
public sealed class CIcAtDayStart
{
	private readonly Dictionary<CCountry, (int Base, int Total)> _figures = new();

	/// <summary>Every country's base and total IC as they stand now.</summary>
	public static CIcAtDayStart Take(CGameState state)
	{
		var taken = new CIcAtDayStart();
		foreach (CCountry country in state.Countries)
		{
			taken._figures[country] = (country.BaseIc, country.TotalIc);
		}
		return taken;
	}

	/// <summary>The country's base IC as it stood when this was taken; its present one for a country made since.</summary>
	public int BaseIc(CCountry country) => _figures.TryGetValue(country, out (int Base, int Total) figures) ? figures.Base : country.BaseIc;

	/// <summary>The country's total IC as it stood when this was taken; its present one for a country made since.</summary>
	public int TotalIc(CCountry country) => _figures.TryGetValue(country, out (int Base, int Total) figures) ? figures.Total : country.TotalIc;
}
