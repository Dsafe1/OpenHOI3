using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

/// <summary>
/// Makes a new game as a player gets it and sets every country's AI strategy beside the one a savegame the original
/// wrote on the start date keeps under <c>ai</c>:
///
///   OpenHOI3.SaveCheck aistrategy &lt;install folder&gt; &lt;start-date savegame&gt; [tag] [examples]
///
/// The personality, and for each list - <c>conquer_prov</c>, <c>defend_prov</c>, <c>threat</c>, <c>antagonize</c>,
/// <c>befriend</c>, <c>protect</c>, <c>rival</c>, <c>vassal</c> - whether it is the save's entry for entry, in order
/// and weight; and where not, whether it holds the same entries in another order. Given a tag, that country's lists
/// both ways. See <c>docs/mechanics/aistrategy.md</c>, section 13.
/// </summary>
internal static class CAiStrategyCheck
{
	private static readonly string[] Lists =
		{ "conquer_prov", "defend_prov", "threat", "antagonize", "befriend", "protect", "rival", "vassal" };

	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck aistrategy <install folder> <start-date savegame> [tag] [examples]");
			return 2;
		}
		string? only = args.Length > 3 && args[3].Length == 3 && !int.TryParse(args[3], out _) ? args[3] : null;
		int examples = args.Skip(3).Select(a => int.TryParse(a, out int n) ? n : -1).FirstOrDefault(n => n >= 0, 5);
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		using var scripts = new OpenHOI3.Lua.CAiScripts(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
			played: new[] { new CCountryTag(save.GetString("player")) }, date: CStartSave.Date(args[2]));
		Console.WriteLine($"a new game made in {clock.ElapsedMilliseconds} ms; savegame {save.GetString("date")}, player {save.GetString("player")}");

		var strategies = new Dictionary<string, ClausewitzNode>();
		foreach (ClausewitzNode block in save.Children)
		{
			if (block.IsBlock && block.Key is { Length: 3 } key && block.Get("ai") is { IsBlock: true } ai)
			{
				strategies[key] = ai;
			}
		}
		if (only is not null)
		{
			Show(state, strategies, only);
			return 0;
		}
		if (args.Skip(3).FirstOrDefault(a => a.StartsWith("explain=", StringComparison.Ordinal)) is { } explain)
		{
			string[] pair = explain["explain=".Length..].Split(':');
			Explain(state, new CCountryTag(pair[0]), new CCountryTag(pair[1]));
			return 0;
		}

		int personalities = 0;
		int compared = 0;
		var exact = new Dictionary<string, int>();
		var sameSet = new Dictionary<string, int>();
		var misses = new Dictionary<string, List<string>>();
		var personalityMisses = new List<string>();
		foreach (CCountry country in state.Countries)
		{
			if (!strategies.TryGetValue(country.Tag.Value, out ClausewitzNode? ai) || !country.Strategy.IsMade)
			{
				continue;
			}
			compared++;
			if (Personality(country.Strategy.Personality) == ai.GetString("personality"))
			{
				personalities++;
			}
			else
			{
				personalityMisses.Add($"{country.Tag}: {Personality(country.Strategy.Personality)}, save {ai.GetString("personality")}");
			}
			foreach (string list in Lists)
			{
				List<(string Key, int Weight)> ours = Ours(country.Strategy, list);
				List<(string Key, int Weight)> theirs = Theirs(ai, list);
				if (ours.SequenceEqual(theirs))
				{
					exact[list] = exact.GetValueOrDefault(list) + 1;
				}
				else if (ours.OrderBy(e => e.Key).SequenceEqual(theirs.OrderBy(e => e.Key)))
				{
					sameSet[list] = sameSet.GetValueOrDefault(list) + 1;
				}
				else
				{
					if (!misses.TryGetValue(list, out List<string>? found))
					{
						misses[list] = found = new List<string>();
					}
					found.Add($"{country.Tag}: ours {Text(ours)}\n         save {Text(theirs)}");
				}
			}
		}
		Console.WriteLine($"{compared} countries with a strategy both here and in the save");
		var unmade = new List<string>();
		foreach ((string tag, ClausewitzNode ai) in strategies)
		{
			if (state.Country(new CCountryTag(tag)) is not { Strategy.IsMade: true })
			{
				bool empty = Lists.All(list => !ai.All(list).Any());
				unmade.Add(empty ? tag : tag + "*");
			}
		}
		Console.WriteLine($"{unmade.Count} in the save not made here (* where the save's has entries): {string.Join(" ", unmade)}");
		Console.WriteLine($"personality: {personalities} of {compared}");
		foreach (string miss in personalityMisses.Take(examples))
		{
			Console.WriteLine($"   {miss}");
		}
		foreach (string list in Lists)
		{
			Console.WriteLine($"{list}: {exact.GetValueOrDefault(list)} of {compared} exactly, " +
				$"{sameSet.GetValueOrDefault(list)} more with the same entries in another order");
			foreach (string miss in misses.GetValueOrDefault(list, new List<string>()).Take(examples))
			{
				Console.WriteLine($"   {miss}");
			}
		}
		Console.WriteLine($"{warnings.Count} warnings while loading");
		return 0;
	}

	/// <summary>One country's lists, ours and the save's, one beside the other.</summary>
	private static void Show(CGameState state, Dictionary<string, ClausewitzNode> strategies, string tag)
	{
		if (state.Country(new CCountryTag(tag)) is not { } country)
		{
			Console.WriteLine($"no country {tag}");
			return;
		}
		strategies.TryGetValue(tag, out ClausewitzNode? ai);
		Console.WriteLine($"{tag}: personality {Personality(country.Strategy.Personality)}, save {ai?.GetString("personality")}");
		foreach (string list in Lists)
		{
			Console.WriteLine($"{list}:");
			Console.WriteLine($"   ours {Text(Ours(country.Strategy, list))}");
			Console.WriteLine($"   save {Text(ai is null ? new List<(string, int)>() : Theirs(ai, list))}");
		}
	}

	/// <summary>What one country's lists weigh of another: the figures the terms are made of.</summary>
	private static void Explain(CGameState state, CCountryTag one, CCountryTag other)
	{
		if (state.Country(one) is not { } country || state.Country(other) is not { } target)
		{
			Console.WriteLine("no such country");
			return;
		}
		COwnedCounts owned = COwnedCounts.Of(state);
		CCountryFacts facts = CCountryFacts.Of(state);
		Console.WriteLine($"{one}: {owned.Count(one)} provinces, {CStrategyFigures.Regiments(country)} brigades, " +
			$"{country.Strategy.Personality}, faction '{country.Faction}', ruling {country.RulingIdeology}");
		Console.WriteLine($"{other}: {owned.Count(other)} provinces, {CStrategyFigures.Regiments(target)} brigades, " +
			$"{target.Strategy.Personality}, faction '{target.Faction}', ruling {target.RulingIdeology}, overlord {target.Overlord}");
		Console.WriteLine($"{one} borders {other}: {facts.AreNeighbours(one, other)}; {other} borders {one}: {facts.AreNeighbours(other, one)}");
		Console.WriteLine($"border share {CStrategyFigures.BorderShares(state, country).GetValueOrDefault(other)}; " +
			$"capital area touches: {CStrategyFigures.CapitalAreaTouches(state, country, other)}; " +
			$"capital borders: {CStrategyFigures.CapitalBorders(state, country, other)}");
		Console.WriteLine($"casus belli {one} on {other}: {OpenHOI3.Sim.Wars.CCasusBellis.HasUsable(state, country, target)}; " +
			$"{other} on {one}: {OpenHOI3.Sim.Wars.CCasusBellis.HasUsable(state, target, country)}");
		Console.WriteLine($"distance {CStrategyFigures.Distance(state, country, target)}, reach {CStrategyFigures.Reach(owned.Count(one))}");
		long regiments = Math.Max(CStrategyFigures.Regiments(country), 1) * 1000L;
		long regimentRatio = CStrategyFigures.Ratio(CStrategyFigures.Regiments(target) * 1000L, regiments);
		long provinceRatio = CStrategyFigures.Ratio(owned.Count(other) * 1000L, owned.Count(one) * 1000L);
		Console.WriteLine($"protect's power ({regimentRatio} + {provinceRatio}) / 2 = {(regimentRatio + provinceRatio) * 1000 / 2000}");
	}

	private static List<(string Key, int Weight)> Ours(CAiStrategy strategy, string list)
	{
		return list switch
		{
			"conquer_prov" => strategy.ConquerProvinces.Entries.Select(e => (e.Key.ToString(), e.Weight)).ToList(),
			"defend_prov" => strategy.DefendProvinces.Entries.Select(e => (e.Key.ToString(), e.Weight)).ToList(),
			"threat" => Countries(strategy.Threat),
			"antagonize" => Countries(strategy.Antagonize),
			"befriend" => Countries(strategy.Befriend),
			"protect" => Countries(strategy.Protect),
			"rival" => Countries(strategy.Rival),
			_ => Countries(strategy.Vassal),
		};
	}

	private static List<(string Key, int Weight)> Countries(CStrategyList<CCountryTag> list)
	{
		return list.Entries.Select(e => (e.Key.Value, e.Weight)).ToList();
	}

	private static List<(string Key, int Weight)> Theirs(ClausewitzNode ai, string list)
	{
		var entries = new List<(string, int)>();
		foreach (ClausewitzNode entry in ai.All(list))
		{
			entries.Add((entry.GetString("id"), entry.GetInt("value")));
		}
		return entries;
	}

	private static string Personality(EAiPersonality personality) => personality switch
	{
		EAiPersonality.Military => "military",
		EAiPersonality.Capital => "capital",
		EAiPersonality.Diplomacy => "diplomacy",
		_ => "balanced",
	};

	private static string Text(List<(string Key, int Weight)> entries)
	{
		return entries.Count == 0 ? "-" : string.Join(" ", entries.Select(e => $"{e.Key} {e.Weight}"));
	}
}
