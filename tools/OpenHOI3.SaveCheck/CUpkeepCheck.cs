using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

/// <summary>
/// Runs the two days the original has behind it on the first screen, for the units only,
/// and sets each brigade beside the save's: <c>docs/mechanics/upkeep.md</c>.
///
/// A day here is what a day does to a unit and nothing else: its men are rotated out,
/// and the reinforcement and upgrade shares are run with the IC and the shares the
/// country has when this is called. Nothing is charged for and no stockpile moves, and
/// the units are put back as they were found.
///
/// What a brigade's upgrade costs and how long it takes depend on two of the country's
/// laws, its training law and its industrial policy, and the original's AI goes on
/// changing laws after the last day is run. So each country is run with the law the
/// save shows and with its history's, for each of the two, and judged with whichever
/// pair its upgrades fit best; which countries needed their history's is reported.
/// </summary>
internal static class CUpkeepCheck
{
	private const int DaysBehind = 2;

	/// <summary>The two groups of laws that set what a unit costs to build and how long it takes.</summary>
	private static readonly string[] Groups = { "training_laws", "industrial_policy_laws" };

	private static readonly HashSet<string> UnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"theatre", "armygroup", "army", "corps", "division", "navy", "air",
	};

	private static readonly HashSet<string> SubUnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"regiment", "ship", "wing",
	};

	private sealed record Kept(CFixedPoint Strength, CFixedPoint Highest, CFixedPoint Experience, CFixedPoint Organisation,
		Dictionary<string, int> Levels, Dictionary<string, CFixedPoint> Progress);

	/// <param name="held">
	/// The countries whose brigades the save shows held to the ceiling on their
	/// organisation, which is what a change of law after the two days does.
	/// </param>
	/// <param name="notHeld">The countries whose brigades it shows above that ceiling.</param>
	/// <returns>The countries whose upgrades fit a law out of their history better than the save's, and which law.</returns>
	public static List<string> Run(IReadOnlyList<(ClausewitzNode Node, CCountry Country)> countries, CGameState state,
		ClausewitzNode save, IReadOnlyDictionary<string, Dictionary<string, CLaw>> historyLaws,
		IReadOnlyDictionary<string, Dictionary<string, CLaw>> lawBook,
		Action<string, bool, Func<string>> compare, out List<string> held, out List<string> notHeld)
	{
		held = new List<string>();
		notHeld = new List<string>();
		string player = save.GetString("player");
		var savedUnits = new Dictionary<int, ClausewitzNode>();
		var savedSubUnits = new Dictionary<int, ClausewitzNode>();
		foreach ((ClausewitzNode node, CCountry _) in countries)
		{
			Collect(node, savedUnits, savedSubUnits);
		}

		var fromHistory = new List<string>();
		foreach ((ClausewitzNode node, CCountry country) in countries)
		{
			string tag = country.Tag.Value;
			CFixedPoint manpower = country.Manpower;
			Dictionary<int, Kept> start = Keep(country);
			var queue = new CBuildQueueCheck.CKept(country);
			Dictionary<int, ClausewitzNode> savedQueue = CBuildQueueCheck.Collect(node);
			var abilities = new Dictionary<string, CFixedPoint>(country.Abilities, StringComparer.OrdinalIgnoreCase);
			if (start.Count == 0 && queue.Lines.Count == 0)
			{
				continue;
			}

			var found = new Dictionary<string, CLaw?>();
			var history = new Dictionary<string, CLaw?>();
			foreach (string group in Groups)
			{
				found[group] = country.Laws.GetValueOrDefault(group);
				history[group] = historyLaws.TryGetValue(tag, out Dictionary<string, CLaw>? own) ? own.GetValueOrDefault(group) : null;
			}

			// As found first, so that it wins where nothing tells the two apart.
			int best = 0;
			int fewest = int.MaxValue;
			for (int choice = 0; choice < 1 << Groups.Length; choice++)
			{
				PutBack(country, start);
				Give(state, country, choice, found, history);
				RunDays(state, country, queue, abilities);
				int faults = Faults(country, savedSubUnits) + CBuildQueueCheck.Faults(country, savedQueue);
				if (faults < fewest)
				{
					fewest = faults;
					best = choice;
				}
			}
			PutBack(country, start);
			Give(state, country, best, found, history);
			RunDays(state, country, queue, abilities);
			CBuildQueueCheck.CompareProgress(country, savedQueue, tag == player, queue, compare);
			List<string> put = Groups.Where((group, index) => (best & (1 << index)) != 0 && history[group] != found[group]).ToList();
			if (put.Count > 0)
			{
				fromHistory.Add($"{tag} ({string.Join(", ", put)})");
			}

			Compare(state, node, country, save, savedUnits, savedSubUnits, tag == player, compare);
			if (COrganisationCheck.Compare(state, node, country, savedUnits, savedSubUnits, compare) is { } wasHeld)
			{
				(wasHeld ? held : notHeld).Add(tag);
			}

			// As it was found: what a line costs and takes is what the laws the save
			// shows make of it, a change of law being what works it out again.
			PutBack(country, start);
			Give(state, country, 0, found, history);
			country.Manpower = manpower;
			country.Abilities.Clear();
			foreach ((string category, CFixedPoint ability) in abilities)
			{
				country.Abilities[category] = ability;
			}
			if (queue.Lines.Count != 0)
			{
				var asFound = new Dictionary<string, CLaw>(country.Laws, StringComparer.OrdinalIgnoreCase);
				foreach ((string group, Dictionary<string, CLaw> laws) in lawBook)
				{
					if (laws.TryGetValue(node.GetString(group), out CLaw? law))
					{
						country.Laws[group] = law;
					}
				}
				CCountryModifiers.Rebuild(state);
				CBuildQueueCheck.SetUpAsAtStart(state, country, queue);
				CBuildQueueCheck.ComparePrices(state, country, savedQueue, compare);

				// And then as the last day is judged to have found it, for the day run after this.
				country.Laws.Clear();
				foreach ((string group, CLaw law) in asFound)
				{
					country.Laws[group] = law;
				}
				CCountryModifiers.Rebuild(state);
				CBuildQueueCheck.SetUpAsAtStart(state, country, queue);
			}
		}
		return fromHistory;
	}

	private static void RunDays(CGameState state, CCountry country, CBuildQueueCheck.CKept queue,
		Dictionary<string, CFixedPoint> abilities)
	{
		// Its queue as a new game makes it, under the laws it has been given, and what it
		// knew before anything was finished.
		country.Abilities.Clear();
		foreach ((string category, CFixedPoint ability) in abilities)
		{
			country.Abilities[category] = ability;
		}
		CBuildQueueCheck.SetUpAsAtStart(state, country, queue);

		// The officers it starts with over what its own units call for, as the original
		// has it before its AI has added a headquarters.
		CFixedPoint ratio = country.OfficerRatio;
		country.OfficerRatio = COfficers.Ratio(state, country.Officers, COfficers.Needed(country));

		for (int day = 0; day < DaysBehind; day++)
		{
			// An hour comes before its day.
			COrganisationCheck.RunHourBeforeTheGame(state, country);
			CBuildQueueCheck.RunDay(state, country);

			foreach (CUnit unit in country.UnitsInOrder())
			{
				CUnitUpkeep.Rotate(state, country, unit);
			}
			CReinforcement.Run(state, country, CProduction.Given(country, EProductionCategory.Reinforcement), apply: true);

			Dictionary<int, CFixedPoint> organisation = COrganisationCheck.Keep(country);
			country.UpgradeSpent = CUpgrades.Run(state, country, CProduction.Given(country, EProductionCategory.Upgrades), apply: true);
			COrganisationCheck.HoldAsBeforeTheGame(country, organisation);
		}
		country.OfficerRatio = ratio;
	}

	/// <summary>Gives the country, for each group, the law it was found with or its history's, as the bits of the choice say.</summary>
	private static void Give(CGameState state, CCountry country, int choice,
		Dictionary<string, CLaw?> found, Dictionary<string, CLaw?> history)
	{
		for (int index = 0; index < Groups.Length; index++)
		{
			string group = Groups[index];
			CLaw? law = (choice & (1 << index)) != 0 ? history[group] : found[group];
			if (law is null)
			{
				country.Laws.Remove(group);
			}
			else
			{
				country.Laws[group] = law;
			}
		}
		CCountryModifiers.Rebuild(state);
	}

	private static Dictionary<int, Kept> Keep(CCountry country)
	{
		var kept = new Dictionary<int, Kept>();
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				kept[subUnit.Id] = new Kept(subUnit.Strength, subUnit.Highest, subUnit.Experience, subUnit.Organisation,
					new Dictionary<string, int>(subUnit.TechnologyLevels, StringComparer.OrdinalIgnoreCase),
					new Dictionary<string, CFixedPoint>(subUnit.TechnologyProgress, StringComparer.OrdinalIgnoreCase));
			}
		}
		return kept;
	}

	private static void PutBack(CCountry country, Dictionary<int, Kept> kept)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				Kept was = kept[subUnit.Id];
				subUnit.Strength = was.Strength;
				subUnit.Highest = was.Highest;
				subUnit.Experience = was.Experience;
				subUnit.Organisation = was.Organisation;
				subUnit.TechnologyLevels.Clear();
				foreach ((string technology, int level) in was.Levels)
				{
					subUnit.TechnologyLevels[technology] = level;
				}
				subUnit.TechnologyProgress.Clear();
				foreach ((string technology, CFixedPoint progress) in was.Progress)
				{
					subUnit.TechnologyProgress[technology] = progress;
				}
				CUnits.RebuildStats(subUnit);
			}
		}
	}

	/// <summary>How many of a country's upgrades are not where the save has them.</summary>
	private static int Faults(CCountry country, IReadOnlyDictionary<int, ClausewitzNode> savedSubUnits)
	{
		int faults = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (!savedSubUnits.TryGetValue(subUnit.Id, out ClausewitzNode? saved))
				{
					continue;
				}
				foreach (CTechnology technology in subUnit.Type.Technologies)
				{
					if (technology.Change && saved.Get(technology.Name) is { IsBlock: true, Children.Count: >= 2 } pair
						&& Number(pair.Children[1].Value) != subUnit.TechnologyProgress.GetValueOrDefault(technology.Name))
					{
						faults++;
					}
				}
			}
		}
		return faults;
	}

	private static void Compare(CGameState state, ClausewitzNode node, CCountry country, ClausewitzNode save,
		IReadOnlyDictionary<int, ClausewitzNode> savedUnits, IReadOnlyDictionary<int, ClausewitzNode> savedSubUnits,
		bool isPlayer, Action<string, bool, Func<string>> compare)
	{
		string tag = country.Tag.Value;
		// The player's shares are the ones nothing has moved since; an AI's may have been.
		string whose = isPlayer ? "the player's country" : "the others, whose shares an AI moves";
		foreach (CUnit unit in country.AllUnits())
		{
			// A unit the supply network had not reached loses men faster, and there is no network here.
			bool supplied = !savedUnits.TryGetValue(unit.Id, out ClausewitzNode? savedUnit)
				|| Number(savedUnit.GetString("supplies")) >= CFixedPoint.One;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (!savedSubUnits.TryGetValue(subUnit.Id, out ClausewitzNode? saved)
					|| !saved.GetString("type").Equals(subUnit.Type.Key, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string who = $"{tag} {subUnit.Id} '{subUnit.Name}' {subUnit.Type.Key}";

				CFixedPoint strength = Number(saved.GetString("strength"));
				compare(supplied
						? "two days in: the strength of each brigade"
						: "two days in: the strength of each brigade, in a unit the save shows short of supplies (no supply network here)",
					strength == subUnit.Strength,
					() => $"{who}: save {strength}, ours {subUnit.Strength}  (reserve {subUnit.IsReserve}, " +
						$"rotation {country.Modifier[CUnitUpkeep.Rotation]}, the save's unit received {savedUnit?.GetString("supplies")})");

				foreach (CTechnology technology in subUnit.Type.Technologies)
				{
					if (saved.Get(technology.Name) is not { IsBlock: true, Children.Count: >= 2 } pair)
					{
						continue;
					}
					int savedLevel = (int)Number(pair.Children[0].Value).ToInt();
					CFixedPoint savedProgress = Number(pair.Children[1].Value);
					int level = subUnit.TechnologyLevels.GetValueOrDefault(technology.Name);
					CFixedPoint progress = subUnit.TechnologyProgress.GetValueOrDefault(technology.Name);
					string kind = technology.Change ? "equipment" : "doctrine";
					compare($"two days in: the level of each {kind} a brigade holds", savedLevel == level,
						() => $"{who} {technology.Name}: save {savedLevel}, ours {level}  " +
							$"(the country has {country.TechnologyLevels.GetValueOrDefault(technology.Name)})");
					if (technology.Change)
					{
						compare($"two days in: how far each upgrade has got, {whose}", savedProgress == progress,
							() => $"{who} {technology.Name}: save {savedProgress}, ours {progress}  " +
								$"(level {level} of {country.TechnologyLevels.GetValueOrDefault(technology.Name)}, " +
								$"{CBuildCosts.BuildTime(country, subUnit)} days to build, the share ran to {country.UpgradeSpent} of {country.UpgradeCost})");
					}
				}
			}
		}

		// What its units in its capital use is the one figure of their supplies a save keeps:
		// the country's own usage, less what the supply network took out of the capital.
		if (country.Capital == 0 || !CResources.HoldsCapital(state, country))
		{
			return;
		}
		CFixedPoint used = CFixedPoint.Zero;
		var ours = new HashSet<int>();
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Location == country.Capital && unit.CarriedBy is null)
			{
				used += CUnitSupply.SupplyConsumption(state, country, unit);
				ours.Add(unit.Id);
			}
		}

		// What the save has there that changes the figure: a leader, who makes his unit and
		// those under him use less; a unit the files do not give, which is the AI's and not
		// built here; and a unit that asked and was given nothing.
		int leaders = 0;
		int others = 0;
		int unsupplied = 0;
		foreach ((int id, ClausewitzNode unit) in savedUnits)
		{
			if (unit.GetInt("location") != country.Capital || !Owns(node, unit))
			{
				continue;
			}
			leaders += unit.Get("leader") is not null ? 1 : 0;
			others += ours.Contains(id) ? 0 : 1;
			unsupplied += Number(unit.GetString("supplies")) < CFixedPoint.One ? 1 : 0;
		}
		string key = CGoodsPool.KeyOf(EGoods.Supplies);
		ClausewitzNode? capital = save.Get(country.Capital.ToString(CultureInfo.InvariantCulture));
		CFixedPoint savedUse = Number(node.Get("usage")?.GetString(key)) - Number(capital?.Get("throughput")?.GetString(key));
		if (used == CFixedPoint.Zero && savedUse == CFixedPoint.Zero)
		{
			return;
		}
		string where = unsupplied > 0 ? "where the save shows one short of supplies (no supply network here)"
			: others > 0 ? "where the save has a unit the files do not give"
			: leaders > 0 ? "where the save gives one a leader"
			: "where the save has the same units and none has a leader";
		compare($"two days in: the supplies the units in a capital use in a day, {where}", savedUse == used,
			() => $"{tag}: save {savedUse}, ours {used}  (off by {savedUse - used}; " +
				$"consumption modifier {country.Modifier[CUnitSupply.Consumption]})");
	}

	/// <summary>Whether a saved unit is somewhere inside a country's own block.</summary>
	private static bool Owns(ClausewitzNode country, ClausewitzNode unit)
	{
		foreach (ClausewitzNode child in country.Children)
		{
			if (ReferenceEquals(child, unit) || (child.IsBlock && child.Key is not null
				&& (UnitKeywords.Contains(child.Key) || SubUnitKeywords.Contains(child.Key)) && Owns(child, unit)))
			{
				return true;
			}
		}
		return false;
	}

	private static void Collect(ClausewitzNode node, Dictionary<int, ClausewitzNode> units, Dictionary<int, ClausewitzNode> subUnits)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock)
			{
				continue;
			}
			bool isUnit = UnitKeywords.Contains(child.Key);
			if (!isUnit && !SubUnitKeywords.Contains(child.Key))
			{
				continue;
			}
			if (child.Get("id") is { IsBlock: true } id)
			{
				(isUnit ? units : subUnits).TryAdd(id.GetInt("id"), child);
			}
			Collect(child, units, subUnits);                  // a carrier's air group is written inside the ship
		}
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
