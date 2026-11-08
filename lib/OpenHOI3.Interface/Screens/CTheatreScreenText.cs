using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the theatres screen, <c>country_theatres</c>, says: the two lists of theatres, the selected theatre's stances, forces and
/// objectives, and a row for each front facing somebody who is no friend. See <c>docs/mechanics/theatresscreen.md</c>.
/// </summary>
public static class CTheatreScreenText
{
	public const string WindowName = "country_theatres";
	public const string TopColourName = "topcolor_theatres";
	public const string TheatreRowName = "theatre_entry";
	public const string AreaRowName = "area_entry";

	private const long GoodRatio = 750;
	private const long AverageRatio = 500;
	private const int MostNamedObjectives = 3;
	private const long PointWeight = 20000;
	private const long BaseWeight = 1000;
	private const long CapitalWeight = 10000;
	private const string ProvinceIc = "ic";
	private const string Infrastructure = "infra";

	private static readonly string[] LandStances =
		{ "STANCE_WITHDRAW", "STANCE_PREPARE", "STANCE_DEFEND", "STANCE_ATTACK", "STANCE_BLITZ" };

	private static readonly string[] AirStances = { "STANCE_AIR_PREPARE", "STANCE_AIR_DEFEND", "STANCE_AIR_ATTACK" };

	private static readonly string[] NavalStances = { "STANCE_NAVAL_PREPARE", "STANCE_NAVAL_DEFEND", "STANCE_NAVAL_ATTACK" };

