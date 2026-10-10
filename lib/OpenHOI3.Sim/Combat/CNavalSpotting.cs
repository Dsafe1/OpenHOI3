using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Whether one fleet sees another well enough to start a battle with it: the tests that
/// say no outright, the ones that say yes, and otherwise a roll for each pair of ships.
/// See <c>docs/mechanics/navalcombat.md</c>, section 1.
/// </summary>
public static class CNavalSpotting
{
	public const string SurfaceDetection = "surface_detection";
	public const string SubDetection = "sub_detection";
	public const string Visibility = "visibility";
	public const string SpottingRainModifier = "SPOTTINGRAINMODIFIER";
	public const string SpottingCloudModifier = "SPOTTINGCLOUDMODIFIER";

	/// <summary>How well a country knows a sea province it has a unit in.</summary>
	public const int LevelWithAUnitThere = 2;

	/// <summary>The level at which a fleet is seen with no roll.</summary>
	public const int LevelOfOwnGround = 9;

	/// <summary>What a fleet's roll is out of.</summary>
	private const int FleetRoll = 10000;

	/// <summary>
	/// How well a country knows a sea province. The original keeps a level for every
	/// country in every province, raised by units, patrols and radar; what is built is
	/// the least of it, that a fleet knows the water it is in.
	/// </summary>
	public static int IntelligenceLevel(CGameState state, CProvince province, CUnit fleet)
	{
		return fleet.Location == province.Id ? LevelWithAUnitThere : 0;
	}

	/// <summary>Whether <paramref name="ours"/> starts a battle with <paramref name="theirs"/> this hour.</summary>
	public static bool ShouldStart(CGameState state, CUnit ours, CUnit theirs)
	{
		if (theirs.SubUnits.Count < 1 || ours.EngageWait > 0)
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

		CProvince province = state.Province(ours.Location);
		int level = IntelligenceLevel(state, province, ours);
		if (level >= LevelOfOwnGround || theirs.IsFighting)
		{
			return true;
		}

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
				figure = figure * CFixedPoint.Scale / underWay;
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
