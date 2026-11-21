using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// What a change of hands does with the air and naval units based in the province, the original's
/// <c>Province_SetController</c> with its consequences (<c>docs/mechanics/war.md</c>, section 11;
/// <c>reversing/findings/FINDINGS-surrender.md</c>, D.2): where the province has an air base built, every air unit based
/// there whose owner is an enemy of the new holder is given the old holder's nearest other air base - or, where the old
/// holder is in a real faction, the nearest of every member's, in the faction's order - and is sent there if it stands in
/// the province; with none, it loses its base, and goes out of the game at the hour's end where it is neither moving nor
/// fighting. The same for a naval base and its fleets.
/// </summary>
public static class CCapturedBases
{
	/// <summary>The distance, in thousandths of the map's measure, the first candidate has to beat [exe].</summary>
	private const long Start = 1_410_000_920;

	/// <summary>Called as the province changes hands, before the new holder is written: the old holder still holds it.</summary>
	public static void ControllerChanging(CGameState state, CProvince province, CCountryTag? old, CCountryTag controller)
	{
		if (state.Country(old) is not { } former)
		{
			return;
		}
		if (CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero)
		{
			Rebase(state, province, former, controller, EUnitKind.Air);
		}
		if (CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero)
		{
			Rebase(state, province, former, controller, EUnitKind.Navy);
		}
	}

	private static void Rebase(CGameState state, CProvince province, CCountry former, CCountryTag controller, EUnitKind kind)
	{
		string building = kind == EUnitKind.Air ? CMovementRules.AirBase : CMovementRules.NavalBase;
		int target = Nearest(state, province, former, building);
		foreach (CUnit unit in BasedHere(state, province, kind))
		{
			if (!CMovementRules.IsEnemyIn(state, controller, unit.Owner, province.Id))
			{
				continue;
			}
			if (target == 0)
			{
				unit.Base = 0;
				if (!unit.IsMoving && !unit.IsFighting)
				{
					CPendingUnitRemovals.Queue(state, unit);
				}
				continue;
			}
			unit.Base = target;
			if (unit.Location == province.Id && state.Country(unit.Owner) is { } owner)
			{
				CUnitOrders.Give(state, owner, unit, kind == EUnitKind.Air ? EUnitOrder.RebaseAir : EUnitOrder.Rebase, target);
			}
		}
	}

	/// <summary>
	/// The nearest province other than this one, in a straight line, among the old holder's bases of the kind - a
	/// province it holds with the base built to any level - or every member's of its real faction, the first strictly
	/// nearer winning; 0 for none.
	/// </summary>
	private static int Nearest(CGameState state, CProvince province, CCountry former, string building)
	{
		IEnumerable<CCountry> candidates = former.Faction.Length > 0
			? CCountryLists.FactionMembers(state, former.Faction)
			: new[] { former };
		long best = Start;
		int target = 0;
		foreach (CCountry country in candidates)
		{
			foreach (CProvince candidate in CCountryLists.Controlled(state, country.Tag))
			{
				if (candidate.Id == province.Id
					|| CMovementRules.BuildingLevel(state, candidate, building) <= CFixedPoint.Zero)
				{
					continue;
				}
				long distance = CMapDistance.Between(province, candidate, state.MapWidth) * CFixedPoint.Scale;
				if (distance < best)
				{
					best = distance;
					target = candidate.Id;
				}
			}
		}
		return target;
	}

	/// <summary>The units of the kind based in the province, every country's, in the countries' order and each country's own.</summary>
	private static List<CUnit> BasedHere(CGameState state, CProvince province, EUnitKind kind)
	{
		var units = new List<CUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Kind == kind && unit.Base == province.Id && !unit.IsRemoved)
				{
					units.Add(unit);
				}
			}
		}
		return units;
	}
}