	/// <summary>The country's own theatres in their order: none for one whose headquarters is gone.</summary>
	public static List<(CTheatre Theatre, CUnit Headquarters)> Rows(CGameState state, CCountry country)
	{
		var rows = new List<(CTheatre, CUnit)>();
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (CTheatres.Headquarters(state, theatre) is { } headquarters)
			{
				rows.Add((theatre, headquarters));
			}
		}
		return rows;
	}

	/// <summary>Each ally's theatres, ally by ally in the order of the country's allies.</summary>
	public static List<(CTheatre Theatre, CUnit Headquarters)> AlliedRows(CGameState state, CCountry country)
	{
		return CCountryLists.Allies(state, country).SelectMany(ally => Rows(state, ally)).ToList();
	}

	/// <summary>The frame of a theatre's and a front's mark, from one: the plan's power ratio over 0.75, over 0.5, or neither [exe].</summary>
	public static int RatioFrame(CGameState state, CUnit headquarters)
	{
		long ratio = PowerRatio(state, headquarters);
		return ratio > GoodRatio ? 1 : ratio > AverageRatio ? 2 : 3;
	}

	/// <summary>The plan's power beside the enemy's: its own figures where it is on, and otherwise the two the day's look at an inactive plan writes (section 6).</summary>
	public static long PowerRatio(CGameState state, CUnit headquarters)
	{
		if (headquarters.Plan.Active)
		{
			return CAirBody.PowerRatio(headquarters.Plan);
		}
		CPlanWanted wanted = CPlanWants.Compute(state, headquarters, null);
		var plan = new CUnitPlan { OurPower = wanted.OurPower, TheirPower = wanted.TheirPower };
		return CAirBody.PowerRatio(plan);
	}

	/// <summary>
	/// What the player is short of for a headquarters, the lines of its own queue aimed at it taken off; null where there is nothing. Given provinces, of those alone. The original takes each
	/// line's size off every brigade type it holds, once a brigade.
	/// </summary>
	public static int[]? Needs(CGameState state, CCountry player, CUnit headquarters, IReadOnlyList<int>? provinces)
	{
		if (CPlanWants.Needed(state, headquarters, provinces) is not { } needs)
		{
			return null;
		}
		Subtract(player, headquarters, needs);
		return needs.Any(need => need > 0) ? needs : null;
	}

	public static void Subtract(CCountry player, CUnit headquarters, int[] needs)
	{
		foreach (Sim.Construction.CConstruction line in player.Constructions.Where(line => line.Target == headquarters.Id))
		{
			foreach (Sim.Construction.CBrigadeOrder brigade in line.Brigades)
			{
				needs[brigade.Type.DatabaseIndex] -= line.Size;
			}
		}
	}

	/// <summary>The needs in words, in the database's order; <c>NO_NEEDS_FOR_BUILDS</c> with none to show.</summary>
	public static string NeedsText(Func<string, string> localise, CGameState state, int[]? needs)
	{
		string counts = needs is null ? "" : Counts(state.SubUnitTypes.Values.OrderBy(type => type.DatabaseIndex).Select(type => (type.Key, needs[type.DatabaseIndex])));
		return counts.Length > 0 ? counts : localise("NO_NEEDS_FOR_BUILDS");
	}

	/// <summary>The production screen's line of a theatre's needs: the counts, each followed by a comma, the last too.</summary>
	public static string QueueNeedsText(Func<string, string> localise, CGameState state, int[]? needs)
	{
		string counts = needs is null ? "" : Counts(state.SubUnitTypes.Values.OrderBy(type => type.DatabaseIndex).Select(type => (type.Key, needs[type.DatabaseIndex])));
		return counts.Length > 0 ? counts + ", " : localise("NO_NEEDS_FOR_BUILDS");
	}

	/// <summary>That line's tooltip: each counter named in words, and a comma a new line.</summary>
	public static string QueueNeedsTooltip(Func<string, string> localise, CGameState state, string line)
	{
		var text = new StringBuilder();
		for (int at = 0; at < line.Length; at++)
		{
			char character = line[at];
			if (character == '£')
			{
				int end = at + 1;
				while (end < line.Length && (char.IsAsciiLetter(line[end]) || line[end] == '_'))
				{
					end++;
				}
				string key = line[(at + 1)..end];
				if (state.SubUnitTypes.ContainsKey(key))
				{
					text.Append(localise(key));
				}
				at = end - 1;
			}
			else
			{
				text.Append(character == ',' ? '\n' : character);
			}
		}
		return text.ToString();
	}

	public static string LandStanceKey(int stance) => LandStances[Math.Clamp(stance, 0, LandStances.Length - 1)];

	public static string AirStanceKey(int stance) => AirStances[Math.Clamp(stance, 0, AirStances.Length - 1)];

	public static string NavalStanceKey(int stance) => NavalStances[Math.Clamp(stance, 0, NavalStances.Length - 1)];

	/// <summary>
	/// A list of units by type in the database's order, with those of none left out: <c>3 £infantry_brigade, 1 £artillery_brigade</c>,
	/// each type's mark drawing its counter.
	/// </summary>
	public static string Counts(IEnumerable<(string Type, int Count)> counts)
	{
		var text = new StringBuilder();
		foreach ((string type, int count) in counts)
		{
			if (count <= 0)
			{
				continue;
			}
			if (text.Length > 0)
			{
				text.Append(", ");
			}
			text.Append(count.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(CCounterIcons.Mark).Append(type);
		}
		return text.ToString();
	}

	/// <summary>The line of forces: every brigade under the headquarters' theatre, by type.</summary>
	public static string Forces(CGameState state, CTheatre theatre, CUnit headquarters)
	{
		if (state.Country(headquarters.Owner) is not { } owner)
		{
			return "";
		}
		var counts = new Dictionary<string, int>();
		foreach (CUnit unit in owner.AllUnits())
		{
			if (!ReferenceEquals(CTheatres.FindFor(state, unit), theatre))
			{
				continue;
			}
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				counts[brigade.Type.Key] = counts.GetValueOrDefault(brigade.Type.Key) + 1;
			}
		}
		return Counts(state.SubUnitTypes.Keys.Select(type => (type, counts.GetValueOrDefault(type))));
	}

	/// <summary>
	/// The plan's objectives in words, empty where the plan is off or has none: up to three by name, joined by commas and an
	/// ampersand; of four or more, the one that scores most with "&amp; more...", the first of equals [exe].
	/// </summary>
	public static string Objectives(Func<string, string> localise, CGameState state, CUnit headquarters)
	{
		CUnitPlan plan = headquarters.Plan;
		if (!plan.Active || plan.Objectives.Count == 0)
		{
			return "";
		}
		if (plan.Objectives.Count <= MostNamedObjectives)
		{
			var names = plan.Objectives.Select(objective => state.Province(objective.Province).Name).ToList();
			if (names.Count == 1)
			{
				return names[0];
			}
			return string.Join(", ", names.Take(names.Count - 1)) + " & " + names[^1];
		}
		CProvince best = state.Province(plan.Objectives[0].Province);
		long bestScore = long.MinValue;
		foreach (CObjective objective in plan.Objectives)
		{
			CProvince province = state.Province(objective.Province);
			long score = ObjectiveScore(state, province);
			if (score > bestScore)
			{
				bestScore = score;
				best = province;
			}
		}
		return localise("OBJ_AND_MORE").Replace("$NAME$", best.Name);
	}

	/// <summary>An objective's weight: its victory points by 20000, its IC, a base of each kind 1000 and being its holder's capital 10000 [exe].</summary>
	public static long ObjectiveScore(CGameState state, CProvince province)
	{
		long score = province.Points * PointWeight + province.Modifier[ProvinceIc].Raw;
		if (CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase).Raw > 0)
		{
			score += BaseWeight;
		}
		if (CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase).Raw > 0)
		{
			score += BaseWeight;
		}
		if (province.Controller is { } holder && state.Country(holder)?.ActingCapital == province.Id)
		{
			score += CapitalWeight;
		}
		return score;
	}

	/// <summary>
	/// The operation line: for each of the headquarters' owner's invasions with a division under this theatre and both ends known, the
	/// sentence of it, one after another with nothing between them; "not part of" where there are none.
	/// </summary>
	public static string CurrentOperation(Func<string, string> localise, CGameState state, CTheatre theatre, CUnit headquarters)
	{
		var text = new StringBuilder();
		if (state.Country(headquarters.Owner) is { } owner)
		{
			foreach (CInvasion invasion in owner.UnitAi.Invasions)
			{
				if (invasion.Objective == 0 || invasion.EmbarkationPort == 0
					|| !invasion.LandForce.Any(unit => ReferenceEquals(CTheatres.FindFor(state, unit), theatre)))
				{
					continue;
				}
				text.Append(localise("THEATRE_INVASION")
					.Replace("$TARGET$", state.Province(invasion.Objective).Name)
					.Replace("$STAGE$", state.Province(invasion.EmbarkationPort).Name));
			}
		}
		return text.Length > 0 ? text.ToString() : localise("NO_INVASION_PLAN");
	}

	/// <summary>The fronts that get a row: of two provinces or more, facing somebody who is no friend of the player, in the theatre's order.</summary>
	public static List<CFront> AreaFronts(CGameState state, CCountry player, CTheatre theatre)
	{
		return theatre.Fronts
			.Where(front => front.Provinces.Count >= 2 && front.Enemy is { } enemy && !CCountryLists.IsFriend(state, player, enemy, true))
			.ToList();
	}

	/// <summary>
	/// The province that names a front: the middle of it counts for four and its ends for nothing, and each province's infrastructure is added;
	/// the highest above nought, the first of equals. Null where none is above nought, which leaves the row blank [exe].
	/// </summary>
	public static CProvince? NamingProvince(CGameState state, CFront front)
	{
		int count = front.Provinces.Count;
		int half = count / 2;
		CProvince? best = null;
		float bestScore = 0f;
		for (int index = 0; index < count; index++)
		{
			CProvince province = state.Province(front.Provinces[index]);
			float distance = half == 0 ? 0f : Math.Abs(index - half) / (float)half;
			float score = 4f * (1f - distance) + CMovementRules.BuildingLevel(state, province, Infrastructure).Raw / 10 / 1000f;
			if (score > bestScore)
			{
				bestScore = score;
				best = province;
			}
		}
		return best;
	}

	/// <summary>
	/// The sprites a front's picture is looked for under, in the order they are tried: the province's own, each of its regions', the enemy's where the
	/// province is on its acting capital's continent, then its continent's [exe].
	/// </summary>
	public static IEnumerable<string> ContinentSprites(CGameState state, CProvince province, CCountryTag enemy)
	{
		yield return "GFX_theatres_" + province.Id.ToString(CultureInfo.InvariantCulture);
		foreach (int region in province.Regions)
		{
			yield return "GFX_theatres_" + state.Regions[region].Name;
		}
		if (state.Country(enemy) is { ActingCapital: > 0 } foe && state.Province(foe.ActingCapital).Continent == province.Continent)
		{
			yield return "GFX_theatres_" + enemy.Value;
		}
		yield return "GFX_theatres_continent_" + province.Continent;
	}

	/// <summary>A front's title: "Kraków front toward Germany".</summary>
	public static string AreaTitle(Func<string, string> localise, CProvince province, string enemyName)
	{
		return localise("FRONT_TITLE").Replace("$FROM$", province.Name).Replace("$TO$", enemyName);
	}

	/// <summary>The odds sentence's key: by the plan's stance and its power ratio, one figure for every front of the theatre [exe].</summary>
	public static string OddsKey(CGameState state, CUnit headquarters)
	{
		long ratio = PowerRatio(state, headquarters);
		string side = headquarters.Plan.Stance <= CUnitPlan.Defensive ? "ODDS_DEFEND_" : "ODDS_ATTACK_";
		return side + (ratio > GoodRatio ? "GOOD" : ratio > AverageRatio ? "AVERAGE" : "BAD");
	}

	/// <summary>A row's stance button, one step on: it wraps after five, and only ever changes its own words [exe].</summary>
	public static int NextStance(int stance) => (stance + 1) % LandStances.Length;
}
