using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// Air superiority: a unit goes round the provinces of its mission and flies at the
/// strongest enemy air unit it knows of in them - to where that one is going, one
/// province ahead of it. It picks only at its base or on top of its target. Written in
/// the original's order, the hour running on past a "return to base" where the
/// original's does. See <c>docs/mechanics/aircombat.md</c>, section 3.
/// </summary>
public static class CAirSuperiority
{
	/// <summary>The mission's own hour, before the frame every air order shares.</summary>
	public static void RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (mission.ReturnToBase)
		{
			return;
		}
		if (!CAirMissions.IsFitToFly(state, unit))
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (unit.IsFighting || unit.IsMoving)
		{
			return;
		}
		if (unit.FuelReceived == CFixedPoint.Zero)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		if (state.TotalHours > mission.EndHour)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (!CAirMissions.DayNightGate(state, country, unit))
		{
			return;
		}

		if (mission.LiveTarget is not { } held)
		{
			mission.Target = null;
			if (unit.Location == unit.Base && CAirMissions.IsFitToFly(state, unit)
				&& PickTarget(state, country, unit) is { } found)
			{
				mission.Target = found;
			}
		}
		else if (!mission.Provinces.Contains(held.Location))
		{
			mission.Target = null;
		}

		// With nothing to go for, or having arrived, the next province of the round.
		if ((mission.Destination == 0 && mission.LiveTarget is null)
			|| (mission.Destination != 0 && mission.Destination == unit.Location))
		{
			mission.Destination = After(mission, mission.Destination);
		}

		if (mission.LiveTarget is { } here && here.Location == unit.Location && unit.Base != 0)
		{
			if (state.TotalHours > mission.EndHour)
			{
				CAirMissions.ReturnToBase(state, country, unit);
				return;
			}
			if (!CAirMissions.DayNightGate(state, country, unit))
			{
				return;
			}
			mission.Target = PickTarget(state, country, unit);
			mission.Destination = 0;
			return;
		}

		CUnit? target = mission.LiveTarget;
		int ahead = target is null ? 0 : CAirTargets.NextProvince(target);
		if (target is null || (mission.Destination != 0 && ahead == mission.Destination))
		{
			if (mission.Destination != 0 && CAirMissions.DayNightGate(state, country, unit))
			{
				CAirMissions.GoTo(state, country, unit, mission.Destination);
			}
			return;
		}
		if (!CAirMissions.DayNightGate(state, country, unit))
		{
			return;
		}
		int goal = ahead != 0 && ahead != target.Location ? ahead : target.Location;
		if (CAirMissions.GoTo(state, country, unit, goal))
		{
			mission.Destination = goal;
		}
	}

	/// <summary>The province after one in the mission's list: the first where there is none, and after the last.</summary>
	public static int After(CMission mission, int province)
	{
		if (mission.Provinces.Count == 0)
		{
			return province;
		}
		int at = province == 0 ? -1 : mission.Provinces.IndexOf(province);
		return at < 0 || at == mission.Provinces.Count - 1 ? mission.Provinces[0] : mission.Provinces[at + 1];
	}

	/// <summary>
	/// The enemy air unit to go for: over the mission's provinces the unit may fly to
	/// and its country knows, every air unit of a country at war with its own, the best
	/// scored and the first found among equals.
	/// </summary>
	public static CUnit? PickTarget(CGameState state, CCountry country, CUnit unit)
	{
		CUnit? best = null;
		long most = CAirTargets.Nothing;
		foreach (int id in unit.Mission.Provinces)
		{
			CProvince province = state.Province(id);
			if (!CMovementRules.MayStandIn(state, country, unit, province)
				|| CIntelligence.Level(state, province, unit.Owner) < CIntelligence.LeastToPick)
			{
				continue;
			}
			foreach (CUnit other in state.UnitsIn(id))
			{
				if (other.Kind != EUnitKind.Air || other.IsRemoved || other.Owner == unit.Owner
					|| !CMovementRules.IsEnemy(state, other.Owner, unit.Owner))
				{
					continue;
				}
				if (CAirTargets.Score(state, unit, other, needsWings: false) is { } score && score > most)
				{
					most = score;
					best = other;
				}
			}
		}
		return best;
	}
}
