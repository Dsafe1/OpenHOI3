using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A game loaded from the original's <c>.hoi3</c> save: the world the install describes, then every figure the save
/// holds of it read over that, each one the save leaves out set to what it means - and what is not read yet cleared and
/// reported, never left as the history had it. Then what the original works out again after a load, and none of a new
/// game's two days. See <c>docs/mechanics/savegame.md</c>.
/// </summary>
public static class CSaveLoader
{
	/// <summary>The keys of a country block this reads, besides the law groups, the posts, the categories and the tags.</summary>
	private static readonly HashSet<string> CountryKeys = new(StringComparer.Ordinal)
	{
		"capital", "acting_capital", "technology", "distribution", "leadership", "manpower", "officers", "diplo_influence",
		"national_unity", "dissent", "neutrality", "effective_neutrality", "ideology", "flags", "variables",
		"auto_assign_leaders",
	};

	private static readonly HashSet<string> ProvinceKeys = new(StringComparer.Ordinal)
	{
		"owner", "controller", "core", "manpower", "leadership", "revolt_risk", "points", "nationalism", "pool",
		"current_producing", "max_producing", "weather", "flags", "strategic_resource",
	};

	/// <param name="played">The country played; null, the one the save says was.</param>
	public static (CGameState State, CSaveReport Report) Load(CDirectorySettings directories, CMap map,
		CLocalisation localisation, CCountries countries, string path, CCountryTag? played = null,
		IAiScripts? aiScripts = null, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		ClausewitzNode root = ClausewitzParser.ParseFile(path);
		CGameState state = CGameStateLoader.BuildWorld(directories, map, localisation, countries, warn, aiScripts);
		var report = new CSaveReport();
		ClearUnread(state);

		foreach (ClausewitzNode node in root.Children)
		{
			if (node.Key is null)
			{
				continue;
			}
			if (int.TryParse(node.Key, out int id))
			{
				if (id > 0 && id < state.Provinces.Count && node.IsBlock)
				{
					ReadProvince(state, state.Province(id), node, report);
				}
				else
				{
					report.Unread("top", "a province that is none of the map's");
				}
				continue;
			}
			if (node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag) && state.Country(tag) is { } country)
			{
				ReadCountry(state, country, node, report);
				continue;
			}
			switch (node.Key)
			{
				case "date":
					if (CSaveValues.TryHours(node.Value, out long hours))
					{
						state.SetClock(hours);
					}
					break;
				case "player":
					report.Player = node.Value?.Trim('"') ?? "";
					break;
				// The game's generator goes on where the save left it, where the original throws both away
				// (docs/compatibility.md, entry 15). The hour reseeds it, so the seed is all that carries the game on.
				case "seed":
					state.Seed = unchecked((uint)CSaveValues.Whole(node.Value));
					break;
				case "count":
					break;
				case "faction":
					ReadFactions(state, node);
					break;
				default:
					report.Unread("top", node.Key);
					break;
			}
		}

