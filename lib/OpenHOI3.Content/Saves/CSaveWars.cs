using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A save's <c>active_war</c> and <c>previous_war</c> blocks: each war's name, whether it is limited, its sides in their
/// order, the hour of its last action and its goals, each filed under its actor as the history's are. A war made in play
/// keeps the name it was written with. See <c>docs/mechanics/savegame.md</c>, section 6.
/// </summary>
public static class CSaveWars
{
	private const string None = "---";

	public static CWar Read(CGameState state, ClausewitzNode block, CSaveReport report, Func<string, string>? localise = null)
	{
		string name = block.Get("name")?.Value?.Trim('"') ?? "";
		var war = new CWar(name)
		{
			// A name a declaration made is written out in a save; its parts are read back, so that a war declared after
			// the load is not given a name a loaded one has (war.md, section 7).
			MadeName = localise is null
				? null
				: MadeName(name, localise, state.Countries.Select(country => country.Adjective).ToHashSet(StringComparer.Ordinal)),
		};
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
				case "name":
					break;
				case "limited":
					war.IsLimited = entry.Value == "yes";
					break;
				case "attacker":
					Side(war.Attackers, entry);
					break;
				case "defender":
					Side(war.Defenders, entry);
					break;
				case "action":
					war.Action = CSaveOrders.Hour(entry.Value) ?? 0;
					break;
				// Who began it, where a war has been taken over: nothing here holds it.
				case "original_attacker":
				case "original_defender":
					if (entry.Value?.Trim('"') != None)
					{
						report.Unread(block.Key!, entry.Key);
					}
					break;
				case "war_goal" when entry.IsBlock:
					ReadGoal(state, war, entry, report);
					break;
				default:
					report.Unread(block.Key!, entry.Key);
					break;
			}
		}
		return war;
	}

	/// <summary>
	/// A war under its key - <c>active_war</c> or <c>previous_war</c> - as the original writes it: its name, a war made in
	/// play's written out as the war list shows it; whether it is limited; its sides in order; the hour of its last
	/// action; who began it, which nothing here holds and every save has as none; and its goals, each holder's in the
	/// order they were added (<c>savegame.md</c>, section 10).
	/// </summary>
	public static void Write(CSaveTextWriter writer, string key, CWar war, Func<string, string> localise)
	{
		writer.Begin(key);
		writer.Quoted("name", war.MadeName?.Text(localise) ?? war.Name);
		writer.Bool("limited", war.IsLimited);
		foreach (CCountryTag attacker in war.Attackers)
		{
			writer.Quoted("attacker", attacker.Value);
		}
		foreach (CCountryTag defender in war.Defenders)
		{
			writer.Quoted("defender", defender.Value);
		}
		writer.DateOrNull("action", war.Action > 0 ? war.Action : null);
		writer.Quoted("original_attacker", None);
		writer.Quoted("original_defender", None);
		foreach (CWarGoal goal in war.Goals.Values.SelectMany(goals => goals))
		{
			writer.Begin("war_goal");
			writer.Quoted("casus_belli", goal.Type.Name);
			writer.Quoted("actor", goal.Actor.Value);
			writer.Quoted("receiver", goal.Recipient.Value);
			// Where a goal names them; in no save, so where the original writes them is inferred.
			if (goal.Country is { } third)
			{
				writer.Quoted("country", third.Value);
			}
			if (goal.Region is { } region)
			{
				writer.Quoted("region", region.Name);
			}
			writer.End();
		}
		writer.End();
	}

	/// <summary>
	/// The parts of a name a declaration made, read back out of its text: the key whose text it fits, with the order and
	/// the adjectives in their places - none where it fits neither, as a history war's name does not. A fit is taken only
	/// where each adjective is a country's and the parts write the same text out again: "Second Italo-Abyssinian War"
	/// fits the revolt war's text, and "Second Italo" is no country's.
	/// </summary>
	public static CWarName? MadeName(string name, Func<string, string> localise, ISet<string> adjectives)
	{
		string suffixes = string.Join("|", new[] { "ST", "ND", "RD", "TH" }.Select(key => Regex.Escape(localise(key))));
		foreach (string key in new[] { CWarName.Aggression, CWarName.Normal })
		{
			string template = localise(key);
			if (!template.Contains("$FIRST$", StringComparison.Ordinal))
			{
				continue;
			}
			string pattern = "^" + Regex.Escape(template)
				.Replace(Regex.Escape("$ORDER$"), $"(?:(?<order>[0-9]+)(?:{suffixes}) )?", StringComparison.Ordinal)
				.Replace(Regex.Escape("$FIRST$"), "(?<first>.+?)", StringComparison.Ordinal)
				.Replace(Regex.Escape("$SECOND$"), "(?<second>.+?)", StringComparison.Ordinal) + "$";
			Match match = Regex.Match(name, pattern);
			if (!match.Success)
			{
				continue;
			}
			int order = match.Groups["order"].Success ? int.Parse(match.Groups["order"].Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
			var made = new CWarName(key, match.Groups["first"].Value, match.Groups["second"].Value, order);
			bool countries = adjectives.Contains(made.First) && (!match.Groups["second"].Success || adjectives.Contains(made.Second));
			if (countries && made.Text(localise) == name)
			{
				return made;
			}
		}
		return null;
	}

	private static void Side(List<CCountryTag> side, ClausewitzNode entry)
	{
		if (CCountryTag.TryParse(entry.Value?.Trim('"'), out CCountryTag tag) && !side.Contains(tag))
		{
			side.Add(tag);
		}
	}

	private static void ReadGoal(CGameState state, CWar war, ClausewitzNode block, CSaveReport report)
	{
		if (!CCountryTag.TryParse(block.Get("actor")?.Value?.Trim('"'), out CCountryTag actor)
			|| !CCountryTag.TryParse(block.Get("receiver")?.Value?.Trim('"'), out CCountryTag receiver))
		{
			report.Unread("war_goal", "a goal of no actor or receiver");
			return;
		}
		var goal = new CWarGoal(CCasusBellis.Find(state, block.Get("casus_belli")?.Value?.Trim('"') ?? ""), actor, receiver)
		{
			Country = CCountryTag.TryParse(block.Get("country")?.Value?.Trim('"'), out CCountryTag third) ? third : null,
			Region = state.Regions.FirstOrDefault(region => region.Name == block.Get("region")?.Value?.Trim('"')),
		};
		if (!war.Goals.TryGetValue(actor, out List<CWarGoal>? goals))
		{
			war.Goals[actor] = goals = new List<CWarGoal>();
		}
		goals.Add(goal);
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is { } key && key is not ("casus_belli" or "actor" or "receiver" or "country" or "region"))
			{
				report.Unread("war_goal", key);
			}
		}
	}
}
