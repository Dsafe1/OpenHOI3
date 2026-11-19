using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// A fleet's base, as the original's unit loader and its base check leave it (<c>docs/mechanics/fleetsandair.md</c>,
/// section 2.1; <c>reversing/findings/FINDINGS-seaair.md</c>, section 3): a file's or a save's <c>base</c> is kept only
/// where that province has a naval base built, and as the fleet's block closes a fleet with no base, or one an enemy
/// holds, is given the nearest by sea of its country's naval bases, else of its faction's.
/// </summary>
public static class CFleetBase
{
	/// <summary>The distance, in thousandths, the first of the country's naval bases has to beat [exe].</summary>
	private const long OwnStart = 999_999_000;

	/// <summary>The same for the faction's, a different figure the original starts that walk at [exe].</summary>
	private const long FactionStart = 1_410_000_920;

	/// <summary>Whether a province may be a fleet's base as a unit block is read: a naval base built, to any level.</summary>
	public static bool MayBeBase(CGameState state, int province)
	{
		return province > 0 && province < state.Provinces.Count
			&& CMovementRules.BuildingLevel(state, state.Province(province), CMovementRules.NavalBase) > CFixedPoint.Zero;
	}

	/// <summary>
	/// The original's base check of a fleet: nothing where it has a base no enemy of its country holds; otherwise the base
	/// is taken away and the country's naval bases are measured by sea from where the fleet lies, in their list's order,
	/// the first strictly nearer winning - a province with no place in the table of distances measures nought, so from a
	/// coast with no port the first base of the list is taken - and with none, its faction's members' bases the same way.
	/// </summary>
	public static void Check(CGameState state, CCountry owner, CUnit fleet)
	{
		if (fleet.Kind != EUnitKind.Navy)
		{
			return;
		}
		if (fleet.Base > 0 && fleet.Base < state.Provinces.Count
			&& !CMovementRules.IsEnemy(state, state.Province(fleet.Base).Controller, owner.Tag))
		{
			return;
		}
		fleet.Base = 0;
		if (fleet.Location <= 0 || fleet.Location >= state.Provinces.Count)
		{
			return;
		}
		CProvince at = state.Province(fleet.Location);
		long best = OwnStart;
		foreach (CProvince province in NavalBases(state, owner.Tag))
		{
			long distance = state.NavalDistances.Between(province, at) * CFixedPoint.Scale;
			if (distance < best)
			{
				best = distance;
				fleet.Base = province.Id;
			}
		}
		if (fleet.Base != 0 || owner.Faction.Length == 0)
		{
			return;
		}
		best = FactionStart;
		foreach (CCountry member in CCountryLists.FactionMembers(state, owner.Faction))
		{
			foreach (CProvince province in NavalBases(state, member.Tag))
			{
				long distance = state.NavalDistances.Between(province, at) * CFixedPoint.Scale;
				if (distance < best)
				{
					best = distance;
					fleet.Base = province.Id;
				}
			}
		}
	}

	/// <summary>A country's naval bases: the provinces it controls with a naval base built, in the order it took them.</summary>
	public static List<CProvince> NavalBases(CGameState state, CCountryTag country)
	{
		var bases = new List<CProvince>();
		foreach (CProvince province in CCountryLists.Controlled(state, country))
		{
			if (CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero)
			{
				bases.Add(province);
			}
		}
		return bases;
	}
}
