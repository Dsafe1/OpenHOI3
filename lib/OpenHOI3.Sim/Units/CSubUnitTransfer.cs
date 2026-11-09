using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Transport;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Whether a brigade, a ship or a wing may be moved into a unit, and whether two units may be reorganised at all - the
/// original's two tests, asked in its order. See <c>docs/mechanics/reorganise.md</c>, sections 1 and 4.
/// </summary>
public static class CSubUnitTransfer
{
	/// <summary>
	/// Whether the unit may take the brigade (section 4): refused at the first of eleven tests. With
	/// <paramref name="sameProvince"/> the two units have to stand in one province.
	/// </summary>
	public static bool MayTake(CGameState state, CCountry country, CUnit unit, CSubUnit subUnit, bool sameProvince)
	{
		if (unit.Lender is not null || subUnit.Unit?.Lender is not null || unit.IsFighting || !CUnitAvailability.IsBuildableType(subUnit.Type) || !SameKind(unit, subUnit))
		{
			return false;
		}
		bool isCag = subUnit.Type.Flag(CAirFire.IsCag);
		if (CCarriers.IsCarrierAirGroup(unit) && !isCag)
		{
			return false;
		}
		// A carrier's room: its size above every wing aboard and the unit's own - the unit, aboard, counted twice.
		if (isCag && unit.CarriedBy is { } ship)
		{
			long wings = unit.SubUnits.Count;
			foreach (CUnit aboard in ship.Carried)
			{
				wings += aboard.SubUnits.Count;
			}
			if (ship.Stats[CShipCensus.CarrierSize].Raw / CFixedPoint.Scale <= wings)
			{
				return false;
			}
		}
		if (subUnit.Unit is { } from)
		{
			// A headquarters' first brigade stays.
			if (from.Kind < EUnitKind.Division && from.SubUnits.Count > 0 && ReferenceEquals(from.SubUnits[0], subUnit))
			{
				return false;
			}
			if (unit.SubUnits.Count > 0 && CCarriers.IsCarrierAirGroup(unit) != CCarriers.IsCarrierAirGroup(from))
			{
				return false;
			}
			// Nothing is lent here, so the loan test is never met.
			if (from.IsFighting || (sameProvince && from.Location != unit.Location))
			{
				return false;
			}
		}
		return !CMovementRules.IsLandUnit(unit) || unit.SubUnits.Count < CUnitAvailability.MostBrigades(state, country);
	}

	/// <summary>
	/// The move itself, which the transfer and the split both make: the brigade from its unit to this one; a unit with no
	/// name named by its kind, numbered; given a leader where its country chooses its own, it has none and it is not a
	/// lone land brigade; a unit left empty losing its leader; a fleet left lighter checked for what it carries. A carrier's
	/// air groups ride on it. Nothing is tested here.
	/// </summary>
	public static void Move(CGameState state, CCountry country, CSubUnit subUnit, CUnit unit)
	{
		CUnit? source = subUnit.Unit;
		source?.SubUnits.Remove(subUnit);
		unit.SubUnits.Add(subUnit);
		subUnit.Unit = unit;
		if (unit.Name.Length < 2)
		{
			unit.Name = CUnitNames.ForKind(state, country, unit.Kind, unit.SubUnits[0].Type);
			CUnitNames.Register(country, unit.Name);
		}
		if (country.AutoAssignLeaders && unit.Leader is null
			&& !(unit.Kind <= EUnitKind.Division && unit.SubUnits.Count == 1))
		{
			Leaders.CLeaders.AssignBest(state, country, unit);
		}
		if (source is { SubUnits.Count: 0 })
		{
			Leaders.CLeaders.SetLeader(state, source, null, false, false);
		}
		if (source is { Kind: EUnitKind.Navy })
		{
			CTransport.CheckOverload(state, source);
		}
		state.ForgetPositions();
	}

	/// <summary>A brigade into a land unit, a ship into a fleet, a wing into an air unit.</summary>
	private static bool SameKind(CUnit unit, CSubUnit subUnit)
	{
		return subUnit.Type.Branch switch
		{
			EUnitBranch.Land => CMovementRules.IsLandUnit(unit),
			EUnitBranch.Naval => unit.Kind == EUnitKind.Navy,
			EUnitBranch.Air => unit.Kind == EUnitKind.Air,
			_ => false,
		};
	}

	/// <summary>
	/// Whether the reorganise button may be pressed for these units (section 1): exactly two, each in no battle, not
	/// falling back, going nowhere, with no attack delay, in the first's province, a group of carrier aircraft only with
	/// another and two on a carrier on the same one - and not where the two hold two brigades between them.
	/// </summary>
	public static bool MayReorganise(IReadOnlyList<CUnit> units)
	{
		if (units.Count != 2)
		{
			return false;
		}
		CUnit first = units[0];
		bool firstCag = CCarriers.IsCarrierAirGroup(first);
		int brigades = 0;
		foreach (CUnit unit in units)
		{
			if (unit.Lender is not null || unit.IsFighting || unit.IsRetreating || unit.IsMoving || unit.AttackDelay > CFixedPoint.Zero
				|| unit.Location != first.Location || CCarriers.IsCarrierAirGroup(unit) != firstCag)
			{
				return false;
			}
			if (first.Kind == EUnitKind.Air && first.CarriedBy is { } carrier && !ReferenceEquals(unit.CarriedBy, carrier))
			{
				return false;
			}
			brigades += unit.SubUnits.Count;
		}
		return brigades != 2;
	}
}
