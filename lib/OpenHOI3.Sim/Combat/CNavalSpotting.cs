using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Whether one fleet sees another well enough to start a battle with it: the tests that
/// say no outright, the ones that say yes, and otherwise a roll for each pair of ships.
/// A fleet on a mission asks the same question of fleets it is only looking for. See
/// <c>docs/mechanics/navalcombat.md</c>, section 1, and
/// <c>docs/mechanics/navalmissions.md</c>, section 9.
/// </summary>
public static class CNavalSpotting
{
	public const string SurfaceDetection = "surface_detection";
	public const string SubDetection = "sub_detection";
	public const string Visibility = "visibility";
	public const string SpottingRainModifier = "SPOTTINGRAINMODIFIER";
	public const string SpottingCloudModifier = "SPOTTINGCLOUDMODIFIER";
	public const string AfterAttackFactor = "NAVAL_INTERCEPTION_AFTER_ATTACK_FACTOR";

	/// <summary>What a fleet's roll is out of.</summary>
	private const int FleetRoll = 10000;

	/// <summary>
	/// What an air unit's roll is out of: a tenth of a fleet's, so it sees ten times as readily
	/// (reversing/findings/FINDINGS-liveread.md, section 109).
	/// </summary>
	private const int AirRoll = 1000;

	/// <summary>
	/// Whether a unit starts a battle or a bombing with one it has found, by its own order: on
	/// patrol always when aggressive, never when passive, and otherwise only against a fleet
	/// weaker than itself; raiding or carrying troops never (<c>transport.md</c>, section 5);
	/// anything else, a bomber's mission among them, yes. The found unit's order asks nothing,
	/// so a raider or a transport may be found and fought (FINDINGS-liveread.md, section 126).
	/// </summary>
	public static bool MayStartAgainst(CUnit asker, CUnit found)
	{
		switch (asker.Order)
		{
			case EUnitOrder.Transport:
				return false;
			case EUnitOrder.Patrol:
				return asker.Mission.Stance switch
				{
					CMission.Aggressive => true,
					CMission.Passive => false,
					_ => CEnemyFleets.Power(found) < CEnemyFleets.Power(asker),
				};
			case EUnitOrder.ConvoyRaid:
				return false;
			default:
				return true;
		}
	}

