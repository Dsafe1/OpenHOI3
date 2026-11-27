using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// Whether it is night in a province. The clock is the game's, which is the same hour
/// everywhere; a province's own time of day comes of how far east it is, and the length
/// of its day of how far north and what month it is. See
/// <c>docs/mechanics/landcombat.md</c>, section 4.
/// </summary>
public static class CDaylight
{
	private const int HoursInADay = 24;
	private const int Noon = 12;
	private const int Sunrise = 6;
	private const int Sunset = 18;

	/// <summary>
	/// How many hours longer than twelve the day is at the far north, by month, January
	/// first, in thousandths: the longest in June and the shortest in December.
	/// </summary>
	private static readonly long[] LongerByMonth =
	{
		-6000, -3000, 0, 3000, 6000, 12000, 6000, 3000, 0, -3000, -6000, -12000,
	};

	/// <summary>
	/// How far north of the equator a province is, in thousandths of the way to the
	/// map's northern edge, by the middle of its bounding box. Nothing south of the
	/// equator is anything but nought: the original cuts it so.
	/// </summary>
	public static long LatitudeNorth(CGameState state, CProvince province)
	{
		int half = state.MapHeight / 2;
		if (half <= 0)
		{
			return 0;
		}
		long north = province.CentreY - half;
		return north <= 0 ? 0 : north * CFixedPoint.Scale / half;
	}

	/// <summary>
	/// The latitude a season, a temperature and a day's length go by: the original's, which is nought everywhere south
	/// of the map's middle - and under an enhanced game's fix <c>southern_seasons</c> how far from the middle a province
	/// is either way, in thousandths of the way to the map's edge, so that the south has seasons, the cold of its high
	/// latitudes and days that lengthen and shorten (<c>docs/mechanics/weather.md</c>, section 1).
	/// </summary>
	public static long Latitude(CGameState state, CProvince province)
	{
		if (!state.Rules.Has(Rules.EFix.SouthernSeasons))
		{
			return LatitudeNorth(state, province);
		}
		int half = state.MapHeight / 2;
		if (half <= 0)
		{
			return 0;
		}
		return System.Math.Abs((long)province.CentreY - half) * CFixedPoint.Scale / half;
	}

	public static bool IsNight(CGameState state, CProvince province)
	{
		return IsNight(state, province, state.Hour, state.Date.Month - 1);
	}

	/// <summary>
	/// The province's own hour is the game's, moved on by a twenty-fourth of a day for
	/// each twenty-fourth of the map it is east, and by twelve. Its day runs from six to
	/// eighteen, longer or shorter at both ends by half of what the month gives its
	/// latitude; south of the equator the months are six on.
	/// </summary>
	public static bool IsNight(CGameState state, CProvince province, int hour, int month)
	{
		if (state.MapWidth <= 0)
		{
			return false;
		}
		int local = (province.PositionX * HoursInADay / state.MapWidth + Noon + hour) % HoursInADay;
		bool isNorth = province.PositionY >= state.MapHeight / 2;
		if (!isNorth)
		{
			month = (month + 6) % 12;
		}
		long longer = Latitude(state, province) * LongerByMonth[month] / CFixedPoint.Scale;
		long half = longer * CFixedPoint.Scale / (2 * CFixedPoint.Scale) / CFixedPoint.Scale;
		return !(Sunrise - half <= local && local <= Sunset + half);
	}

	/// <summary>
	/// The hour the AI takes for sunset over a province: twelve and q, where q is half of twelve hours and what the month
	/// gives its latitude, in whole hours - 18 at the equator, 24 under the midnight sun. No longitude: the AI sets it
	/// against the game's own hour (<c>docs/mechanics/moving.md</c>, section 17).
	/// </summary>
	public static int SunsetHour(CGameState state, CProvince province)
	{
		int month = state.Date.Month - 1;
		if (province.PositionY < state.MapHeight / 2)
		{
			month = (month + 6) % 12;
		}
		long q = (Latitude(state, province) * LongerByMonth[month] / CFixedPoint.Scale + 12000) / 2000;
		return Noon + (int)q;
	}
}
