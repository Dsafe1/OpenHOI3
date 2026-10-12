using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

/// <summary>
/// Asks every event's and decision's conditions of every country on the start date and says what
/// held and what could not be answered:
///
///   OpenHOI3.SaveCheck conditions &lt;install folder&gt; [tag]
///
/// A look and not a check: no save keeps what a condition answered. A country event is asked of
/// each country that owns a province, a province event of each owned province with its owner, a
/// decision's <c>potential</c> and then its <c>allow</c> of each country. With a tag, that country
/// is the one played and what held for it is listed. See <c>docs/mechanics/triggers.md</c>.
/// </summary>
internal static class CConditionsTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck conditions <install folder> [tag]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add,
			fill: false, firstDays: false);
		CCountryTag? played = null;
		if (args.Length > 2)
		{
			played = new CCountryTag(args[2].ToUpperInvariant());
			if (state.Country(played) is null)
			{
				Console.Error.WriteLine($"no country is {played}");
				return 2;
			}
			state.Played.Add(played.Value);
		}

		List<CCountry> living = state.Countries.Where(c => CCountryLists.OwnedCount(state, c.Tag) > 0).ToList();
		var report = new CScriptReport();
		var clock = System.Diagnostics.Stopwatch.StartNew();
		var heldBy = new List<(string What, List<string> Who)>();
		int asked = 0;

		foreach (CEvent read in state.Events.Where(e => !e.IsTriggeredOnly))
		{
			var who = new List<string>();
			if (read.IsProvinceEvent)
			{
				foreach (CProvince province in state.Provinces)
				{
					if (province.Owner is { } owner)
					{
						asked++;
						if (CConditions.Holds(state, read.Trigger, CEventScope.Start(owner, province.Id), report))
						{
							who.Add($"{owner}:{province.Id}");
						}
					}
				}
			}
			else
			{
				foreach (CCountry country in living)
				{
					asked++;
					if (CConditions.Holds(state, read.Trigger, CEventScope.Start(country.Tag), report))
					{
						who.Add(country.Tag.Value);
					}
				}
			}
			heldBy.Add(($"event {read.Id} ({read.File}:{read.Line})", who));
		}
		int potentials = 0;
		int allowed = 0;
		foreach (CDecision read in state.Decisions)
		{
			var who = new List<string>();
			foreach (CCountry country in living)
			{
				asked++;
				CEventScope scope = CEventScope.Start(country.Tag);
				if (!CConditions.Holds(state, read.Potential, scope, report))
				{
					continue;
				}
				potentials++;
				asked++;
				if (CConditions.Holds(state, read.Allow, scope, report))
				{
					allowed++;
					who.Add(country.Tag.Value);
				}
			}
			heldBy.Add(($"decision {read.Name} ({read.File}:{read.Line})", who));
		}

		Console.WriteLine($"on {state.Date}: {living.Count} countries, {state.Events.Count} events, {state.Decisions.Count} decisions");
		Console.WriteLine($"asked {asked} conditions in {clock.ElapsedMilliseconds} ms");
		Console.WriteLine($"held: {heldBy.Count(h => h.What.StartsWith("event") && h.Who.Count > 0)} events for somebody; "
			+ $"{potentials} decisions shown and {allowed} of them allowed, counted by country");
		Console.WriteLine();
		Console.WriteLine("not answered, and why - each answered false:");
		foreach ((string key, (EScriptGap why, int times)) in report.Gaps.OrderBy(p => p.Value.Why).ThenBy(p => p.Key))
		{
			Console.WriteLine($"   {why,-10} {key,-34} {times}");
		}
		Console.WriteLine();
		Console.WriteLine(played is null ? "what held, and for whom:" : $"what held for {played}:");
		foreach ((string what, List<string> who) in heldBy)
		{
			List<string> shown = played is null ? who : who.Where(w => w.StartsWith(played.Value.Value)).ToList();
			if (shown.Count == 0)
			{
				continue;
			}
			string list = string.Join(' ', shown.Take(12)) + (shown.Count > 12 ? $" and {shown.Count - 12} more" : "");
			Console.WriteLine($"   {what}: {list}");
		}
		return 0;
	}
}
