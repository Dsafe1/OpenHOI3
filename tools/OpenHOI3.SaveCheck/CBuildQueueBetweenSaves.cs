using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

/// <summary>
/// The build queue between two saves of one game: <c>docs/mechanics/buildqueue.md</c>.
///
/// A country's queue is made as the earlier save has it and worked through for as many
/// days as lie between the two, paid in full, with the laws and the practicals the
/// earlier save shows. What is left is set beside the later save's queue, and what was
/// finished beside the unit the later save has made of it.
///
/// Only a country whose every line was being paid in full in both saves is run: how
/// much a part-paid line was given on the days in between is not something a save says.
/// </summary>
internal static class CBuildQueueBetweenSaves
{
	private const string Deployment = "unit_deployment";

	/// <returns>How many countries had a queue and were left out for a line not paid in full.</returns>
	public static int Compare(CGameState state, IEnumerable<(CCountry Country, ClausewitzNode Earlier, ClausewitzNode Later)> pairs,
		string player, int days, Action<string, bool, Func<string>> compare)
	{
		int leftOut = 0;
		foreach ((CCountry country, ClausewitzNode before, ClausewitzNode after) in pairs)
		{
			Dictionary<int, ClausewitzNode> earlier = CBuildQueueCheck.Collect(before);
			if (earlier.Count == 0)
			{
				continue;
			}
			Dictionary<int, ClausewitzNode> later = CBuildQueueCheck.Collect(after);
			if (earlier.Values.Concat(later.Values).Any(line => Number(line.GetString("status")) != CFixedPoint.One))
			{
				leftOut++;
				continue;
			}

			var abilities = new Dictionary<string, CFixedPoint>(country.Abilities, StringComparer.OrdinalIgnoreCase);
			var constructions = new List<CConstruction>(country.Constructions);
			var deployments = new List<CUnit>(country.Deployments);
			country.Constructions.Clear();
			country.Deployments.Clear();
			foreach (ClausewitzNode line in before.Children.Where(child => earlier.ContainsValue(child)))
			{
				country.Constructions.Add(Read(state, country, line));
			}

			for (int day = 0; day < days; day++)
			{
				CBuildQueue.RunDaily(state, country, CFixed15.FromInt(country.Constructions.Count * 1000));
			}

			string tag = country.Tag.Value;
			string whose = tag == player ? "the player's country" : "the others, whose laws an AI changes";
			foreach (CConstruction construction in country.Constructions)
			{
				string who = $"{tag} {construction.Id} {construction}";
				if (!later.TryGetValue(construction.Id, out ClausewitzNode? line))
				{
					compare($"the build queue over {days} days: a line still being built is in the later save, {whose}", false,
						() => $"{who}: ours has {construction.Progress} of {construction.Duration} days; the later save has no such line");
					continue;
				}
				compare($"the build queue over {days} days: a line still being built is in the later save, {whose}", true, () => "");
				Line("how far each line has got", "progress", construction.Progress);
				Line("how much of the building has been counted", "accumulated_progress", construction.AccumulatedProgress);
				Line("the experience gathered", "accumulated_experiance", construction.AccumulatedExperience);
				Line("what a line costs a day", "cost", construction.Cost);
				Line("the days a line takes", "duration", construction.Duration);

				void Line(string what, string key, CFixedPoint ours)
				{
					CFixedPoint saved = Number(line.GetString(key));
					compare($"the build queue over {days} days: {what}, {whose}", saved == ours,
						() => $"{who}: save {saved}, ours {ours}");
				}
			}

			foreach (CUnit unit in country.Deployments)
			{
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					string who = $"{tag} {subUnit.Id} '{subUnit.Name}' {subUnit.Type.Key}";
					ClausewitzNode? waiting = after.Children
						.Where(child => child.IsBlock && Deployment.Equals(child.Key, StringComparison.OrdinalIgnoreCase))
						.Select(child => Find(child, subUnit.Id)).FirstOrDefault(found => found is not null);
					ClausewitzNode? saved = waiting ?? Find(after, subUnit.Id);
					compare($"the build queue over {days} days: a unit finished is in the later save, {whose}", saved is not null,
						() => $"{who}: the later save has no brigade of that number");
					if (saved is null)
					{
						continue;
					}

					CFixedPoint experience = Number(saved.GetString("experience"));
					compare($"the build queue over {days} days: the experience of a unit finished, {whose}",
						experience == subUnit.Experience, () => $"{who}: save {experience}, ours {subUnit.Experience}");

					// Once it is on the map its strength and organisation are no longer what it was made with.
					if (waiting is not null)
					{
						CFixedPoint strength = Number(saved.GetString("strength"));
						compare($"the build queue over {days} days: the strength of a unit finished and not yet placed, {whose}",
							strength == subUnit.Strength, () => $"{who}: save {strength}, ours {subUnit.Strength}");
						CFixedPoint organisation = Number(saved.GetString("organisation"));
						compare($"the build queue over {days} days: the organisation of a unit finished and not yet placed, {whose}",
							organisation == subUnit.Organisation, () => $"{who}: save {organisation}, ours {subUnit.Organisation}");
					}
				}
			}

			country.Constructions.Clear();
			country.Constructions.AddRange(constructions);
			country.Deployments.Clear();
			country.Deployments.AddRange(deployments);
			country.Abilities.Clear();
			foreach ((string category, CFixedPoint ability) in abilities)
			{
				country.Abilities[category] = ability;
			}
		}
		return leftOut;
	}

	/// <summary>A line of the queue as a save writes it, numbers and all.</summary>
	private static CConstruction Read(CGameState state, CCountry country, ClausewitzNode line)
	{
		var construction = new CConstruction(country.Tag)
		{
			Id = line.Get("id")?.GetInt("id") ?? 0,
			UnitId = line.Get("unit")?.GetInt("id") ?? 0,
			Name = line.GetString("name"),
			Size = Math.Max(1, line.GetInt("size")),
			Cost = Number(line.GetString("cost")),
			Duration = Number(line.GetString("duration")),
			Progress = Number(line.GetString("progress")),
			Status = Number(line.GetString("status")),
			IsReserve = line.GetString("is_reserve") == "yes",
			Manpower = Number(line.GetString("manpower")),
			AccumulatedExperience = Number(line.GetString("accumulated_experiance")),
			AccumulatedProgress = Number(line.GetString("accumulated_progress")),
		};
		if (line.Get("factor") is { Value: { } factor })
		{
			construction.ReservesFactor = Number(factor);
		}
		foreach (ClausewitzNode child in line.Children)
		{
			if (child.Key is null || !child.IsBlock || !state.SubUnitTypes.TryGetValue(child.Key, out CSubUnitType? type))
			{
				continue;
			}
			var order = new CBrigadeOrder(type)
			{
				Id = child.Get("id")?.GetInt("id") ?? 0,
				Name = child.GetString("name"),
				Experience = Number(child.GetString("experience")),
			};
			foreach (ClausewitzNode level in child.Get("model")?.Children ?? new List<ClausewitzNode>())
			{
				order.Model.Add((int)Number(level.Value).ToInt());
			}
			construction.Brigades.Add(order);
		}
		return construction;
	}

	/// <summary>The brigade, ship or wing of that number somewhere under a node.</summary>
	private static ClausewitzNode? Find(ClausewitzNode node, int id)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (!child.IsBlock)
			{
				continue;
			}
			if (child.Get("id") is { IsBlock: true } number && number.GetInt("id") == id && child.Get("strength") is not null)
			{
				return child;
			}
			if (Find(child, id) is { } below)
			{
				return below;
			}
		}
		return null;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
