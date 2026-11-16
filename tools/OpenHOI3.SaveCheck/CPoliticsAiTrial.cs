using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

/// <summary>
/// A trial, not a check: makes a new game with the install's Lua and one country played, and runs it hour by hour
/// with every other country's politics minister at work, the hourly trading left out:
///
///   OpenHOI3.SaveCheck politicsai &lt;install folder&gt; [days] [tag]
///
/// Germany played for sixty days with none given. It tells how many passes the new game's laws took and what they
/// changed, then each law changed, minister appointed and mobilisation the ministers posted, and what of the Lua
/// failed or read what is not built. No save keeps what the original's AI did when. See
/// <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
internal static class CPoliticsAiTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck politicsai <install folder> [days] [tag]");
			return 2;
		}
		int days = args.Length > 2 && int.TryParse(args[2], out int given) ? given : 60;
		string tag = args.Skip(2).FirstOrDefault(a => a.Length == 3 && !int.TryParse(a, out _)) ?? "GER";

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);

		// The laws as the history gives them, to say what the new game's look changed.
		var clock = Stopwatch.StartNew();
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(tag) });
		Console.WriteLine($"a new game as {tag}, {state.Date}, in {clock.ElapsedMilliseconds} ms");
		foreach (string warning in warnings.Where(w => w.Contains("politics", StringComparison.Ordinal)))
		{
			Console.WriteLine("warning: " + warning);
		}
		int startErrors = reports.Count(r => r.StartsWith("LUA Error", StringComparison.Ordinal));
		Console.WriteLine($"the new game's look at the laws: {startErrors} Lua errors");
		state.AiScripts = new CPoliticsOnly(scripts);
		var atStart = state.Countries.ToDictionary(c => c.Tag, c => new Dictionary<string, CMinister>(c.Government));

		var laws = new List<string>();
		var ministers = new List<string>();
		var mobilised = new List<string>();
		int filled = 0;
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
					case CChangeLawCommand law when state.Country(law.Country) is { } country:
						string was = country.Laws.TryGetValue(law.Group, out CLaw? old) ? old.Name : "none";
						laws.Add($"{state.Date} {law.Country} {law.Group}: {was} -> {law.Law}");
						break;
					case CChangeMinisterCommand minister when state.Country(minister.Country) is { } country:
						bool vacant = !country.Government.ContainsKey(minister.Position);
						if (vacant)
						{
							filled++;
						}
						string holder = country.Government.TryGetValue(minister.Position, out CMinister? held) ? held.Name : "nobody";
						string name = country.Ministers.TryGetValue(minister.Minister, out CMinister? chosen) ? chosen.Name : minister.Minister.ToString();
						ministers.Add($"{state.Date} {minister.Country} {minister.Position}: {holder} -> {name}");
						break;
					case CToggleMobilisationCommand toggle:
						mobilised.Add($"{state.Date} {toggle.Country} {(toggle.Mobilise ? "mobilises" : "stands down")}");
						break;
				}
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
		Console.WriteLine($"{days} days in {clock.ElapsedMilliseconds} ms: {laws.Count} laws changed, {ministers.Count} ministers " +
			$"appointed ({filled} to an empty post), {mobilised.Count} mobilisations");
		foreach (string line in laws.Take(25).Concat(ministers.Take(25)).Concat(mobilised.Take(25)))
		{
			Console.WriteLine("   " + line);
		}
		// Set against the original's: between the start save and one of 20 April 1936, 194 posts in 63 countries.
		int differ = 0;
		int differing = 0;
		foreach (CCountry country in state.Countries)
		{
			int here = state.GovernmentPositions.Count(p => atStart[country.Tag].GetValueOrDefault(p.Name) != country.Government.GetValueOrDefault(p.Name));
			differ += here;
			differing += here > 0 ? 1 : 0;
		}
		Console.WriteLine($"at {state.Date}, {differ} posts differ from the new game's in {differing} countries");
		Console.WriteLine($"Lua errors, {reports.Count(r => r.StartsWith("LUA Error", StringComparison.Ordinal))}, and what is not built:");
		foreach (var group in reports.GroupBy(r => r).OrderByDescending(g => g.Count()).Take(15))
		{
			Console.WriteLine($"   {group.Count()} x {group.Key}");
		}
		return 0;
	}
}
