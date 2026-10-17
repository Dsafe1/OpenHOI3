using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// A unit type's name as an effect - <c>infantry_brigade = current</c>: a brigade, ship or wing of that type for the
/// scope's country in a province, put in the emptiest of its units there that has room, or in a unit of its own, at half
/// its strength and half its organisation. See <c>docs/mechanics/effects.md</c>, section 9.
/// </summary>
public static class CScriptedBrigades
{
	/// <summary>The most brigades a unit may have and still take one more.</summary>
	private const int Room = 4;

	public static void Run(CGameState state, CEffect effect, CEventScope scope)
	{
		if (!state.SubUnitTypes.TryGetValue(effect.Key, out CSubUnitType? type))
		{
			return;
		}
		// Where: a province by its number; THIS's province or its FROM province; or the scope's own - whose controller
		// is then the one it is given to.
		CScriptArgument where = effect.Argument;
		bool current = where.IsWord("current");
		int province = where.IsThis ? scope.Root.Province
			: where.IsFrom ? scope.Root.FromProvince
			: current ? scope.Province
			: where.Province;
		if (province <= 0 || province >= state.Provinces.Count)
		{
			return;
		}
		CCountryTag? ownerTag = current ? state.Province(province).Controller : scope.Country;
		if (state.Country(ownerTag) is not { } owner)
		{
			return;
		}
		Give(state, owner, type, province);
	}

	/// <summary>
	/// The brigade made and put: unless it is a headquarters, in the owner's unit in the province of its arm with fewer
	/// than four, whose first brigade is no headquarters, the emptiest - the first of equals; otherwise in a new unit.
	/// </summary>
	public static CSubUnit Give(CGameState state, CCountry owner, CSubUnitType type, int province)
	{
		CUnit? into = null;
		if (type.Key != CCountryConditions.HeadquartersBrigade)
		{
			int best = 0;
			foreach (CUnit unit in state.UnitsIn(province))
			{
				if (unit.Owner != owner.Tag || unit.IsRemoved || !SameArm(unit, type) || unit.SubUnits.Count >= Room
					|| (unit.SubUnits.Count > 0 && unit.SubUnits[0].Type.Key == CCountryConditions.HeadquartersBrigade))
				{
					continue;
				}
				int score = (Room - unit.SubUnits.Count) * 10;
				if (score > best)
				{
					best = score;
					into = unit;
				}
			}
		}

		bool made = into is null;
		if (into is null)
		{
			EUnitKind kind = type.Branch switch
			{
				EUnitBranch.Naval => EUnitKind.Navy,
				EUnitBranch.Air => EUnitKind.Air,
				_ => EUnitKind.Division,
			};
			into = new CUnit(kind, owner.Tag) { Id = ++state.LastUnitId, Location = province };
			if (kind != EUnitKind.Division)
			{
				into.Base = province;
			}
		}

		var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = into };
		brigade.Name = CUnitNames.ForSubUnit(state, owner, type);
		CUnitNames.Register(owner, brigade.Name);
		// Every technology at nought - the maintainer's word on the original, 2026-10-16.
		CUnits.RebuildStats(brigade);
		brigade.Strength = CFixedPoint.Max(CFixedPoint.Zero, CUnits.MaxStrength(state, owner, brigade) / 2);
		brigade.Highest = brigade.Strength;
		brigade.Organisation = CFixedPoint.Max(CFixedPoint.Zero, COrganisation.Ceiling(state, owner, brigade) / 2);
		into.SubUnits.Add(brigade);

		if (made)
		{
			into.Name = into.Kind == EUnitKind.Division ? CUnitNames.ForDivision(state, owner, type) : brigade.Name;
			if (into.Kind == EUnitKind.Division)
			{
				CUnitNames.Register(owner, into.Name);
			}
			owner.Units.Add(into);
		}
		state.ForgetPositions();
		CCombats.CheckForCombat(state, into, province);
		return brigade;
	}

	private static bool SameArm(CUnit unit, CSubUnitType type)
	{
		return type.Branch switch
		{
			EUnitBranch.Naval => unit.Kind == EUnitKind.Navy,
			EUnitBranch.Air => unit.Kind == EUnitKind.Air,
			_ => CMovementRules.IsLandUnit(unit),
		};
	}
}
