using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;

/// <summary>
/// Makes a new game as a player gets it - the install's Lua asked for its relations, its two days
/// run - and sets what its countries hold of each other beside a savegame the original wrote on the
/// start date:
///
///   OpenHOI3.SaveCheck diplomacy &lt;install folder&gt; &lt;start-date savegame&gt; [examples]
///
/// Every pair block of the save - <c>TAG = { value threat }</c> inside a country's - against the
/// pair here: the relation as the save writes it, whole and cut toward nought, and the threat to the
/// thousandth; and each country's neutrality and effective neutrality. See
/// <c>docs/mechanics/diplomacy.md</c>, section 2.
/// </summary>
internal static class CDiplomacyCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 3)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck diplomacy <install folder> <start-date savegame> [examples]");
			return 2;
		}
		int examples = args.Length > 3 && int.TryParse(args[3], out int wanted) ? wanted : 10;
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		using var scripts = new CAiScripts(directories, warnings.Add);
		if (args.Length > 3 && args[3].StartsWith("explain=", StringComparison.Ordinal))
		{
			return Explain(directories, map, localisation, countries, scripts, new CCountryTag(args[3]["explain=".Length..]));
		}
		Console.WriteLine($"the install's Lua loaded in {clock.ElapsedMilliseconds} ms, {scripts.Failures} errors");
		clock.Restart();
		bool saveLaws = args.Skip(3).Contains("laws=save");
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add,
			fill: !saveLaws, firstDays: !saveLaws, aiScripts: scripts);
		if (saveLaws)
		{
			// The laws the save has, before the two days: what the original's AI moved as the game
			// was entered - Laws(country), not run here - for a check of what that explains.
			ClausewitzNode laws = ClausewitzParser.ParseFile(args[2]);
			Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warnings.Add);
			foreach (ClausewitzNode block in laws.Children)
			{
				if (block.IsBlock && state.Country(Tag(block.Key)) is { } country)
				{
					foreach ((string group, Dictionary<string, CLaw> book) in lawBook)
					{
						if (book.TryGetValue(block.GetString(group), out CLaw? law))
						{
							country.Laws[group] = law;
						}
					}
				}
			}
			CTick.RunFirstDays(state);
		}
		Console.WriteLine($"a new game, its two days run: made in {clock.ElapsedMilliseconds} ms, {scripts.Failures} Lua errors");
		foreach (string warning in warnings)
		{
			if (warning.Contains("LUA Error") || warning.Contains("relations"))
			{
				Console.WriteLine("  " + warning);
			}
		}

		ClausewitzNode save = ClausewitzParser.ParseFile(args[2]);
		Console.WriteLine($"savegame: date {save.GetString("date")}, player {save.GetString("player")}");

		int pairs = 0, relationsAgree = 0, threatsAgree = 0, countriesSeen = 0, neutralityAgrees = 0, effectiveAgrees = 0;
		int alignmentAgrees = 0, mostAgrees = 0;
		var alignmentMisses = new List<string>();
		var mostMisses = new List<string>();
		var relationMisses = new List<string>();
		var threatMisses = new List<string>();
		var neutralityMisses = new List<string>();
		foreach (ClausewitzNode block in save.Children)
		{
			if (!block.IsBlock || state.Country(Tag(block.Key)) is not { } country)
			{
				continue;
			}
			countriesSeen++;
			ClausewitzNode? position = block.Get("alignment")?.Get("position");
			long savedX = Number(position?.GetString("x") ?? "").Raw;
			long savedY = Number(position?.GetString("y") ?? "").Raw;
			if (savedX == country.AlignmentX && savedY == country.AlignmentY)
			{
				alignmentAgrees++;
			}
			else
			{
				alignmentMisses.Add($"{country.Tag} at ({country.AlignmentX / 1000.0:F3}, {country.AlignmentY / 1000.0:F3}) here, " +
					$"({savedX / 1000.0:F3}, {savedY / 1000.0:F3}) saved");
			}
			string savedMost = block.GetString("highest_threat");
			string most = country.MostThreatening?.Value ?? "---";
			if (savedMost == most)
			{
				mostAgrees++;
			}
			else
			{
				mostMisses.Add($"{country.Tag} fears {most} most here, {savedMost} saved");
			}
			CFixedPoint savedNeutrality = Number(block.GetString("neutrality"));
			CFixedPoint savedEffective = Number(block.GetString("effective_neutrality"));
			if (savedNeutrality == country.Neutrality)
			{
				neutralityAgrees++;
			}
			else
			{
				neutralityMisses.Add($"{country.Tag} neutrality {country.Neutrality} here, {savedNeutrality} saved");
			}
			if (savedEffective == country.EffectiveNeutrality)
			{
				effectiveAgrees++;
			}
			else
			{
				neutralityMisses.Add($"{country.Tag} effective neutrality {country.EffectiveNeutrality} here, {savedEffective} saved");
			}

			foreach (ClausewitzNode pair in block.Children)
			{
				if (!pair.IsBlock || pair.GetString("value").Length == 0 || state.Country(Tag(pair.Key)) is not { } other)
				{
					continue;
				}
				pairs++;
				CDiplomacyStatus status = CGameState.Status(country, other);
				long savedRelation = long.TryParse(pair.GetString("value"), out long whole) ? whole : long.MinValue;
				if (status.Relation.ToInt() == savedRelation)
				{
					relationsAgree++;
				}
				else
				{
					relationMisses.Add($"{country.Tag}->{other.Tag} relation {status.Relation} here, {savedRelation} saved");
				}
				if (status.Threat == Number(pair.GetString("threat")))
				{
					threatsAgree++;
				}
				else
				{
					threatMisses.Add($"{country.Tag}->{other.Tag} threat {status.Threat} here, {pair.GetString("threat")} saved");
				}
			}
		}

		Console.WriteLine($"relation: {relationsAgree} of {pairs} pairs agree");
		Show(relationMisses, examples);
		Console.WriteLine($"threat: {threatsAgree} of {pairs} pairs agree");
		Show(threatMisses, examples);
		Console.WriteLine($"neutrality: {neutralityAgrees} of {countriesSeen} countries agree, effective {effectiveAgrees} of {countriesSeen}");
		Show(neutralityMisses, examples);
		Console.WriteLine($"alignment: {alignmentAgrees} of {countriesSeen} countries stand where the save has them");
		Show(alignmentMisses, examples);
		Console.WriteLine($"most-threatening country: {mostAgrees} of {countriesSeen} agree");
		Show(mostMisses, examples);
		return relationsAgree == pairs ? 0 : 1;
	}

	/// <summary>
	/// A country's drift as its first step weighs it: the game made up to the day before its first
	/// day, each group's terms, and the inputs they are made of.
	/// </summary>
	private static int Explain(CDirectorySettings directories, CMap map, CLocalisation localisation, CCountries countries,
		CAiScripts scripts, CCountryTag tag)
	{
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, _ => { },
			firstDays: false, aiScripts: scripts);
		CTick.RunEntryPass(state);
		OpenHOI3.Sim.Modifiers.CCountryModifiers.Rebuild(state);
		if (state.Country(tag) is not { } country)
		{
			Console.WriteLine($"no country {tag}");
			return 2;
		}
		Console.WriteLine($"{tag} at ({country.AlignmentX}, {country.AlignmentY}), ruling {country.RulingIdeology}, " +
			$"base IC {country.BaseIc}, total IC {country.TotalIc}, effective neutrality {country.EffectiveNeutrality}");
		Console.WriteLine($"  drift_speed {country.Modifier[CAlignment.DriftSpeed]}, suseptibility {country.Modifier[CAlignment.Susceptibility]}");
		foreach ((CIdeologyGroup group, long[] terms) in CAlignment.Explain(state, country))
		{
			Console.WriteLine($"  {group.Name,-10} relation {terms[0]} ideology {terms[1]} proximity {terms[2]} revanchism {terms[3]} " +
				$"threat {terms[4]} repulsion {terms[5]} influence {terms[6]}");
			foreach (CCountry member in CCountryLists.FactionMembers(state, group.Faction))
			{
				Console.Write($" {member.Tag}:rel {CRelations.Of(country, member)} base IC {member.BaseIc}");
			}
			Console.WriteLine();
		}
		(long x, long y) = CAlignment.Drift(state, country);
		Console.WriteLine($"  drift ({x}, {y}), clamped {CAlignment.ClampMove(state, country.AlignmentX, country.AlignmentY, x, y)}");
		return 0;
	}

	private static CCountryTag? Tag(string? key)
	{
		return key is { Length: CCountryTag.Length } && CCountryTag.TryParse(key, out CCountryTag tag) ? tag : null;
	}

	private static CFixedPoint Number(string text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	private static void Show(List<string> misses, int examples)
	{
		for (int i = 0; i < misses.Count && i < examples; i++)
		{
			Console.WriteLine("  " + misses[i]);
		}
		if (misses.Count > examples)
		{
			Console.WriteLine($"  ... and {misses.Count - examples} more");
		}
	}
}
