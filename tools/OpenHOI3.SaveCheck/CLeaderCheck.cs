using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

/// <summary>
/// Sets the leaders a new game starts with beside a savegame of the start date:
/// <c>docs/mechanics/leaders.md</c>. Each country's leaders in service - which, in what
/// order, at what rank, with what experience and traits - and each unit's leader, the
/// units matched by number as <see cref="CUnitCheck"/> matches them.
///
/// The save is two days in. In those days the original's AI has made headquarters of
/// its own, which took leaders, and its own ministers may have moved some; neither is
/// built, so a unit whose leader differs is told apart by whether its country is played.
/// </summary>
internal static class CLeaderCheck
{
	private static readonly HashSet<string> UnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"theatre", "armygroup", "army", "corps", "division", "navy", "air",
	};

	/// <returns>How many units only the save has - the AI's own headquarters - hold a leader.</returns>
	public static int Run(IReadOnlyList<(ClausewitzNode Node, CCountry Country)> countries, CGameState state,
		string player, Action<string, bool, Func<string>> compare)
	{
		var savedLeaderOf = new Dictionary<int, (int Leader, string Tag, string Name)>();
		foreach ((ClausewitzNode node, CCountry country) in countries)
		{
			string tag = country.Tag.Value;

			// ---- in service
			var saved = new List<(int Id, ClausewitzNode Node)>();
			foreach (ClausewitzNode entry in node.Get("active_leaders")?.Children ?? new List<ClausewitzNode>())
			{
				if (entry.IsBlock && int.TryParse(entry.Key, out int id))
				{
					saved.Add((id, entry));
				}
			}
			List<int> ours = country.ActiveLeaders.Select(leader => leader.Id).ToList();
			compare("leaders in service, in order", saved.Select(entry => entry.Id).SequenceEqual(ours),
				() => $"{tag}: save [{string.Join(' ', saved.Take(8).Select(entry => entry.Id))} ...], " +
					$"ours [{string.Join(' ', ours.Take(8))} ...]");
			foreach ((int id, ClausewitzNode entry) in saved)
			{
				if (!state.Leaders.TryGetValue(id, out CLeader? leader))
				{
					compare("leader in service known to us", false, () => $"{tag}: {id}");
					continue;
				}
				compare("leader rank", entry.GetInt("rank") == leader.Rank,
					() => $"{tag} {leader}: save {entry.GetInt("rank")}, ours {leader.Rank}");
				CFixed15 experience = Experience(entry.GetString("current_experience"));
				compare("leader experience", experience == leader.Experience,
					() => $"{tag} {leader}: save {entry.GetString("current_experience")}, ours {leader.Experience.Raw / (double)CFixed15.Scale}");
				List<string> traits = entry.Get("trait")?.Children.Select(trait => trait.Value ?? trait.Key ?? "").ToList()
					?? new List<string>();
				compare("leader traits", traits.SequenceEqual(leader.Traits.Select(trait => trait.Key)),
					() => $"{tag} {leader}: save [{string.Join(' ', traits)}], ours [{string.Join(' ', leader.Traits)}]");
			}

			Collect(node, tag, savedLeaderOf);
		}

		// ---- on units
		var byId = new Dictionary<int, CUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				byId[unit.Id] = unit;
			}
		}
		int theirsOnly = 0;
		foreach ((int id, (int leader, string tag, string name)) in savedLeaderOf)
		{
			if (!byId.TryGetValue(id, out CUnit? unit))
			{
				if (leader != 0)
				{
					theirsOnly++;
				}
				continue;
			}
			int our = unit.Leader?.Id ?? 0;
			string what = tag == player ? "unit leader, the country played" : "unit leader, a country the AI runs";
			compare(what, our == leader, () => $"{tag} {unit.Kind} {name} ({id}): save {Describe(state, leader)}, " +
				$"ours {Describe(state, our)}");
		}
		return theirsOnly;
	}

	private static string Describe(CGameState state, int id)
	{
		if (id == 0)
		{
			return "none";
		}
		return state.Leaders.TryGetValue(id, out CLeader? leader)
			? $"{leader.Name} ({id}, rank {leader.Rank}, skill {leader.Skill}, {string.Join('/', leader.Traits)})"
			: $"{id}?";
	}

	private static CFixed15 Experience(string text)
	{
		// The save writes the 15 binary places out as a decimal with five places.
		return decimal.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
			out decimal value)
			? new CFixed15((long)Math.Round(value * CFixed15.Scale))
			: CFixed15.Zero;
	}

	private static void Collect(ClausewitzNode node, string tag, Dictionary<int, (int, string, string)> into)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock || !UnitKeywords.Contains(child.Key))
			{
				continue;
			}
			int id = child.Get("id")?.GetInt("id") ?? 0;
			int leader = child.Get("leader")?.GetInt("id") ?? 0;
			into[id] = (leader, tag, child.GetString("name"));
			Collect(child, tag, into);
		}
	}
}
