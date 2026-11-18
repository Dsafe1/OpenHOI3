using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

/// <summary>
/// A trial with a check at its end: makes a new game with the install's Lua and one country played, and runs it hour by
/// hour with every other country's intelligence and politics ministers at work, the hourly trading left out:
///
///   OpenHOI3.SaveCheck espionageai &lt;install folder&gt; [days] [tag] [save to set beside]
///
/// Germany played for 110 days with none given - to the 20 April 1936 of the reference save. It tells how many
/// countries' ministers acted and what they posted, what of the Lua failed or read what is not built, and given a save
/// of that day sets every AI country's priority towards every country owning a province beside it: the reading set the
/// script's rules against that save and found 5,802 of 5,929 alike. The missions are drawn by the C runtime's
/// generator and are not set beside it. See <c>docs/mechanics/espionage.md</c>, section 15.
/// </summary>
internal static class CEspionageAiTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck espionageai <install folder> [days] [tag] [save to set beside]");
			return 2;
		}
		int days = args.Length > 2 && int.TryParse(args[2], out int given) ? given : 110;
		string tag = args.Skip(2).FirstOrDefault(a => a.Length == 3 && !int.TryParse(a, out _)) ?? "GER";
		string? savePath = args.Skip(2).FirstOrDefault(a => a.EndsWith(".hoi3", StringComparison.OrdinalIgnoreCase));

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);
		var clock = Stopwatch.StartNew();
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(tag) });
		Console.WriteLine($"a new game as {tag}, {state.Date}, in {clock.ElapsedMilliseconds} ms");
		state.AiScripts = new CPoliticsOnly(scripts);

		var acted = new HashSet<CCountryTag>();
		int priorities = 0;
		int missions = 0;
		clock.Restart();
		for (int hour = 0; hour < days * CGameState.HoursPerDay; hour++)
		{
			CTick.RunHour(state);
			var posted = new List<CCommand>(state.Outbox);
			state.Outbox.Clear();
			foreach (CCommand command in posted)
			{
				switch (command)
				{
					case CChangeSpyPriorityCommand priority:
						priorities++;
						acted.Add(priority.Actor);
						break;
					case CChangeSpyMissionCommand mission:
						missions++;
						acted.Add(mission.Actor);
						break;
				}
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
		Console.WriteLine($"{days} days in {clock.ElapsedMilliseconds} ms, to {state.Date}: {acted.Count} countries' ministers acted, " +
			$"posting {priorities} priorities and {missions} mission weights");
		if (state.Country(new CCountryTag("FRA")) is { } france)
		{
			Console.WriteLine("France's priorities above nought: " + string.Join(" ", state.Countries
				.Where(c => CGameState.Presence(france, c).Priority > 0)
				.Select(c => $"{c.Tag}:{CGameState.Presence(france, c).Priority}")));
		}
		Console.WriteLine($"Lua errors, {reports.Count(r => r.StartsWith("LUA Error", StringComparison.Ordinal))}, and what is not built:");
		foreach (var group in reports.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(15))
		{
			Console.WriteLine($"   {group.Count()} x {group.Key}");
		}

		if (savePath is not null)
		{
			Compare(state, ClausewitzParser.ParseFile(savePath), tag);
		}
		return 0;
	}

	/// <summary>Every AI country's priority towards every country owning a province, beside the save's.</summary>
	private static void Compare(CGameState state, ClausewitzNode save, string player)
	{
		bool[] owns = OpenHOI3.Sim.Diplomacy.CRelations.Owning(state);
		int same = 0, higher = 0, lower = 0;
		var examples = new List<string>();
		foreach (ClausewitzNode block in save.Children)
		{
			if (!block.IsBlock || block.Key is not { Length: CCountryTag.Length } key || key == player || key == "REB"
				|| !CCountryTag.TryParse(key, out CCountryTag tag) || state.Country(tag) is not { } country || !owns[country.Index]
				|| block.Get("spy_priority") is not { } list)
			{
				continue;
			}
			List<long> theirs = list.Children.Where(c => c.Value is not null)
				.Select(c => long.Parse(c.Value!, CultureInfo.InvariantCulture)).ToList();
			foreach (CCountry other in state.Countries)
			{
				int at = other.Index + 1;
				if (other == country || !owns[other.Index] || at >= theirs.Count)
				{
					continue;
				}
				long ours = CGameState.Presence(country, other).Priority;
				if (ours == theirs[at])
				{
					same++;
					continue;
				}
				if (ours > theirs[at])
				{
					higher++;
				}
				else
				{
					lower++;
				}
				if (examples.Count < 12)
				{
					examples.Add($"{country.Tag} on {other.Tag}: ours {ours}, theirs {theirs[at]}");
				}
			}
		}
		Console.WriteLine($"priorities abroad beside the save: {same} alike, {higher} ours higher, {lower} ours lower");
		foreach (string example in examples)
		{
			Console.WriteLine("   " + example);
		}
	}
}