	/// <summary>Whether any land unit aboard a fleet is on the move or fighting.</summary>
	private static bool HasBusyPassenger(CUnit fleet)
	{
		foreach (CUnit passenger in fleet.Passengers)
		{
			if (passenger.IsMoving || passenger.IsFighting)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether <paramref name="ours"/> starts a battle with <paramref name="theirs"/> this hour.</summary>
	public static bool ShouldStart(CGameState state, CUnit ours, CUnit theirs)
	{
		if (theirs.SubUnits.Count < 1 || ours.EngageWait > 0)
		{
			return false;
		}
		// A raider is after convoys and starts no battle between fleets.
		if (ours.Order == EUnitOrder.ConvoyRaid)
		{
			return false;
		}
		// A fleet nobody plays that has been told nothing, or is carrying troops, waits to be found.
		if (!state.Played.Contains(ours.Owner)
			&& (ours.Order == EUnitOrder.None || ours.Order == EUnitOrder.Rebase || ours.Order == EUnitOrder.Transport
				|| ours.Order == EUnitOrder.Invasion))
		{
			return false;
		}
		CShipCensus mine = CShipCensus.Of(ours);
		CShipCensus other = CShipCensus.Of(theirs);
		if (mine.Submarines > 0 && other.Screens > mine.Submarines / 3)
		{
			return false;
		}
		// A patrol that is not aggressive leaves alone a fleet with much more in heavy ships.
		if (ours.Order == EUnitOrder.Patrol && ours.Mission.Stance != CMission.Aggressive
			&& 5 * (mine.Carriers + mine.Capitals) < 3 * (other.Carriers + other.Capitals))
		{
			return false;
		}

		CProvince province = state.Province(ours.Location);
		int level = CIntelligence.Level(state, province, ours.Owner);
		// A fleet in a battle or under bombs (FINDINGS-liveread.md, section 125), or one whose passengers are landing or
		// fighting, is found without a roll - and by a played country's fleet, every fleet with the fog of war off (ours: the
		// original's finds every fleet so, the AI's too - docs/compatibility.md, entry 21).
		bool noFog = !state.FogOfWar && state.Played.Contains(ours.Owner);
		if (level >= Intelligence.CProvinceIntel.Most || theirs.IsFighting || theirs.Bombing is not null || noFog
			|| HasBusyPassenger(theirs))
		{
			return true;
		}

		// An escort or a patrol looking for a raider: the one's skill at its mission against the other's.
		long efficiency = CFixedPoint.Scale;
		if ((ours.Order == EUnitOrder.ConvoyEscort || ours.Order == EUnitOrder.Patrol)
			&& theirs.Order == EUnitOrder.ConvoyRaid)
		{
			efficiency += CMissionEfficiency.Of(state, state.Country(ours.Owner), ours.Order)
				- CMissionEfficiency.Of(state, state.Country(theirs.Owner), theirs.Order);
		}
		// Water a convoy was attacked in today or yesterday is watched the harder.
		long afterAttack = CFixedPoint.Scale;
		long attacked = state.Province(theirs.Location).LastConvoyAttackHour;
		if (attacked != CProvince.NoConvoyAttack
			&& state.TotalHours / CGameState.HoursPerDay - attacked / CGameState.HoursPerDay <= 1
			&& state.Defines.Has(CManpower.MilitarySection, AfterAttackFactor))
		{
			afterAttack = state.Defines[CManpower.MilitarySection, AfterAttackFactor].Raw;
		}

		long spotting = CFixedPoint.Scale + CCommandReach.Effect(state, ours, ETraitEffect.SpottingChance);
		long underWay = theirs.IsMoving ? 3000 : 2000;
		long weather = CFixedPoint.Scale
			- province.Weather.Precipitation.Raw * state.Defines[CWeatherEffects.WeatherSection, SpottingRainModifier].Raw / CFixedPoint.Scale
			- province.Weather.CloudCoverage.Raw * state.Defines[CWeatherEffects.WeatherSection, SpottingCloudModifier].Raw / CFixedPoint.Scale;
		// An air unit looks with its surface detection whatever it looks at, and rolls out of a tenth as much as a fleet.
		bool fromTheAir = ours.Kind == EUnitKind.Air;
		int roll = fromTheAir ? AirRoll : FleetRoll;
		foreach (CSubUnit their in theirs.SubUnits)
		{
			long visibility = System.Math.Max(CFixedPoint.Scale, their.Stats[Visibility].Raw);
			bool isSubmarine = CShipCensus.IsSubmarine(their);
			foreach (CSubUnit our in ours.SubUnits)
			{
				long seen = !fromTheAir && isSubmarine ? our.Stats[SubDetection].Raw / 2 : our.Stats[SurfaceDetection].Raw;
				long figure = seen * visibility / CFixedPoint.Scale;
				figure = figure * spotting / CFixedPoint.Scale;
				figure = figure * efficiency / CFixedPoint.Scale;
				figure = figure * afterAttack / CFixedPoint.Scale;
				figure = figure * CFixedPoint.Scale / underWay;
				if (theirs.IsShadowed)
				{
					figure *= 2;
				}
				if (level >= 2)
				{
					figure = figure * 1250 / CFixedPoint.Scale;
				}
				for (int step = 6; step <= 8; step++)
				{
					if (level >= step)
					{
						figure *= 2;
					}
				}
				figure = figure * weather / CFixedPoint.Scale;
				if (figure < CFixedPoint.Scale)
				{
					figure = CFixedPoint.Scale;
				}
				if (state.Random.Next(roll) < figure / CFixedPoint.Scale)
				{
					return true;
				}
			}
		}
		return false;
	}
}
