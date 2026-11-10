using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's AI strategy, both ways: <c>ai_hard_strategy</c> and <c>ai_event_strategy</c>, which nothing here holds and
/// which are written empty, and <c>ai</c> - whether it was made, the personality, the eight lists of entries an
/// <c>id</c> and a <c>value</c>, and the theatres, each <c>area_theatre</c>. See <c>docs/mechanics/savegame.md</c>,
/// section 16.
/// </summary>
public static class CSaveAiStrategy
{
	/// <summary>The keys of a country block this reads.</summary>
	public static readonly string[] Keys = { "ai_hard_strategy", "ai_event_strategy", "ai" };

	/// <summary>The type a theatre's id has (<c>FINDINGS-savegame.md</c>, B.3).</summary>
	public const int TheatreIdType = 45;

	private static readonly string[] Personalities = { "balanced", "military", "capital", "diplomacy", "balanced" };

	/// <summary>The keys an <c>ai</c> block has that nothing here holds, which a load passes over without a report.</summary>
	private static readonly HashSet<string> NotHeld = new(StringComparer.Ordinal)
	{
		"consolidate", "static", "max_subunits", "land_perc", "air_perc", "naval_perc", "armor_bias", "military_access",
	};

	public static void Read(CGameState state, CCountry country, ClausewitzNode node, CSaveIds ids, CSaveReport report)
	{
		// The two strategies scripts give are not built: one that says anything is reported.
		foreach (string key in new[] { "ai_hard_strategy", "ai_event_strategy" })
		{
			if (node.Get(key) is { } given && (given.GetBool("initialized") || given.Children.Any(child => child.IsBlock)))
			{
				report.Unread("country", key);
			}
		}
		CAiStrategy strategy = country.Strategy;
		strategy.Clear();
		strategy.Theatres.Clear();
		if (node.Get("ai") is not { } ai)
		{
			strategy.IsMade = false;
			return;
		}
		strategy.IsMade = ai.GetBool("initialized");
		int personality = Array.IndexOf(Personalities, ai.GetString("personality"), 1);
		strategy.Personality = personality > 0 ? (EAiPersonality)personality : EAiPersonality.None;
		foreach (ClausewitzNode entry in ai.Children)
		{
			switch (entry.Key)
			{
				case "conquer_prov":
					strategy.ConquerProvinces.Append((int)CSaveValues.Whole(entry, "id"), (int)CSaveValues.Whole(entry, "value"));
					break;
				case "defend_prov":
					strategy.DefendProvinces.Append((int)CSaveValues.Whole(entry, "id"), (int)CSaveValues.Whole(entry, "value"));
					break;
				case "threat":
					AddCountry(state, strategy.Threat, entry, report);
					break;
				case "antagonize":
					AddCountry(state, strategy.Antagonize, entry, report);
					break;
				case "befriend":
					AddCountry(state, strategy.Befriend, entry, report);
					break;
				case "protect":
					AddCountry(state, strategy.Protect, entry, report);
					break;
				case "rival":
					AddCountry(state, strategy.Rival, entry, report);
					break;
				case "vassal":
					AddCountry(state, strategy.Vassal, entry, report);
					break;
				case "area_theatre" when entry.IsBlock:
					strategy.Theatres.Add(ReadTheatre(state, country, entry, ids));
					break;
				case "initialized":
				case "personality":
					break;
				default:
					if (entry.Key is { } key && !NotHeld.Contains(key))
					{
						report.Unread("ai", key);
					}
					break;
			}
		}
	}

	private static void AddCountry(CGameState state, CStrategyList<CCountryTag> list, ClausewitzNode entry, CSaveReport report)
	{
		if (CCountryTag.TryParse(entry.GetString("id"), out CCountryTag tag) && state.Country(tag) is not null)
		{
			list.Append(tag, (int)CSaveValues.Whole(entry, "value"));
		}
		else
		{
			report.Unread("ai", $"a country of no tag known, {entry.GetString("id")}");
		}
	}

