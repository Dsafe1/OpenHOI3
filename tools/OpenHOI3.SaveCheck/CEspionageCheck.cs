using System;
using System.Collections.Generic;
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
/// Makes a new game as the save's player gets it, with the install's Lua and its two days run, and sets every country's
/// spies beside a savegame the original wrote on the start date:
///
///   OpenHOI3.SaveCheck espionage &lt;install folder&gt; &lt;start-date savegame&gt; [later savegame] [examples]
///
/// The free spies, and in every presence the level, the priority, the nine mission weights and the covert-ops points.
/// Given a later save of a game from that start, it then plays on to that save's hour and sets the player's country's
/// free spies and presences beside it - the one country whose espionage no AI moves, the AI's spying being unbuilt.
/// See <c>docs/mechanics/espionage.md</c>, section 14.
/// </summary>
internal static class CEspionageCheck
{
	private sealed class Tally
	{
		public int Same { get; set; }

		public int Compared { get; set; }

		public List<string> Examples { get; } = new();

		public void Add(bool same, Func<string> example, int keep)
		{
			Compared++;
			if (same)
			{
				Same++;
			}
			else if (Examples.Count < keep)
			{
				Examples.Add(example());
			}
		}
	}

	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck espionage <install folder> <start-date savegame> [later savegame] [examples]");
			return 2;
		}
		string? laterPath = args.Length > 3 && !int.TryParse(args[3], out _) ? args[3] : null;
		int examples = args.Skip(3).Select(arg => int.TryParse(arg, out int n) ? n : -1).FirstOrDefault(n => n >= 0, 5);

		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		string player = save.GetString("player");
		using var scripts = new CAiScripts(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(player) }, date: CStartSave.Date(args[2]));
		Console.WriteLine($"start save: {save.GetString("date")}, player {player}; {state.CovertOperations.Count} covert operations read");

		bool all = Compare(state, save, examples, everyone: true);

		if (laterPath is not null)
		{
			ClausewitzNode later = ClausewitzParser.ParseFile(laterPath);
			if (!TryHour(later.GetString("date"), out long until) || until <= state.TotalHours)
			{
				Console.Error.WriteLine($"the later save is dated '{later.GetString("date")}', which is not after the new game's first hour");
				return 2;
			}
			int hours = 0;
			while (state.TotalHours < until)
			{
				CTick.RunHour(state);
				var posted = new List<CCommand>(state.Outbox);
				state.Outbox.Clear();
				foreach (CCommand command in posted)
				{
					if (command.MayExecute(state))
					{
						command.Execute(state);
					}
				}
				hours++;
			}
			Console.WriteLine();
			Console.WriteLine($"later save: {later.GetString("date")}, {hours} hours played on; {player} alone is set beside it");
			all &= Compare(state, later, examples, everyone: false, only: player);
		}

		Console.WriteLine();
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return all ? 0 : 1;
	}

	/// <summary>Every country's spies beside the save's, or one country's; answers whether everything agreed.</summary>
	private static bool Compare(CGameState state, ClausewitzNode save, int examples, bool everyone, string? only = null)
	{
		var tallies = new Dictionary<string, Tally>();
		Tally Of(string name) => tallies.TryGetValue(name, out Tally? tally) ? tally : tallies[name] = new Tally();
		// A spy caught is a roll, so which ones were caught cannot agree; how many may.
		long oursAbroad = 0, theirsAbroad = 0;

		foreach (ClausewitzNode block in save.Children)
		{
			if (!block.IsBlock || block.Key is not { Length: CCountryTag.Length } key || !CCountryTag.TryParse(key, out CCountryTag tag)
				|| state.Country(tag) is not { } country || block.Get("spy_allocation") is null)
			{
				continue;
			}
			if (!everyone && key != only)
			{
				continue;
			}
			CFixedPoint spies = Number(block.GetString("spies"));
			Of("free spies").Add(country.FreeSpies == spies, () => $"{tag}: ours {country.FreeSpies}, theirs {spies}", examples);

			// The save's arrays count the original's placeholder first, so a country's place there is its index here plus one.
			List<long> levels = Numbers(block.Get("spy_allocation"));
			List<long> priorities = Whole(block.Get("spy_priority"));
			List<long> missions = Whole(block.Get("spy_mission"));
			List<long> points = Numbers(block.Get("covert_ops_points"));
			foreach (CCountry other in state.Countries)
			{
				int at = other.Index + 1;
				CSpyPresence presence = CGameState.Presence(country, other);
				string pair = $"{tag} in {other.Tag}";
				if (at < levels.Count)
				{
					if (other != country)
					{
						oursAbroad += presence.Level;
						theirsAbroad += levels[at];
					}
					Of(other == country ? "level at home" : "level abroad").Add(presence.Level == levels[at],
						() => $"{pair}: ours {new CFixedPoint(presence.Level)}, theirs {new CFixedPoint(levels[at])}"
							+ (everyone ? $"  ({Terms(state, country, other)})" : ""), examples);
				}
				if (at < priorities.Count)
				{
					Of("priority").Add(presence.Priority == priorities[at],
						() => $"{pair}: ours {presence.Priority}, theirs {priorities[at]}", examples);
				}
				if ((at + 1) * CSpyPresence.MissionCount <= missions.Count)
				{
					bool same = true;
					for (int m = 0; m < CSpyPresence.MissionCount; m++)
					{
						same &= presence.Missions[m] == missions[at * CSpyPresence.MissionCount + m];
					}
					Of("mission weights").Add(same, () => $"{pair}: ours {string.Join(' ', presence.Missions)}, theirs "
						+ string.Join(' ', missions.Skip(at * CSpyPresence.MissionCount).Take(CSpyPresence.MissionCount)), examples);
				}
				if (at < points.Count)
				{
					Of("covert-ops points").Add(presence.CovertOpsPoints == points[at],
						() => $"{pair}: ours {new CFixedPoint(presence.CovertOpsPoints)}, theirs {new CFixedPoint(points[at])}", examples);
				}
			}
		}

		Console.WriteLine($"spies abroad in all: ours {new CFixedPoint(oursAbroad)}, theirs {new CFixedPoint(theirsAbroad)}");
		bool all = true;
		foreach ((string name, Tally tally) in tallies)
		{
			Console.WriteLine($"{name}: {tally.Same} of {tally.Compared} agree");
			foreach (string example in tally.Examples)
			{
				Console.WriteLine($"    {example}");
			}
			all &= tally.Same == tally.Compared;
		}
		return all;
	}

	/// <summary>A new game's score for the pair, term by term, as the state now stands (espionage.md, section 2).</summary>
	private static string Terms(CGameState state, CCountry country, CCountry other)
	{
		int? relation = state.AiScripts?.NonHistoricalRelation(state, country, other);
		string continent = state.Province(country.ActingCapital).Continent;
		string otherContinent = state.Province(other.ActingCapital).Continent;
		return $"relation {relation}, neighbours {CCountryFacts.Of(state).AreNeighbours(country.Tag, other.Tag)}, "
			+ $"continents {continent}/{otherContinent}, factions '{country.Faction}'/'{other.Faction}', "
			+ $"parties {country.RulingIdeology}/{other.RulingIdeology}, war {CCountryLists.AtWar(state, country.Tag, other.Tag)}, "
			+ $"alliance {CCountryLists.HasAlliance(state, country.Tag, other.Tag)}, major {OpenHOI3.Sim.Agreements.COfferRules.IsMajor(country)}";
	}

	/// <summary>A save's date, <c>1936.1.4.12</c>, as the game's hours.</summary>
	private static bool TryHour(string text, out long hours)
	{
		hours = 0;
		string[] parts = text.Split('.');
		if (parts.Length < 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
			|| !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)
			|| !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
		{
			return false;
		}
		int hour = parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ? h : 0;
		hours = (long)new CDate(year, month, day).DayNumber * CGameState.HoursPerDay + hour;
		return true;
	}

	private static List<long> Numbers(ClausewitzNode? block)
	{
		var values = new List<long>();
		foreach (ClausewitzNode child in block?.Children ?? new List<ClausewitzNode>())
		{
			if (child.Value is not null)
			{
				values.Add(Number(child.Value).Raw);
			}
		}
		return values;
	}

	private static List<long> Whole(ClausewitzNode? block)
	{
		var values = new List<long>();
		foreach (ClausewitzNode child in block?.Children ?? new List<ClausewitzNode>())
		{
			if (child.Value is not null && long.TryParse(child.Value, out long value))
			{
				values.Add(value);
			}
		}
		return values;
	}

	private static CFixedPoint Number(string text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
