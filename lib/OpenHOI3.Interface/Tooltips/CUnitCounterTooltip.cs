using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The tooltip over a unit's counter, which the map shows in place of the province's: every unit at the hovered unit's
/// place - of every country, with no test of who sees it - as a line of their flags and the best leader among them, then a
/// line for each kind of brigade with its counter, its count and a strength bar for each, all of it told as well as the
/// player knows the hovered unit. See <c>docs/mechanics/tooltips.md</c>, section 7.1, and
/// <c>reversing/findings/FINDINGS-countertooltip.md</c>.
/// </summary>
public static class CUnitCounterTooltip
{
	/// <summary>A brigade's type with at least this priority is named under 6 of intelligence; a lower one is unknown [exe].</summary>
	public const int NamedFromPriority = 6;

	/// <summary>From this share of its most strength, in tenths of thousandths, a brigade's bar is full [exe].</summary>
	public const long FullFrom = 9900;

	/// <summary>The bars of a line are put in fives, a space after each five.</summary>
	private const int BarsAGroup = 5;

	private const string Priority = "priority";

	public static string Build(Func<string, string> localise, CGameState state, CUnit hovered, CCountryTag? player)
	{
		int known = CUnitIntel.Level(state, hovered, player);
		List<CUnit> units = UnitsAt(state, hovered);
		var text = new StringBuilder();
		if (units.Count > 0)
		{
			foreach (CCountryTag owner in units.Select(unit => unit.Owner).Distinct())
			{
				text.Append('@').Append(owner.Value).Append(' ');
			}
			text.Append(known < CUnitIntel.NameShown ? "?" : BestLeader(units)?.Name ?? localise("NO_LEADER")).Append('\n');
		}
		if (known < CUnitIntel.TypeShown)
		{
			return text.Append("? £unknown").ToString();
		}
		var byType = new SortedDictionary<int, (CSubUnitType Type, List<CSubUnit> Brigades)>();
		foreach (CUnit unit in units)
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				if (!byType.TryGetValue(brigade.Type.DatabaseIndex, out var bucket))
				{
					byType[brigade.Type.DatabaseIndex] = bucket = (brigade.Type, new List<CSubUnit>());
				}
				bucket.Brigades.Add(brigade);
			}
		}
		foreach ((CSubUnitType type, List<CSubUnit> brigades) in byType.Values)
		{
			string count = brigades.Count.ToString(CultureInfo.InvariantCulture);
			if (known >= 8)
			{
				text.Append(count).Append(" £").Append(type.Key).Append(' ');
				for (int index = 0; index < brigades.Count; index++)
				{
					text.Append(Bar(state, brigades[index]));
					if ((index + 1) % BarsAGroup == 0)
					{
						text.Append(' ');
					}
				}
				text.Append('\n');
			}
			else if (known >= 6)
			{
				// A space before a count of two figures and not before one, as read.
				text.Append(brigades.Count < 10 ? "" : " ").Append(count).Append(" £").Append(type.Key).Append('\n');
			}
			else
			{
				text.Append(type.Stats[Priority].Raw >= NamedFromPriority * CFixedPoint.Scale ? "? £" + type.Key : "? £unknown")
					.Append('\n');
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// The units the tooltip tells: a fleet in port the fleets in that port, an air unit at its base the units based there,
	/// anything else the units in its province - less land units at sea, and fleets in port under a hovered land unit.
	/// *Ours*: in the order of the countries and then of each one's units, where the original's is the order they entered
	/// the province.
	/// </summary>
	public static List<CUnit> UnitsAt(CGameState state, CUnit hovered)
	{
		if (hovered.Location <= 0 || hovered.Location >= state.Provinces.Count)
		{
			return new List<CUnit>();
		}
		bool atSea = state.Provinces[hovered.Location].IsSea;
		bool inPort = hovered.Kind == EUnitKind.Navy && !atSea;
		bool atBase = hovered.Kind == EUnitKind.Air && hovered.Base > 0 && hovered.Base == hovered.Location;
		var found = new List<CUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsRemoved || unit.Location <= 0)
				{
					continue;
				}
				bool listed = inPort ? unit.Kind == EUnitKind.Navy && unit.Location == hovered.Location
					: atBase ? unit.Kind == EUnitKind.Air && unit.Base == hovered.Base
					: unit.Location == hovered.Location;
				if (!listed)
				{
					continue;
				}
				bool land = unit.Kind is not (EUnitKind.Navy or EUnitKind.Air);
				if (land && atSea)
				{
					continue;
				}
				if (hovered.Kind is not (EUnitKind.Navy or EUnitKind.Air) && unit.Kind == EUnitKind.Navy && !atSea)
				{
					continue;
				}
				found.Add(unit);
			}
		}
		return found;
	}

	/// <summary>
	/// The leader the head line names: going down the list, a unit's leader replaces the one so far where his rank is at
	/// least as high and his skill higher - or there is none so far. So it hangs on the list's order, and is not simply the
	/// highest ranked.
	/// </summary>
	public static CLeader? BestLeader(IEnumerable<CUnit> units)
	{
		CLeader? best = null;
		foreach (CUnit unit in units)
		{
			if (unit.Leader is not { } leader)
			{
				continue;
			}
			if (best is null || (leader.Rank >= best.Rank && leader.Skill > best.Skill))
			{
				best = leader;
			}
		}
		return best;
	}

	/// <summary>A brigade's strength bar: the frame of tenths of its most strength, cut, and full from 99%.</summary>
	public static string Bar(CGameState state, CSubUnit brigade)
	{
		CCountry? owner = brigade.Unit is { } unit ? state.Country(unit.Owner) : null;
		long most = owner is null ? brigade.Type.Stats[CSubUnitType.MaxStrength].Raw : CUnits.MaxStrength(state, owner, brigade).Raw;
		if (most <= 0)
		{
			return "{10";
		}
		long share = brigade.Strength.Raw * 1000 / most * 10;
		return share >= FullFrom ? "{10" : "{0" + (share / 1000).ToString(CultureInfo.InvariantCulture);
	}
}
