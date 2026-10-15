using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;

/// <summary>
/// Checks the day's step of revolt risk against two savegames of one game with one midnight between them:
///
///   OpenHOI3.SaveCheck revolt &lt;install folder&gt; &lt;earlier save&gt; &lt;later save&gt;
///
/// The world is set as the earlier save has it, as far as revolt risk reads it - who owns and holds each province,
/// its revolt risk and nationalism, the occupation policies, and where every division of the start's orders of battle
/// stands with what strength its brigades have - and every province's suppression is worked out and its revolt risk
/// stepped once; where either save holds revolt risk, the result is set beside the later save's. Divisions made since
/// the start are not in it, and the step is taken with the units where they stood at the earlier save's hour. See
/// <c>docs/mechanics/revolt.md</c>, section 6.
/// </summary>
internal static class CRevoltCheck
{
	private static readonly HashSet<string> UnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"theatre", "armygroup", "army", "corps", "division", "navy", "air",
	};

	public static int Run(string[] args)
	{
		if (args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck revolt <install folder> <earlier save> <later save>");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, firstDays: false);
		ClausewitzNode earlier = ClausewitzParser.ParseFile(args[2]);
		ClausewitzNode later = ClausewitzParser.ParseFile(args[3]);
		Console.WriteLine($"earlier: {earlier.GetString("date")}   later: {later.GetString("date")}");

		// Who owns and holds what, and each province's revolt risk and nationalism.
		foreach (ClausewitzNode node in earlier.Children)
		{
			if (node.Key is null || !int.TryParse(node.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
				|| id <= 0 || id >= state.Provinces.Count || !node.IsBlock)
			{
				continue;
			}
			CProvince province = state.Provinces[id];
			if (CCountryTag.TryParse(node.GetString("owner"), out CCountryTag owner))
			{
				province.Owner = owner;
			}
			if (CCountryTag.TryParse(node.GetString("controller"), out CCountryTag controller))
			{
				province.Controller = controller;
			}
			province.RevoltRisk = Number(node.GetString("revolt_risk"));
			province.Nationalism = Number(node.GetString("nationalism"));
		}

		// The occupation policies, and where each division stands with what strength.
		var units = new Dictionary<int, CUnit>();
		var brigades = new Dictionary<int, CSubUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				units[unit.Id] = unit;
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					brigades[subUnit.Id] = subUnit;
				}
			}
		}
		int placed = 0;
		foreach (ClausewitzNode node in earlier.Children)
		{
			if (node.Key is null || !node.IsBlock || !CCountryTag.TryParse(node.Key, out CCountryTag tag)
				|| state.Country(tag) is not { } country)
			{
				continue;
			}
			foreach (ClausewitzNode pair in node.Children)
			{
				if (pair.IsBlock && pair.Key is not null && CCountryTag.TryParse(pair.Key, out CCountryTag otherTag)
					&& state.Country(otherTag) is { } other && pair.GetString("occupation_policy") is { Length: > 0 } name
					&& COccupationPolicies.Find(state, name) is { } policy)
				{
					CGameState.Status(country, other).OccupationPolicy = policy.Index;
				}
			}
			placed += Place(node, units, brigades);
		}
		state.ForgetPositions();
		CCountryModifiers.Rebuild(state);
		CProvinceModifiers.Rebuild(state);

		int agree = 0;
		int compared = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			province.Suppression = new CFixedPoint(CSuppression.Near(state, province));
		}
		var lines = new List<string>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			CFixedPoint before = province.RevoltRisk;
			CFixedPoint saved = Number(later.Get(province.Id.ToString(CultureInfo.InvariantCulture))?.GetString("revolt_risk"));
			CRevoltRisk.StepDaily(state, province);
			if (before == CFixedPoint.Zero && saved == CFixedPoint.Zero && province.RevoltRisk == CFixedPoint.Zero)
			{
				continue;
			}
			compared++;
			bool same = saved == province.RevoltRisk;
			agree += same ? 1 : 0;
			lines.Add($"{(same ? "agree   " : "DISAGREE")}  {province.Id} {province.Name} ({province.Controller} holds {province.Owner}'s): " +
				$"before {before}, suppression {province.Suppression}, support {province.Modifier[CRevoltRisk.PartisanSupport]}, " +
				$"ours {province.RevoltRisk}, saved {saved}");
		}
		Console.WriteLine($"{placed} divisions placed as the earlier save has them");
		foreach (string line in lines)
		{
			Console.WriteLine(line);
		}
		Console.WriteLine($"revolt risk after one day's step: {agree} of {compared} agree");
		return 0;
	}

	/// <summary>Every division of a country's block put where the save has it, its brigades given the save's strength.</summary>
	private static int Place(ClausewitzNode node, Dictionary<int, CUnit> units, Dictionary<int, CSubUnit> brigades)
	{
		int placed = 0;
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock || !UnitKeywords.Contains(child.Key))
			{
				continue;
			}
			if (child.Get("id") is { IsBlock: true } id && units.TryGetValue(id.GetInt("id"), out CUnit? unit)
				&& int.TryParse(child.GetString("location"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int location))
			{
				unit.Location = location;
				placed++;
				foreach (ClausewitzNode regiment in child.Children.Where(entry => entry.Key == "regiment"))
				{
					if (regiment.Get("id") is { IsBlock: true } brigadeId && brigades.TryGetValue(brigadeId.GetInt("id"), out CSubUnit? brigade))
					{
						brigade.Strength = Number(regiment.GetString("strength"));
					}
				}
			}
			placed += Place(child, units, brigades);
		}
		return placed;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
