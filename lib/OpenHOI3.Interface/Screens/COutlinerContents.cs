using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the outliner lists: each switched-on section that has anything, in the sections' order, its title first and
/// then its rows - the player's battles, units with the trees of those expanded, allied objectives and bombings. See
/// <c>docs/mechanics/outliner.md</c>, section 2.
/// </summary>
public static class COutlinerContents
{
	/// <summary>
	/// The rows, in order. <paramref name="expanded"/> answers whether a unit's subordinates are shown under it: the
	/// client's, every unit collapsed to begin with.
	/// </summary>
	public static List<COutlinerRow> Gather(CGameState state, CCountryTag player, ICollection<EOutlinerSection> wanted,
		Func<CUnit, bool> expanded)
	{
		var rows = new List<COutlinerRow>();
		if (state.Country(player) is not { } country)
		{
			return rows;
		}
		var bySection = new Dictionary<EOutlinerSection, List<COutlinerRow>>();
		foreach (EOutlinerSection section in Enum.GetValues<EOutlinerSection>())
		{
			bySection[section] = new List<COutlinerRow>();
		}
		GatherCombats(country, bySection);
		GatherUnits(country, bySection, expanded);
		GatherObjectives(state, player, bySection[EOutlinerSection.AlliedObjectives]);
		foreach (EOutlinerSection section in Enum.GetValues<EOutlinerSection>())
		{
			List<COutlinerRow> held = bySection[section];
			if (!wanted.Contains(section) || held.Count == 0)
			{
				continue;
			}
			rows.Add(new COutlinerRow(section, IsTitle: true));
			rows.AddRange(held);
		}
		return rows;
	}

	/// <summary>Every battle and bombing a unit of the player's is in, once each, in the order met.</summary>
	private static void GatherCombats(CCountry country, Dictionary<EOutlinerSection, List<COutlinerRow>> bySection)
	{
		var seen = new HashSet<CCombat>();
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CCombat combat in unit.Combats)
			{
				EOutlinerSection? section = combat.Kind switch
				{
					ECombatKind.Land => EOutlinerSection.LandCombat,
					ECombatKind.Naval => EOutlinerSection.NavalCombat,
					ECombatKind.Air => EOutlinerSection.AirCombat,
					_ => null,
				};
				if (section is { } kind && seen.Add(combat))
				{
					bySection[kind].Add(new COutlinerRow(kind, Combat: combat, Province: combat.Province));
				}
			}
			// A bombing is ours where the unit of the player's in it is an air unit; whichever side it is on.
			if (unit.Bombing is { } bombing && seen.Add(bombing))
			{
				EOutlinerSection kind = unit.Kind == EUnitKind.Air ? EOutlinerSection.OurBombings : EOutlinerSection.HostileBombing;
				bySection[kind].Add(new COutlinerRow(kind, Combat: bombing, Province: bombing.Province));
			}
		}
	}

	/// <summary>
	/// The units' sections: each root, then the tree under it where expanded. A root is a unit with no superior, or a
	/// fleet or an air unit under a land unit - which is so listed twice.
	/// </summary>
	private static void GatherUnits(CCountry country, Dictionary<EOutlinerSection, List<COutlinerRow>> bySection,
		Func<CUnit, bool> expanded)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.IsRemoved || !IsRoot(unit))
			{
				continue;
			}
			EOutlinerSection section;
			if (unit.Kind == EUnitKind.Navy)
			{
				section = EOutlinerSection.NavalUnits;
			}
			else if (unit.Kind == EUnitKind.Air)
			{
				// An air unit whose base is a carrier is the carrier's, and not listed on its own.
				if (unit.CarriedBy is not null && CCarriers.IsCarrierAirGroup(unit))
				{
					continue;
				}
				section = EOutlinerSection.AirUnits;
			}
			else
			{
				section = EOutlinerSection.LandUnits;
			}
			AddTree(bySection[section], section, unit, 0, expanded);
		}
	}

	private static bool IsRoot(CUnit unit)
	{
		if (unit.Parent is not { } parent)
		{
			return true;
		}
		return unit.Kind is EUnitKind.Navy or EUnitKind.Air && parent.Kind is not (EUnitKind.Navy or EUnitKind.Air);
	}

	private static void AddTree(List<COutlinerRow> rows, EOutlinerSection section, CUnit unit, int depth,
		Func<CUnit, bool> expanded)
	{
		rows.Add(new COutlinerRow(section, Unit: unit, Depth: depth));
		if (!expanded(unit))
		{
			return;
		}
		foreach (CUnit child in unit.Children)
		{
			if (!child.IsRemoved)
			{
				AddTree(rows, section, child, depth + 1, expanded);
			}
		}
	}

	/// <summary>One row a province: first what the player was asked, then what it asked.</summary>
	private static void GatherObjectives(CGameState state, CCountryTag player, List<COutlinerRow> rows)
	{
		var asked = new HashSet<int>();
		foreach ((int province, CCountryTag _) in state.AlliedObjectives.AskedOf(player))
		{
			if (asked.Add(province))
			{
				rows.Add(new COutlinerRow(EOutlinerSection.AlliedObjectives, Province: province, AskedOfPlayer: true));
			}
		}
		var asking = new HashSet<int>();
		foreach ((int province, CCountryTag _) in state.AlliedObjectives.Asking(player))
		{
			if (asking.Add(province))
			{
				rows.Add(new COutlinerRow(EOutlinerSection.AlliedObjectives, Province: province));
			}
		}
	}
}
