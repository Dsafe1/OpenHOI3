using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// When the unit panel's attach, detach and headquarters buttons may be pressed, and what the window of commanders lists.
/// See <c>docs/mechanics/attaching.md</c>.
/// </summary>
public static class CAttachText
{
	public const string WindowName = "attachment_selection_panel";
	public const string RowName = "unit_assignment_entry";

	/// <summary>Whether a unit is carrier air groups and nothing else; it has at least one.</summary>
	public static bool IsAllCarrierGroups(CUnit unit)
	{
		return unit.SubUnits.Count > 0 && unit.SubUnits.All(wing => wing.Type.Flag(CAirFire.IsCag));
	}

	/// <summary>Whether <c>attach_button</c> may be pressed: some unit of the country may take this one.</summary>
	public static bool MayAttach(CGameState state, CCountry country, CUnit unit)
	{
		return !IsAllCarrierGroups(unit) && country.AllUnits().Any(other => CDeployment.MayTakeSubordinate(state, other, unit));
	}

	/// <summary>Whether <c>detach_button</c> shows, for a unit with a commander.</summary>
	public static bool ShowsDetach(CUnit unit) => unit.Parent is not null && !IsAllCarrierGroups(unit);

	/// <summary>
	/// The commanders offered, each of the country's units as made that may take this one, put in by the original's
	/// insertion from the tail. Do not make it a sort: the comparison goes on to distance even where the ranks say the
	/// other way, and which of two ends where hangs on the order they came in.
	/// </summary>
	public static List<CUnit> Candidates(CGameState state, CCountry country, CUnit unit)
	{
		var list = new List<CUnit>();
		foreach (CUnit candidate in country.UnitsInOrder())
		{
			if (!CDeployment.MayTakeSubordinate(state, candidate, unit))
			{
				continue;
			}
			int at = list.Count;
			while (at > 0 && SortsAfter(state, unit, list[at - 1], candidate))
			{
				at--;
			}
			list.Insert(at, candidate);
		}
		return list;
	}

	/// <summary>Whether <paramref name="a"/> goes after <paramref name="b"/>: a land unit wants its lowest ranks first, anything else its highest; then the further goes after.</summary>
	public static bool SortsAfter(CGameState state, CUnit subject, CUnit a, CUnit b)
	{
		bool land = subject.Kind <= EUnitKind.Division;
		if (land ? a.Kind < b.Kind : a.Kind > b.Kind)
		{
			return true;
		}
		return Distance(state, subject, a) > Distance(state, subject, b);
	}

	/// <summary>Four times the pixels between the two, as the original's map measures, whole.</summary>
	public static long Distance(CGameState state, CUnit one, CUnit other)
	{
		return CMapDistance.Between(state.Province(one.Location), state.Province(other.Location), state.MapWidth);
	}

	/// <summary>A row's second line: where the commander is, and how far, in words that are not localised in the original either.</summary>
	public static string Location(CGameState state, CUnit subject, CUnit candidate)
	{
		return state.Province(candidate.Location).Name + " (" + Distance(state, subject, candidate).ToString(CultureInfo.InvariantCulture) + "km)";
	}

	/// <summary>
	/// Whether the headquarters button may be pressed: the unit's commander is more than one level up, or it has none, and
	/// the country has a whole headquarters brigade's men - though only a twentieth of them is charged.
	/// </summary>
	public static bool MayCreateHigher(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Parent is { } parent && CAgentUnits.Level(parent) + 1 >= CAgentUnits.Level(unit))
		{
			return false;
		}
		return state.SubUnitTypes.TryGetValue(CTheatreHeadquarters.HqBrigade, out CSubUnitType? type)
			&& country.Manpower.Raw >= type.Stats[CSubUnitStats.BuildCostManpower].Raw;
	}

	/// <summary>Whether <c>disbandbutton</c> may be pressed: as Disband All's test, and not in a battle.</summary>
	public static bool MayDisband(CGameState state, CUnit unit)
	{
		return unit.Lender is null && !unit.IsFighting && Disbandable(state, new[] { unit }).Count == 1;
	}

	/// <summary>Which of several selected units Disband All takes: not a land unit aboard anything, a theatre holding land, or a unit an AI agent runs.</summary>
	public static List<CUnit> Disbandable(CGameState state, IEnumerable<CUnit> units)
	{
		return units.Where(unit => unit.Transport is null && unit.Plane is null && unit.Agent is null
			&& !(unit.Kind == EUnitKind.Theatre && CTheatres.FindFor(state, unit) is { AllProvinces.Count: > 0 })).ToList();
	}

	/// <summary>Which of several selected units <c>detach_multiunit</c> takes: all but a theatre, with no test that one has a commander.</summary>
	public static List<CUnit> Detachable(IEnumerable<CUnit> units)
	{
		return units.Where(unit => unit.Kind != EUnitKind.Theatre).ToList();
	}

	/// <summary>
	/// Which of several selected units a new headquarters takes: all but a theatre - or none, where the highest ranked, the
	/// first of equals, has a commander's commander who could not take it.
	/// </summary>
	public static List<CUnit> ForHigher(CGameState state, IEnumerable<CUnit> units)
	{
		List<CUnit> taken = Detachable(units);
		if (taken.Count == 0)
		{
			return taken;
		}
		CUnit top = taken[0];
		foreach (CUnit unit in taken)
		{
			if (unit.Kind < top.Kind)
			{
				top = unit;
			}
		}
		if (top.Parent?.Parent is { } grand && !CDeployment.MayTakeSubordinate(state, grand, top))
		{
			taken.Clear();
		}
		return taken;
	}

	/// <summary>The men asked of a country before several units are given a headquarters: a brigade's for each.</summary>
	public static bool HasMenFor(CGameState state, CCountry country, int units)
	{
		return state.SubUnitTypes.TryGetValue(CTheatreHeadquarters.HqBrigade, out CSubUnitType? type)
			&& country.Manpower.Raw >= units * type.Stats[CSubUnitStats.BuildCostManpower].Raw;
	}
}
