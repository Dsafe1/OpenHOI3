using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

/// <summary>
/// Strategic warfare's allies entries (<c>docs/mechanics/politics.md</c>, section 6) made after the earlier save, set
/// beside a later save of the same game: for each country, how many entries and their sum, ours and the save's
/// <c>strategic_warfare</c> block's <c>alliance</c> list. A look: the battles fought, and who is in them at midnight, are
/// the AI's and the dice's.
/// </summary>
public static class CAlliesCheck
{
	public static void Report(CGameState state, ClausewitzNode earlier, ClausewitzNode later, string indent, int examples = 12)
	{
		if (!CSaveValues.TryHours(earlier.Get("date")?.Value, out long from))
		{
			return;
		}
		int both = 0, savedOnly = 0, oursOnly = 0, sameCount = 0, sameSum = 0;
		var lines = new List<string>();
		foreach (CCountry country in state.Countries)
		{
			var saved = new List<long>();
			if (later.Get(country.Tag.ToString())?.Get("strategic_warfare")?.Get("alliance") is { } list)
			{
				foreach (ClausewitzNode entry in list.Children)
				{
					if (CSaveValues.TryHours(entry.Key?.Trim('"'), out long hour) && hour > from)
					{
						saved.Add(CSaveValues.Fixed(entry.Value?.Trim('"')).Raw);
					}
				}
			}
			List<long> ours = country.StrategicWarfare.Allies.Where(e => e.Hour > from).Select(e => e.Value).ToList();
			if (saved.Count == 0 && ours.Count == 0)
			{
				continue;
			}
			if (saved.Count > 0 && ours.Count > 0)
			{
				both++;
			}
			else if (saved.Count > 0)
			{
				savedOnly++;
			}
			else
			{
				oursOnly++;
			}
			sameCount += saved.Count == ours.Count ? 1 : 0;
			sameSum += saved.Sum() == ours.Sum() ? 1 : 0;
			if (lines.Count < examples)
			{
				lines.Add($"{country.Tag}: saved {saved.Count} entries, {new CFixedPoint(saved.Sum())}; ours {ours.Count}, {new CFixedPoint(ours.Sum())}");
				lines.Add("    saved: " + string.Join(" ", saved.Select(v => new CFixedPoint(v).ToString())));
				lines.Add("    ours:  " + string.Join(" ", ours.Select(v => new CFixedPoint(v).ToString())));
				lines.Add($"    its IC now {country.TotalIc}, base {country.BaseIc}; saved IC " +
					$"{later.Get(country.Tag.ToString())?.Get("ic")?.Value ?? later.Get(country.Tag.ToString())?.Get("max_ic")?.Value ?? "?"}");
			}
		}
		Console.WriteLine($"{indent}countries with entries: {both} in both, {savedOnly} in the save alone, {oursOnly} in ours alone; " +
			$"the same count {sameCount}, the same sum {sameSum}");
		foreach (string line in lines)
		{
			Console.WriteLine($"{indent}  {line}");
		}
	}
}
