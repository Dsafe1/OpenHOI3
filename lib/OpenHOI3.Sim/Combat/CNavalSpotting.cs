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
	/// Whether a fleet found in a province may be made the defender of a battle, by its
	/// order: one on patrol always when it is aggressive, never when passive, and
	/// otherwise only by a fleet weaker than itself; a raider never; anything else, yes.
	/// </summary>
	public static bool DefenderAccepts(CUnit found, CUnit asker)
	{
		switch (found.Order)
		{
			case EUnitOrder.Patrol:
				return found.Mission.Stance switch
				{
					CMission.Aggressive => true,
					CMission.Passive => false,
					_ => CEnemyFleets.Power(asker) < CEnemyFleets.Power(found),
				};
			case EUnitOrder.ConvoyRaid:
				return false;
			default:
				return true;
		}
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
		// A fleet nobody plays that has been told nothing waits to be found.
		if (!state.Played.Contains(ours.Owner) && (ours.Order == EUnitOrder.None || ours.Order == EUnitOrder.Rebase))
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
		int level = CNavalIntelligence.Level(state, province, ours);
		if (level >= CNavalIntelligence.LevelOfOwnGround || theirs.IsFighting)
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
		foreach (CSubUnit their in theirs.SubUnits)
		{
			long visibility = System.Math.Max(CFixedPoint.Scale, their.Stats[Visibility].Raw);
			bool isSubmarine = CShipCensus.IsSubmarine(their);
			foreach (CSubUnit our in ours.SubUnits)
			{
				long seen = isSubmarine ? our.Stats[SubDetection].Raw / 2 : our.Stats[SurfaceDetection].Raw;
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
				if (state.Random.Next(FleetRoll) < figure / CFixedPoint.Scale)
				{
					return true;
				}
			}
		}
		return false;
	}
}
