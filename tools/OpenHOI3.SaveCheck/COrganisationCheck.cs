using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

/// <summary>
/// Sets each brigade's organisation beside the save's, two days in:
/// <c>docs/mechanics/organisation.md</c>, section 7.
///
/// The original's two days are run before a game is "running" in its own sense, so a
/// brigade is held to its type's figure and nothing else during them; and afterwards a
/// change of law - its AI makes many as a game opens - holds every brigade of that
/// country to the ceiling a running game gives it. A save does not say which countries
/// had a law changed, so each country is judged both ways and the better is taken.
/// </summary>
internal static class COrganisationCheck
{
	private static readonly HashSet<string> UnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"theatre", "armygroup", "army", "corps", "division", "navy", "air",
	};

	private static readonly HashSet<string> SubUnitKeywords = new(StringComparer.OrdinalIgnoreCase)
	{
		"regiment", "ship", "wing",
	};

	/// <summary>
	/// One hour of the two days for a country's units: organisation comes back as it
	/// does in a game, but the most a brigade may have is its type's figure.
	/// </summary>
	public static void RunHourBeforeTheGame(CGameState state, CCountry country)
	{
		var crowding = new CBaseCrowding(state);
		long lowBonus = state.Defines[CManpower.MilitarySection, COrganisation.LowRegainBonus].Raw;
		foreach (CUnit unit in country.AllUnits())
		{
			long factor = COrganisation.RegainFactor(state, country, unit, crowding);
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				COrganisation.Regain(subUnit, factor, subUnit.Stats[CSubUnitType.DefaultOrganisation].Raw, lowBonus);
			}
		}
	}

	/// <summary>The organisation of each of a country's brigades, by its number.</summary>
	public static Dictionary<int, CFixedPoint> Keep(CCountry country)
	{
		var kept = new Dictionary<int, CFixedPoint>();
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				kept[subUnit.Id] = subUnit.Organisation;
			}
		}
		return kept;
	}

	/// <summary>
	/// What a day of upgrades leaves of a brigade's organisation while no game is
	/// running: what it had, held to its type's figure as its new equipment makes it.
	/// </summary>
	public static void HoldAsBeforeTheGame(CCountry country, IReadOnlyDictionary<int, CFixedPoint> before)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				subUnit.Organisation = CFixedPoint.Min(before[subUnit.Id], subUnit.Stats[CSubUnitType.DefaultOrganisation]);
			}
		}
	}

	/// <summary>
	/// Compares a country's brigades, which are as the two days left them, with the
	/// save's. Answers whether the save fits the country's brigades having been held to
	/// their ceilings afterwards, or null where the two readings give the same figures.
	/// </summary>
	public static bool? Compare(CGameState state, ClausewitzNode node, CCountry country,
		IReadOnlyDictionary<int, ClausewitzNode> savedUnits, IReadOnlyDictionary<int, ClausewitzNode> savedSubUnits,
		Action<string, bool, Func<string>> compare)
	{
		string tag = country.Tag.Value;

		// The ratio the save's own figures give: its officers over what its brigades call
		// for, the ones the original's AI added among them.
		CFixedPoint needed = Needed(node, state);
		CFixedPoint officers = Number(node.GetString("officers"));
		CFixedPoint ours = country.OfficerRatio;
		country.OfficerRatio = COfficers.Ratio(state, officers, needed);

		var rows = new List<(CSubUnit SubUnit, CFixedPoint Saved, CFixedPoint Left, CFixedPoint Held, bool Commanded)>();
		bool isHeld = false;
		bool differ = false;
		foreach (CUnit unit in country.AllUnits())
		{
			// The army is the level whose commander counts. It is the files' chain of command
			// that stood when the brigades were held - the AI's theatres came after - and
			// the save that says who had been given a commander by then.
			bool commanded = COrganisation.ArmyAbove(unit) is { } army
				&& savedUnits.TryGetValue(army.Id, out ClausewitzNode? savedArmy) && savedArmy.Get("leader") is not null;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (!savedSubUnits.TryGetValue(subUnit.Id, out ClausewitzNode? saved)
					|| !saved.GetString("type").Equals(subUnit.Type.Key, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				CFixedPoint savedOrganisation = Number(saved.GetString("organisation"));
				CFixedPoint left = subUnit.Organisation;
				CFixedPoint held = CFixedPoint.Min(left, COrganisation.Ceiling(state, country, subUnit));
				rows.Add((subUnit, savedOrganisation, left, held, commanded));

				// Nothing but being held brings a brigade below what the two days left it.
				isHeld |= savedOrganisation < left;
				differ |= held != left;
			}
		}

		foreach ((CSubUnit subUnit, CFixedPoint saved, CFixedPoint left, CFixedPoint held, bool commanded) in rows)
		{
			CFixedPoint expected = isHeld ? held : left;
			string what = "two days in: the organisation of each brigade";
			if (isHeld && commanded)
			{
				what += ", held to its ceiling under an army with a commander (leaders are not built)";
			}
			else if (isHeld)
			{
				what += ", held to its ceiling";
			}
			compare(what, saved == expected,
				() => $"{tag} {subUnit.Id} '{subUnit.Name}' {subUnit.Type.Key}: save {saved}, as the two days left it {left}, held {held}  " +
					$"(reserve {subUnit.IsReserve}, its own figure {subUnit.Stats[CSubUnitType.DefaultOrganisation]}, " +
					$"morale {subUnit.Stats[COrganisation.Morale]}, officer ratio {country.OfficerRatio} = {officers} of {needed}" +
					$"{(commanded && saved < left ? $", which a commander adding {Added(subUnit, saved, held)} would give" : "")})");
		}

		country.OfficerRatio = ours;
		return differ ? isHeld : null;
	}

	/// <summary>
	/// About how much a commander must have added to a brigade's own figure for its
	/// ceiling to come out at the save's: the ceiling is the figure times parts that
	/// are the same either way.
	/// </summary>
	private static string Added(CSubUnit subUnit, CFixedPoint saved, CFixedPoint held)
	{
		long own = subUnit.Stats[CSubUnitType.DefaultOrganisation].Raw;
		if (held.Raw <= 0)
		{
			return "?";
		}
		double added = (double)saved.Raw * own / held.Raw - own;
		return (added / CFixedPoint.Scale).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>The officers a country's saved brigades call for, the ones the original's AI added among them.</summary>
	private static CFixedPoint Needed(ClausewitzNode node, CGameState state)
	{
		CFixedPoint needed = CFixedPoint.Zero;
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock)
			{
				continue;
			}
			if (SubUnitKeywords.Contains(child.Key))
			{
				if (state.SubUnitTypes.TryGetValue(child.GetString("type"), out CSubUnitType? type))
				{
					needed += type.Stats[CSubUnitType.Officers];
				}
				needed += Needed(child, state);                       // a carrier's air group is written inside the ship
			}
			else if (UnitKeywords.Contains(child.Key))
			{
				needed += Needed(child, state);
			}
		}
		return needed;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