	/// <summary>
	/// A theatre under its own number: its fronts, its own provinces and its key provinces, each province stamped with it,
	/// its headquarters, whether it is hot - and its areas and ports worked out again from its provinces, which are not
	/// saved. Its fronts are made again at the first hour, a loaded country's theatres being marked as every country's are.
	/// </summary>
	private static CTheatre ReadTheatre(CGameState state, CCountry country, ClausewitzNode node, CSaveIds ids)
	{
		var theatre = new CTheatre(CSaveValues.Id(node.Get("id")), country.Tag)
		{
			IsHot = node.GetBool("hot"),
			Headquarters = ids.Of(node.Get("unit")),
		};
		state.Theatres[theatre.Id] = theatre;
		// The theatre's own provinces are its last provinces key, its fronts having theirs inside them. The own come first
		// among all its provinces, in the running original's list, whatever the save's order (FINDINGS-liveread.md, section 15).
		foreach (int province in Numbers(node.Children.LastOrDefault(child => child.Key == "provinces")))
		{
			CTheatreProvinces.Add(state, theatre, province);
		}
		foreach (ClausewitzNode block in node.All("front"))
		{
			var front = new CFront
			{
				Enemy = CCountryTag.TryParse(block.GetString("local_enemy"), out CCountryTag enemy) ? enemy : null,
				IsHot = block.GetBool("front"),
			};
			front.Provinces.AddRange(Numbers(block.Get("provinces")));
			CTheatreProvinces.AddFront(state, theatre, front, reorder: false);
		}
		theatre.KeyProvinces.AddRange(Numbers(node.Get("key")));
		CTheatreProvinces.RebuildAreas(state, theatre);
		return theatre;
	}

	private static IEnumerable<int> Numbers(ClausewitzNode? node)
	{
		return (node?.Children ?? new List<ClausewitzNode>()).Select(value => (int)CSaveValues.Whole(value.Value));
	}

	/// <summary>The two strategies nothing holds, empty, and then the country's own.</summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		WriteEmpty(writer, "ai_hard_strategy");
		WriteEmpty(writer, "ai_event_strategy");
		CAiStrategy strategy = country.Strategy;
		writer.Begin("ai");
		writer.Bool("initialized", strategy.IsMade);
		writer.Word("personality", Personalities[(int)strategy.Personality]);
		WriteList(writer, "conquer_prov", strategy.ConquerProvinces, id => id.ToString(System.Globalization.CultureInfo.InvariantCulture));
		WriteList(writer, "defend_prov", strategy.DefendProvinces, id => id.ToString(System.Globalization.CultureInfo.InvariantCulture));
		WriteList(writer, "threat", strategy.Threat, Quoted);
		WriteList(writer, "antagonize", strategy.Antagonize, Quoted);
		WriteList(writer, "befriend", strategy.Befriend, Quoted);
		WriteList(writer, "protect", strategy.Protect, Quoted);
		WriteList(writer, "rival", strategy.Rival, Quoted);
		WriteList(writer, "vassal", strategy.Vassal, Quoted);
		foreach (CTheatre theatre in strategy.Theatres)
		{
			WriteTheatre(writer, state, theatre);
		}
		writer.End();
	}

	private static string Quoted(CCountryTag tag) => "\"" + tag.Value + "\"";

	/// <summary>A strategy nothing gave: never made, balanced, no list.</summary>
	private static void WriteEmpty(CSaveTextWriter writer, string key)
	{
		writer.Begin(key);
		writer.Bool("initialized", false);
		writer.Word("personality", Personalities[0]);
		writer.End();
	}

	private static void WriteList<TKey>(CSaveTextWriter writer, string key, CStrategyList<TKey> list, Func<TKey, string> text)
		where TKey : notnull
	{
		foreach ((TKey listed, int weight) in list.Entries)
		{
			writer.Begin(key);
			writer.Word("id", text(listed));
			writer.Int("value", weight);
			writer.End();
		}
	}

	/// <summary>
	/// <c>area_theatre</c>: its id, each front - its provinces, whom it faces where it faces anyone, whether it is hot, and
	/// <c>sea=no</c>, every front here being a land one - its own provinces and its key provinces where it has any, its
	/// headquarters, its country and whether it is hot.
	/// </summary>
	private static void WriteTheatre(CSaveTextWriter writer, CGameState state, CTheatre theatre)
	{
		writer.Begin("area_theatre");
		writer.IdPair("id", theatre.Id, TheatreIdType);
		foreach (CFront front in theatre.Fronts)
		{
			writer.Begin("front");
			writer.Numbers("provinces", front.Provinces.Select(id => (long)id));
			if (front.Enemy is { } enemy)
			{
				writer.Quoted("local_enemy", enemy.Value);
			}
			writer.Bool("front", front.IsHot);
			writer.Bool("sea", false);
			writer.End();
		}
		if (theatre.Provinces.Count > 0)
		{
			writer.Numbers("provinces", theatre.Provinces.Select(id => (long)id));
		}
		if (theatre.KeyProvinces.Count > 0)
		{
			writer.Numbers("key", theatre.KeyProvinces.Select(id => (long)id));
		}
		writer.IdPair("unit", theatre.Headquarters > 0 ? CSaveUnitWriter.Pair(state, theatre.Headquarters) : (0, 0));
		writer.Quoted("country", theatre.Country.Value);
		writer.Bool("hot", theatre.IsHot);
		writer.End();
	}
}
