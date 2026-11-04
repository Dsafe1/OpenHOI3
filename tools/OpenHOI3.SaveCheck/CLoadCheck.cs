using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// Loads a save of the original's as a game here and says how it went:
///
///   OpenHOI3.SaveCheck load &lt;install folder&gt; &lt;savegame&gt; [hours]
///
/// what of the save is not loaded yet, block by block; the loaded game's figures set beside the save's where the loader
/// reads them, and the ones it works out again - IC - beside nothing; and then the hours given run, to show the game goes
/// on from there. Nothing is written. See <c>docs/mechanics/savegame.md</c>, section 4.
/// </summary>
public static class CLoadCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck load <install folder> <savegame> [hours]");
			return 2;
		}
		int hours = args.Length > 3 && int.TryParse(args[3], out int h) ? h : 24;
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var clock = Stopwatch.StartNew();
		(CGameState state, CSaveReport report) = CSaveLoader.Load(directories, map, localisation, countries, args[2],
			warn: warnings.Add);
		Console.WriteLine($"loaded {System.IO.Path.GetFileName(args[2])} in {clock.ElapsedMilliseconds} ms: " +
			$"{state.Date} {state.Hour}:00, {report.Player} played, seed {state.Seed}");
		Console.WriteLine("not loaded yet:");
		foreach (string line in report.Lines())
		{
			Console.WriteLine("  " + line);
		}

		// What was read, set beside the save it was read from: a miss here is a fault of the reading.
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		int owners = 0, ownersSame = 0, laws = 0, lawsSame = 0, levels = 0, levelsSame = 0;
		foreach (ClausewitzNode node in save.Children)
		{
			if (int.TryParse(node.Key, out int id) && id > 0 && id < state.Provinces.Count)
			{
				owners++;
				CProvince province = state.Province(id);
				string saved = node.Get("owner")?.Value?.Trim('"') ?? "";
				if (saved == (province.Owner?.Value ?? ""))
				{
					ownersSame++;
				}
			}
			else if (CCountryTag.TryParse(node.Key, out CCountryTag tag) && state.Country(tag) is { } country)
			{
				foreach (CLawGroup group in state.LawGroups)
				{
					laws++;
					if ((node.Get(group.Name)?.Value ?? "") == (country.Laws.GetValueOrDefault(group.Name)?.Name ?? ""))
					{
						lawsSame++;
					}
				}
				foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
				{
					if (entry.Key is { } key && entry.IsBlock && state.Technologies.ContainsKey(key))
					{
						levels++;
						if (country.TechnologyLevels.GetValueOrDefault(key) == (int)CSaveValues.Whole(entry.Children[0].Value))
						{
							levelsSame++;
						}
					}
				}
			}
		}
		Console.WriteLine($"owners {ownersSame} of {owners}, laws {lawsSame} of {laws}, technology levels {levelsSame} of {levels}");
		foreach (CCountry country in state.Countries.Where(c => c.Tag.Value is "GER" or "ENG" or "SOV" or "USA").ToList())
		{
			Console.WriteLine($"  {country.Tag}: IC {country.TotalIc} of base {country.BaseIc}, manpower {country.Manpower}, " +
				$"officers {country.Officers}, unity {country.NationalUnity}, faction '{country.Faction}'");
		}

		clock.Restart();
		for (int hour = 0; hour < hours; hour++)
		{
			CTick.RunHour(state);
		}
		Console.WriteLine($"ran {hours} hours in {clock.ElapsedMilliseconds} ms, to {state.Date} {state.Hour}:00");
		Console.WriteLine($"{warnings.Count} warnings; the first few:");
		foreach (string warning in warnings.Take(8))
		{
			Console.WriteLine("  " + warning);
		}
		return 0;
	}
}
