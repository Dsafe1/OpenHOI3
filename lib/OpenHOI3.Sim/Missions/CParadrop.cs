using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The paradrop: an air unit flies the land units it carries to a land province and drops them there. Nothing measures
/// its range. See <c>docs/mechanics/transport.md</c>, section 13.4.
/// </summary>
public static class CParadrop
{
	public const string MinTime = "PARADROP_MIN_TIME";

	/// <summary>The message the player holding the target is told, once.</summary>
	public const string Message = "PARATROOPEDUS";

	/// <summary>A paradrop may be given for any land province.</summary>
	public static bool MayGive(CUnit unit, CProvince province)
	{
		return unit.Kind == EUnitKind.Air && !province.IsSea;
	}

	/// <summary>The earliest hour a paradrop given now may start: now and <c>PARADROP_MIN_TIME</c> hours.</summary>
	public static long EarliestStart(CGameState state)
	{
		return state.TotalHours + state.Defines[CManpower.MilitarySection, MinTime].Raw / CFixedPoint.Scale;
	}

	/// <summary>
	/// Taking off: with no fuel or unfit to fly, told to return to base; before its start hour nothing; waiting while
	/// a passenger's attack delay runs against an enemy's province, or a brigade is short of organisation; otherwise
	/// the way to the target.
	/// </summary>
	public static void TakeOff(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.FuelReceived == CFixedPoint.Zero || !CAirMissions.IsFitToFly(state, unit))
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		if (state.TotalHours < unit.Mission.StartHour)
		{
			return;
		}
		CProvince target = state.Province(unit.OrderProvince);
		bool hostile = CMovementRules.IsEnemy(state, target.Controller, unit.Owner);
		foreach (CUnit passenger in unit.Passengers)
		{
			if (hostile && passenger.AttackDelay > CFixedPoint.Zero)
			{
				return;
			}
		}
		foreach (CUnit passenger in unit.Passengers)
		{
			if (!CAirTransport.IsOrganisedForDrop(state, passenger))
			{
				return;
			}
		}
		CAirMissions.FlyTo(state, country, unit, target, judgeWithoutWay: false);
	}

	/// <summary>
	/// The hour: no base, done; in a battle, a bombing or under way, going on; home with nothing aboard, done; before
	/// the start hour, going on; elsewhere than the target, take off again; over it, the drop.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting || unit.Bombing is not null || unit.IsMoving)
		{
			return true;
		}
		if (CAirMissions.IsAtOwnBase(unit) && unit.Passengers.Count == 0)
		{
			return false;
		}
		if (state.TotalHours < unit.Mission.StartHour)
		{
			return true;
		}
		if (unit.Location != unit.OrderProvince)
		{
			TakeOff(state, country, unit);
			return true;
		}
		CAirTransport.Drop(state, unit);
		CAirTransport.SpendPlanes(state, unit);
		CAirMissions.ReturnToBase(state, country, unit);
		Tell(state, unit);
		return true;
	}

	/// <summary>
	/// <c>PARATROOPEDUS</c>, the first time only: to the player holding the target, where the air unit's country is at
	/// war with the player.
	/// </summary>
	private static void Tell(CGameState state, CUnit unit)
	{
		if (unit.Mission.Told)
		{
			return;
		}
		unit.Mission.Told = true;
		CProvince target = state.Province(unit.OrderProvince);
		if (target.Controller is not { } player || !state.Played.Contains(player)
			|| !CMovementRules.IsEnemy(state, unit.Owner, player))
		{
			return;
		}
		state.RaisePactMessage(new CPactMessage(Message, "", "", unit.Owner, player)
		{
			Told = player,
			Province = target.Id,
			Messenger = "MESSAGE_HEAD_MARS",
			Values = new[] { ("ENEMY", state.Country(unit.Owner)?.Name ?? unit.Owner.Value), ("PROV", target.Name) },
		});
	}
}
