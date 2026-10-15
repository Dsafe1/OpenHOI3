using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// A province's season, 1 to 5, by the day of the year and how far north it is: the original's
/// <c>ComputeProvinceClimate</c>, made again each day. See <c>docs/mechanics/weather.md</c>, section 1.
/// </summary>
public static class CClimate
{
	/// <summary>The season of a province with no latitude worth a season - the whole south, and the tropics.</summary>
	public const int Default = 3;

	private const long NoSeasonBelow = 250;
	private const long LatitudeSpan = 750;
	private const int HalfYear = 183;
	private const int Shoulder = 91;

	/// <summary>Bands A to F, north of the equator: late winter, spring, summer, autumn, late autumn, winter.</summary>
	private static readonly int[] North = { 1, 2, 3, 4, 4, 5 };

	/// <summary>The same half a year on, for the south - which, having no latitude, never reaches them.</summary>
	private static readonly int[] South = { 4, 4, 5, 1, 2, 3 };

	/// <summary>
	/// Winter's width L grows from nought at a latitude of 250 to 183 days at the top of the map, and summer takes the
	/// rest of the half year; four shoulders of 45, 46, 45 and 46 days lie between them, winter centred on 1 January.
	/// </summary>
	public static int Of(CGameState state, CProvince province)
	{
		long latitude = CDaylight.LatitudeNorth(state, province);
		if (latitude <= NoSeasonBelow)
		{
			return Default;
		}
		long a = (latitude - NoSeasonBelow) * CFixedPoint.Scale / LatitudeSpan;
		int winter = (int)(a * (HalfYear * CFixedPoint.Scale) / CFixedPoint.Scale / CFixedPoint.Scale);
		int summer = HalfYear - winter;
		int day = state.Date.DayNumber % CDate.DaysPerYear - winter / 2;
		int[] bands = province.IsNorthern ? North : South;

		int band = day <= 0 ? 5
			: day <= Shoulder / 2 ? 0
			: day <= Shoulder ? 1
			: day <= Shoulder + summer ? 2
			: day <= Shoulder + summer + Shoulder / 2 ? 3
			: day <= 2 * Shoulder + summer ? 4
			: 5;
		return bands[band];
	}

	/// <summary>Every province's season made again, as the original's day does first for each province.</summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id > 0)
			{
				province.Climate = Of(state, province);
			}
		}
	}
}
