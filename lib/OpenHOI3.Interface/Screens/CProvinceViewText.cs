using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the province view writes of one province as it is selected, and what its weather strip shows every frame. See
/// <c>docs/mechanics/provinceview.md</c>, sections 2 and 3.
/// </summary>
public static class CProvinceViewText
{
	/// <summary>What the player must know of a province for its figures to be shown: fixed.</summary>
	public const int FullIntel = 9;

	/// <summary>The wind the bar is full at, in whole kilometres an hour: fixed.</summary>
	public const int MostWind = 30;

	/// <summary>The temperature under which precipitation is snow, in degrees: fixed.</summary>
	public const int SnowBelow = -5;

	private const string WeatherSection = "weather";

	/// <summary>The four goods whose figures the view shows, with their figure's element and their icon's.</summary>
	public static readonly (EGoods Goods, string Text, string Icon, string Name)[] Resources =
	{
		(EGoods.CrudeOil, "text_oil", "oil", "CRUDE_OIL_IRO"),
		(EGoods.Metal, "text_metal", "metal", "METAL_IRO"),
		(EGoods.Energy, "text_energy", "energy", "ENERGY_IRO"),
		(EGoods.RareMaterials, "text_raremat", "rare", "RAREMAT_IRO"),
	};

	/// <summary><c>text_title</c> with several provinces selected: <c>PROV_VIEW_MULTI</c>, NUM how many.</summary>
	public static string MultiTitle(Func<string, string> localise, int count)
	{
		return CTextTemplate.Fill(localise("PROV_VIEW_MULTI"), ("NUM", count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
	}

	/// <summary><c>terrain</c>'s sprite: <c>GFX_terrainimg_</c> and the terrain's key, or none.</summary>
	public static string TerrainSprite(CProvince province)
	{
		return province.Terrain is { } terrain ? "GFX_terrainimg_" + terrain.Name : "";
	}

	/// <summary>Where a province's own picture would be, asked before the terrain's.</summary>
	public static string PicturePath(CProvince province)
	{
		return "gfx/interface/prov_pictures/provimg_" + province.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".dds";
	}

	/// <summary><c>text_espionage</c>: what the player knows of the province, a bare number.</summary>
	public static string Espionage(CGameState state, CProvince province, CCountryTag player)
	{
		return CProvinceIntel.Level(state, province, player).ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary><c>text_manpower</c>: "?" below full intelligence, "-" with no manpower, else the year's figure.</summary>
	public static string Manpower(CGameState state, CProvince province, CCountryTag player)
	{
		if (CProvinceIntel.Level(state, province, player) < FullIntel)
		{
			return "?";
		}
		return province.Manpower == CFixedPoint.Zero ? "-" : "§Y" + CNumberText.Format(CManpower.OfProvince(state, province), 2);
	}

	/// <summary><c>text_leadership</c>: "?" below full intelligence, "-" with no leadership, else its figure.</summary>
	public static string Leadership(CGameState state, CProvince province, CCountryTag player)
	{
		if (CProvinceIntel.Level(state, province, player) < FullIntel)
		{
			return "?";
		}
		return province.Leadership == CFixedPoint.Zero ? "-" : "§Y" + CNumberText.Format(CLeadership.OfProvince(province), 2);
	}

	/// <summary>
	/// A resource's figure: "§W0" where the province produces none at most; "§Y?§W" below full intelligence; else red where
	/// it produces less than its most and green where not, and the day's yield to its holder to one place, rounded.
	/// </summary>
	public static string Resource(CGameState state, CProvince province, CCountryTag player, EGoods goods)
	{
		if (province.MaxProducing[goods] <= CFixedPoint.Zero)
		{
			return "§W0";
		}
		if (CProvinceIntel.Level(state, province, player) < FullIntel)
		{
			return "§Y?§W";
		}
		string colour = province.CurrentProducing[goods] < province.MaxProducing[goods] ? "§R" : "§G";
		CFixedPoint yield = state.Country(province.Controller) is { } holder
			? CResources.Yield(holder, province, goods)
			: province.CurrentProducing[goods];
		return colour + Rounded(yield);
	}

	/// <summary><c>strategic_resource</c>'s sprite, or none.</summary>
	public static string StrategicSprite(CProvince province)
	{
		return province.StrategicResource.Length == 0 ? "" : "GFX_" + province.StrategicResource + "_small";
	}

	/// <summary>An <c>adj</c> row's icon, by the way's kind and whether the neighbour is sea.</summary>
	public static string WaySprite(CGameState state, CProvinceEdge edge)
	{
		return edge.Kind switch
		{
			EProvinceEdgeKind.Crossing => "GFX_provinceview_strait",
			EProvinceEdgeKind.River => "GFX_provinceview_river",
			EProvinceEdgeKind.Impassable => "GFX_provinceview_impassable",
			_ => state.Province(edge.To).IsSea ? "GFX_provinceview_ocean" : "GFX_provinceview_passable",
		};
	}

	/// <summary>The countries whose flags the <c>claims</c> box shows: the province's cores, in their order.</summary>
	public static IReadOnlyList<CCountryTag> Claims(CProvince province) => province.Cores;

	/// <summary><c>weather_icon</c>'s frame: 7 a snowstorm, 6 snow, 5 a storm, 4 rain, 3 overcast, 2 partly cloudy, 1 clear.</summary>
	public static int WeatherFrame(CGameState state, CWeather weather)
	{
		CFixedPoint Limit(string name) => state.Defines[WeatherSection, name];
		if (weather.Precipitation > Limit("GFX_SNOW_LIMIT") && weather.Temperature < CFixedPoint.FromInt(SnowBelow))
		{
			return weather.WindSpeed > Limit("GFX_SNOW_STORM_LIMIT") ? 7 : 6;
		}
		if (weather.Precipitation > Limit("GFX_RAIN_LIMIT"))
		{
			return weather.WindSpeed > Limit("GFX_STORM_LIMIT") ? 5 : 4;
		}
		if (weather.CloudCoverage > Limit("GFX_CLOUD_LIMIT"))
		{
			return 3;
		}
		return weather.CloudCoverage > Limit("GFX_PARTIAL_CLOUD_LIMIT") ? 2 : 1;
	}

	/// <summary>The weather picture's key, by its frame, for <c>weather_icon</c>'s tooltip.</summary>
	public static string WeatherKey(int frame) => frame switch
	{
		7 => "WEATHER_SNOW_STORM",
		6 => "WEATHER_SNOW",
		5 => "WEATHER_STORM",
		4 => "WEATHER_RAIN",
		3 => "WEATHER_CLOUD",
		2 => "WEATHER_PARTIAL_CLOUD",
		_ => "WEATHER_CLEAR_SKY",
	};

	/// <summary><c>ground_status_icon</c>'s frame: 2 frozen, 3 mud, 1 dry.</summary>
	public static int GroundFrame(CWeather weather)
	{
		return weather.Frozen > CFixedPoint.Zero ? 2 : weather.Muddyness > CFixedPoint.Zero ? 3 : 1;
	}

	/// <summary>The ground's key, for its tooltip.</summary>
	public static string GroundKey(CWeather weather)
	{
		return weather.Frozen > CFixedPoint.Zero ? "WEATHER_FROZEN" : weather.Muddyness > CFixedPoint.Zero ? "WEATHER_MUDDY" : "WEATHER_DRY";
	}

	/// <summary><c>day_night</c>'s frame: 2 at night, 1 by day.</summary>
	public static int DayNightFrame(CGameState state, CProvince province)
	{
		return CDaylight.IsNight(state, province) ? 2 : 1;
	}

	/// <summary><c>wind_status</c>'s bar in whole percent: the wind against <see cref="MostWind"/>, cut.</summary>
	public static int WindPercent(CWeather weather)
	{
		long wind = Math.Min(weather.WindSpeed.Raw, MostWind * CFixedPoint.Scale);
		return (int)(wind * 10 / 3 / CFixedPoint.Scale);
	}

	/// <summary><c>temperature_txt</c>: green and a plus above nought, yellow at nought, red below; to one place, and "C".</summary>
	public static string Temperature(CWeather weather)
	{
		string colour = weather.Temperature > CFixedPoint.Zero ? "§G+" : weather.Temperature == CFixedPoint.Zero ? "§Y" : "§R";
		return colour + CNumberText.Format(weather.Temperature, 1) + "C";
	}

	/// <summary>A figure to one place, rounded, as <c>%.1f</c> writes it.</summary>
	public static string Rounded(CFixedPoint value)
	{
		return (value.Raw / (double)CFixedPoint.Scale).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
	}
}