		CCountryTag? player = played ?? (CCountryTag.TryParse(report.Player, out CCountryTag saved) ? saved : null);
		if (player is { } who)
		{
			state.Played.Add(who);
		}
		SetUpLoadedGame(state);
		foreach (string line in report.Lines())
		{
			warn($"save '{System.IO.Path.GetFileName(path)}': not loaded yet - {line}");
		}
		return (state, report);
	}

	/// <summary>
	/// What the world holds from the history that a save would give and this does not read yet: cleared, so that nothing
	/// is answered from the history in a loaded game (<c>CLAUDE.md</c>'s rule on what is not built).
	/// </summary>
	private static void ClearUnread(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.Units.Clear();
			country.Constructions.Clear();
			country.Deployments.Clear();
			country.BuildingDeployments.Clear();
			country.ActiveLeaders.Clear();
			country.Convoys.Clear();
			country.Faction = "";
		}
		state.Wars.Clear();
		state.Alliances.Clear();
		state.Guarantees.Clear();
		state.Naps.Clear();
	}

	private static void ReadProvince(CGameState state, CProvince province, ClausewitzNode node, CSaveReport report)
	{
		province.Owner = Tag(node.Get("owner")?.Value);
		province.Controller = Tag(node.Get("controller")?.Value);
		province.Cores.Clear();
		foreach (ClausewitzNode core in node.All("core"))
		{
			if (Tag(core.Value) is { } tag)
			{
				province.Cores.Add(tag);
			}
		}
		province.Manpower = CSaveValues.Fixed(node, "manpower");
		province.Leadership = CSaveValues.Fixed(node, "leadership");
		province.RevoltRisk = CSaveValues.Fixed(node, "revolt_risk");
		province.Points = (int)CSaveValues.Whole(node, "points");
		province.Nationalism = CSaveValues.Fixed(node, "nationalism");
		province.StrategicResource = node.Get("strategic_resource")?.Value?.Trim('"') ?? "";
		ReadPool(province.Pool, node.Get("pool"));
		ReadPool(province.CurrentProducing, node.Get("current_producing"));
		ReadPool(province.MaxProducing, node.Get("max_producing"));
		ReadWeather(state, province.Weather, node.Get("weather"));
		ReadFlags(province.Flags, node.Get("flags"));
		// Every building: the level in working order first, what was built second; none written is none built.
		var buildings = new HashSet<string>(StringComparer.Ordinal);
		foreach (CBuilding building in state.Buildings)
		{
			buildings.Add(building.Key);
			ClausewitzNode? pair = node.Get(building.Key);
			bool written = pair is { IsBlock: true, Children.Count: >= 2 };
			province.SetBuildingMaxLevel(building, written ? CSaveValues.Fixed(pair!.Children[1].Value) : CFixedPoint.Zero);
			province.SetBuildingCurrentLevel(building, written ? CSaveValues.Fixed(pair!.Children[0].Value) : CFixedPoint.Zero);
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is { } key && !ProvinceKeys.Contains(key) && !buildings.Contains(key))
			{
				report.Unread("province", key);
			}
		}
	}

	private static void ReadCountry(CGameState state, CCountry country, ClausewitzNode node, CSaveReport report)
	{
		country.Capital = (int)CSaveValues.Whole(node, "capital");
		int acting = (int)CSaveValues.Whole(node, "acting_capital");
		country.ActingCapital = acting > 0 ? acting : country.Capital;

		var laws = new HashSet<string>(StringComparer.Ordinal);
		foreach (CLawGroup group in state.LawGroups)
		{
			laws.Add(group.Name);
			if (node.Get(group.Name)?.Value is { } name && group.Laws.FirstOrDefault(law => law.Name == name) is { } law)
			{
				country.Laws[group.Name] = law;
			}
		}
		// The posts: a minister of the country's by the id the save gives, none where it gives none.
		var posts = new HashSet<string>(StringComparer.Ordinal);
		country.Government.Clear();
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			posts.Add(position.Name);
			if (country.Ministers.TryGetValue((int)CSaveValues.Whole(node, position.Name), out CMinister? minister))
			{
				country.Government[position.Name] = minister;
			}
		}
		string ideology = node.Get("ideology")?.Value ?? "";
		country.RulingIdeology = ideology == "noIdeology" ? "" : ideology;

		// Technology: every entry its level and the progress towards the next.
		country.TechnologyLevels.Clear();
		country.ResearchProgress.Clear();
		foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
		{
			if (entry.Key is not null && entry.IsBlock && entry.Children.Count >= 2 && state.Technologies.ContainsKey(entry.Key))
			{
				country.TechnologyLevels[entry.Key] = (int)CSaveValues.Whole(entry.Children[0].Value);
				CFixed15 progress = CSaveValues.Fixed15(entry.Children[1].Value);
				if (progress != CFixed15.Zero)
				{
					country.ResearchProgress[entry.Key] = progress;
				}
			}
		}
		var categories = new HashSet<string>(StringComparer.Ordinal);
		country.Abilities.Clear();
		foreach (Sim.Research.CTechnologyCategory category in state.TechnologyCategories)
		{
			categories.Add(category.Name);
			if (node.Get(category.Name)?.Value is { } text)
			{
				country.Abilities[category.Name] = CSaveValues.Fixed(text);
			}
		}
		ReadShares(country.ProductionShares, node.Get("distribution"));
		ReadShares(country.LeadershipShares, node.Get("leadership"));

		country.Manpower = CSaveValues.Fixed(node, "manpower");
		country.Officers = CSaveValues.Fixed(node, "officers");
		country.DiplomaticInfluence = CSaveValues.Fixed(node, "diplo_influence");
		country.NationalUnity = CSaveValues.Fixed(node, "national_unity");
		country.Dissent = CSaveValues.Fixed(node, "dissent");
		country.Neutrality = CSaveValues.Fixed(node, "neutrality");
		country.EffectiveNeutrality = CSaveValues.Fixed(node, "effective_neutrality");
		country.AutoAssignLeaders = node.GetBool("auto_assign_leaders", true);
		ReadFlags(country.Flags, node.Get("flags"));
		country.Variables.Clear();
		foreach (ClausewitzNode variable in node.Get("variables")?.Children ?? new List<ClausewitzNode>())
		{
			if (variable.Key is { } name)
			{
				country.Variables[name] = CSaveValues.Fixed(variable.Value);
			}
		}

		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not { } key)
			{
				continue;
			}
			// A block under another country's tag is what the two hold of each other.
			if (child.IsBlock && CCountryTag.TryParse(key, out CCountryTag otherTag) && state.Country(otherTag) is { } other)
			{
				ReadStatus(state, CGameState.Status(country, other), child);
				continue;
			}
			if (!CountryKeys.Contains(key) && !laws.Contains(key) && !posts.Contains(key) && !categories.Contains(key))
			{
				report.Unread("country", key);
			}
		}
	}

	/// <summary>A pair block: the relation, a whole number; the threat; the occupation policy, none written none held.</summary>
	private static void ReadStatus(CGameState state, CDiplomacyStatus status, ClausewitzNode node)
	{
		status.Relation = CFixedPoint.FromInt(CSaveValues.Whole(node, "value"));
		status.Threat = CSaveValues.Fixed(node, "threat");
		string policy = node.Get("occupation_policy")?.Value?.Trim('"') ?? COccupationPolicies.NoOccupation;
		status.OccupationPolicy = COccupationPolicies.Find(state, policy)?.Index ?? 0;
	}

	/// <summary>The top-level <c>faction = { axis = { progress = .. country = "GER" ... } }</c>: each country named a member.</summary>
	private static void ReadFactions(CGameState state, ClausewitzNode node)
	{
		foreach (ClausewitzNode faction in node.Children)
		{
			if (faction.Key is null || !faction.IsBlock)
			{
				continue;
			}
			foreach (ClausewitzNode member in faction.All("country"))
			{
				if (Tag(member.Value) is { } tag && state.Country(tag) is { } country)
				{
					country.Faction = faction.Key;
				}
			}
		}
	}

	private static void ReadPool(CGoodsPool pool, ClausewitzNode? node)
	{
		foreach (EGoods goods in Enum.GetValues<EGoods>())
		{
			pool[goods] = CSaveValues.Fixed(node?.Get(CGoodsPool.KeyOf(goods))?.Value);
		}
	}

	private static void ReadShares(CFixed15[] shares, ClausewitzNode? node)
	{
		List<ClausewitzNode> values = node?.Children ?? new List<ClausewitzNode>();
		if (values.Count != shares.Length)
		{
			return;
		}
		for (int i = 0; i < shares.Length; i++)
		{
			shares[i] = CSaveValues.Fixed15(values[i].Value);
		}
	}

	/// <summary>A province's weather, each figure the save leaves out nought - and the pressure its define.</summary>
	private static void ReadWeather(CGameState state, CWeather weather, ClausewitzNode? node)
	{
		weather.Humidity = CSaveValues.Fixed(node, "humidity");
		weather.Pressure = node?.Get("pressure")?.Value is { } pressure ? CSaveValues.Fixed(pressure)
			: state.Defines[CProvinceWeather.Section, CProvinceWeather.PressureDefault];
		weather.WindSpeed = CSaveValues.Fixed(node, "windspeed");
		weather.Temperature = CSaveValues.Fixed(node, "temperature");
		weather.Precipitation = CSaveValues.Fixed(node, "precipitation");
		weather.Muddyness = CSaveValues.Fixed(node, "muddyness");
		weather.Frozen = CSaveValues.Fixed(node, "frozen");
		weather.CloudCoverage = CSaveValues.Fixed(node, "cloud_coverage");
	}

	/// <summary>A flags block: every flag written is set, as the original writes only those that are.</summary>
	private static void ReadFlags(Sim.Script.CFlags flags, ClausewitzNode? node)
	{
		foreach (ClausewitzNode flag in node?.Children ?? new List<ClausewitzNode>())
		{
			if (flag.Key is { } name && flag.Value == "yes")
			{
				flags.Set(name);
			}
		}
	}

	private static CCountryTag? Tag(string? text)
	{
		return CCountryTag.TryParse(text?.Trim('"'), out CCountryTag tag) ? tag : null;
	}

	/// <summary>
	/// What the original works out again after a load, before the first hour (<c>savegame.md</c>, section 2): the
	/// modifiers, resources, IC, manpower and leadership, neutrality, and a fresh AI for every country - and none of a
	/// new game's two days, the filling or the first hour.
	/// </summary>
	private static void SetUpLoadedGame(CGameState state)
	{
		state.ForgetPositions();
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);
		CStrategicResources.Update(state);
		CCountryModifiers.Rebuild(state);
		CCountryModifiers.RebuildDaily(state);
		CManpower.Update(state);
		CLeadership.Update(state);
		foreach (CCountry country in state.Countries)
		{
			CNeutrality.UpdateEffective(state, country);
		}
		// A fresh AI for every country, as the original makes one: the ministers' first hours, and the strategy made again
		// where the original keeps a saved one (savegame.md, Ours).
		foreach (CCountry country in state.Countries)
		{
			country.PoliticsCountdown = state.Played.Contains(country.Tag) ? 0 : state.Random.Next() % 3 + 1;
		}
		Sim.Espionage.CEspionageAi.SetUpAtStart(state);
		CForeignAi.SetUpAtStart(state);
		Sim.Research.CResearchAi.SetUpAtStart(state);
		CAiStrategies.MakeAtStart(state);
		state.IsRunning = true;
	}
}
