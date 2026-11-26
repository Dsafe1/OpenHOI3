using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The single panel's new-unit button: when it may be pressed, when Ctrl and a press may split the unit, and the split -
/// a new unit for every brigade but the last, each taking the old unit's first. See <c>docs/mechanics/unitpanel.md</c>,
/// section 7.1.
/// </summary>
public static class CUnitSplit
{
	/// <summary>
	/// Whether the button may be pressed: not for a land unit of one brigade - two, where it has a leader - nor one in a
	/// battle, run by an AI agent at any level above, carrying anything or aboard anything, or with an attack delay; and
	/// then only for a unit of more than one brigade.
	/// </summary>
	public static bool MayPress(CUnit unit)
	{
		if (unit.Lender is not null)
		{
			return false;
		}
		bool land = CMovementRules.IsLandUnit(unit);
		if (land && unit.SubUnits.Count <= (unit.Leader is null ? 1 : 2))
		{
			return false;
		}
		if (unit.IsFighting || CUnitMerge.HasAgentAbove(new[] { unit }))
		{
			return false;
		}
		// Nothing is lent here, so the loan test is never met.
		if (unit.Kind is EUnitKind.Navy or EUnitKind.Air && unit.Passengers.Count > 0)
		{
			return false;
		}
		if (land && (unit.Transport is not null || unit.Plane is not null))
		{
			return false;
		}
		return unit.AttackDelay <= CFixedPoint.Zero && unit.SubUnits.Count > 1;
	}

	/// <summary>
	/// Whether Ctrl and a press split the unit: more than one brigade, in no battle, going nowhere; a fleet or an air unit
	/// at its own base; a land unit holding no headquarters brigade.
	/// </summary>
	public static bool MaySplit(CUnit unit)
	{
		if (unit.SubUnits.Count <= 1 || unit.IsFighting || unit.IsMoving)
		{
			return false;
		}
		if (unit.Kind is EUnitKind.Navy or EUnitKind.Air && !CAirMissions.IsAtOwnBase(unit))
		{
			return false;
		}
		return !CMovementRules.IsLandUnit(unit) || CArmyRole.Of(unit) != CArmyRole.Headquarters;
	}

	/// <summary>
	/// The split: for every brigade but one, a new unit of the old one's level and owner, in its province and at its base,
	/// put under the old one's commander where he may take it; the old unit's first brigade moved into it by the original's
	/// tests, and a new unit left empty taken out again. A land unit and every land unit made lose their leaders.
	/// </summary>
	public static void Split(CGameState state, CCountry country, CUnit unit)
	{
		int made = unit.SubUnits.Count - 1;
		var units = new System.Collections.Generic.List<CUnit>();
		for (int i = 0; i < made; i++)
		{
			var created = new CUnit(unit.Kind, country.Tag) { Id = ++state.LastUnitId, Location = unit.Location, Base = unit.Base };
			country.Units.Add(created);
			if (unit.Parent is { } parent && CDeployment.MayTakeSubordinate(state, parent, created))
			{
				CAgentUnits.Attach(state, created, parent);
			}
			// Each takes the first brigade the old unit holds, so a first brigade that may not go keeps every one back.
			CSubUnit first = unit.SubUnits[0];
			if (CSubUnitTransfer.MayTake(state, country, created, first, sameProvince: true))
			{
				CSubUnitTransfer.Move(state, country, first, created);
				CCombats.CheckForCombat(state, created, created.Location);
				units.Add(created);
			}
			else
			{
				// Ours: the original leaves it empty for its hourly pass to take away.
				CCombatRetreat.RemoveFromGame(state, created);
			}
		}
		if (CMovementRules.IsLandUnit(unit))
		{
			CLeaders.SetLeader(state, unit, null, false, false);
			foreach (CUnit created in units)
			{
				CLeaders.SetLeader(state, created, null, false, false);
			}
		}
		state.ForgetPositions();
	}
}
