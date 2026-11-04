using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Weather made against <c>docs/mechanics/weather.md</c>: the season, a province's hour, a front's move and spread, the
/// high pressure's walk and a front's birth, on a row of land provinces west to east on a map 1000 by 1000, with Their
/// Finest Hour's defines.
/// </summary>
public class WeatherTests
{
	private static readonly CTerrain Jungle = new("jungle", false, CColor.Gray)
	{
		Humidity = CFixedPoint.Parse("0.1"), Temperature = CFixedPoint.FromInt(3),
	};

	private static readonly CTerrain Plains = new("plains", false, CColor.Gray);

	private static CGameState World(CDate? date = null)
	{
		CProvince Land(int id, int centreY = 500)
		{
			var province = new CProvince(id, $"p{id}", isSea: false)
			{
				Terrain = Plains, CentreX = id * 100, CentreY = centreY, PositionX = id * 100, PositionY = centreY,
				IsNorthern = centreY >= 500,
			};
			var edges = new System.Collections.Generic.List<CProvinceEdge>();
			if (id > 1)
			{
				edges.Add(new CProvinceEdge(id - 1, EProvinceEdgeKind.Plain, 0, CFixedPoint.One) { Bearing = 1000 });
			}
			if (id < 5)
			{
				edges.Add(new CProvinceEdge(id + 1, EProvinceEdgeKind.Plain, 0, CFixedPoint.One) { Bearing = 500 });
			}
			province.Edges = edges.ToArray();
			return province;
		}
		var state = new CGameState(date ?? new CDate(1936, 1, 8),
			new[] { new CProvince(0, "", isSea: false), Land(1), Land(2), Land(3), Land(4), Land(5) },
			System.Array.Empty<OpenHOI3.Sim.Countries.CCountry>())
		{
			MapWidth = 1000, MapHeight = 1000,
		};
		foreach ((string name, string value) in new[]
		{
			("PRESSUREMIN", "870"), ("PRESSUREMAX", "1090"), ("PRESSUREDEFAULT", "1013"), ("PRESSURESTEP", "5"),
			("PRESSUREREDUCTION", "2"), ("PRESSUREPROPAGATION", "10"), ("PRESSURETHRESHOLD", "300"), ("MAXHUMIDITY", "100"),
			("MINHUMIDITY", "0"), ("MAXFROMEACHPRESSURE", "8"), ("CLOUDCOVERAGETEMPERATUREDROP", "8"),
			("HOURLYFROZENINCREASE", "0.01"), ("HOURLYTHAW", "0.01"), ("TEMPERATURECHANGESPEED", "0.1"),
			("WEATHERMOVEMENTDELAY", "2"), ("LOWPRESSUREBASE", "100"), ("LOWPRESSUREOFFSET", "100"),
		})
		{
			state.Defines.Set(CProvinceWeather.Section, name, CFixedPoint.Parse(value));
		}
		foreach (CProvince province in state.Provinces.Skip(1))
		{
			province.Weather.Pressure = CFixedPoint.FromInt(1013);
		}
		return state;
	}

	[Fact]
	public void The_season_is_winter_on_1_January_and_summer_in_July_in_the_north_and_always_3_in_the_south()
	{
		CGameState january = World(new CDate(1936, 1, 1));
		var north = new CProvince(9, "north", isSea: false) { CentreY = 825, IsNorthern = true };   // latitude 650
		var tropics = new CProvince(9, "tropics", isSea: false) { CentreY = 600, IsNorthern = true }; // latitude 200
		var south = new CProvince(9, "south", isSea: false) { CentreY = 100, IsNorthern = false };
		Assert.Equal(5, CClimate.Of(january, north));
		Assert.Equal(3, CClimate.Of(january, tropics));
		Assert.Equal(3, CClimate.Of(january, south));

		// Winter is 97 days wide at 650 and runs from 1 January, day 0, to day 48, 18 February; late winter's
		// 45 days and spring's 46 follow.
		Assert.Equal(5, CClimate.Of(World(new CDate(1936, 2, 18)), north));
		Assert.Equal(1, CClimate.Of(World(new CDate(1936, 2, 19)), north));
		Assert.Equal(2, CClimate.Of(World(new CDate(1936, 5, 1)), north));
		Assert.Equal(3, CClimate.Of(World(new CDate(1936, 7, 3)), north));
		Assert.Equal(4, CClimate.Of(World(new CDate(1936, 10, 1)), north));
		Assert.Equal(5, CClimate.Of(World(new CDate(1936, 12, 31)), north));
	}

	[Fact]
	public void A_province_hour_is_the_original_s_Phongsali_on_8_January()
	{
		// The maintainer's saves of 02:00 and 03:00 on 8 January 1936: a jungle province in season 3 by day, no front.
		CGameState state = World();
		CProvince province = state.Province(1);
		province.Terrain = Jungle;
		province.BaseTemperature = CFixedPoint.Parse("22.35");
		province.Climate = 3;
		province.Weather.Humidity = CFixedPoint.Parse("44.753");
		province.Weather.Pressure = CFixedPoint.Parse("1016.688");
		province.Weather.Temperature = CFixedPoint.Parse("29.065");
		province.Weather.Muddyness = CFixedPoint.Parse("0.524");

		CProvinceWeather.Update(state, province);

		Assert.Equal(CFixedPoint.Parse("44.861"), province.Weather.Humidity);
		Assert.Equal(CFixedPoint.Parse("1016.688"), province.Weather.Pressure);
		Assert.Equal(CFixedPoint.Parse("27.065"), province.Weather.Temperature);
		Assert.Equal(CFixedPoint.Parse("0.517"), province.Weather.Muddyness);
		Assert.Equal(CFixedPoint.Zero, province.Weather.CloudCoverage);
		Assert.Equal(CFixedPoint.Zero, province.Weather.Precipitation);
	}

	[Fact]
	public void Without_a_front_the_wind_dies_by_a_quarter_and_rain_burns_off_cloud_and_humidity()
	{
		CGameState state = World();
		CProvince province = state.Province(2);
		province.Climate = 3;
		province.BaseTemperature = CFixedPoint.FromInt(10);
		province.Weather.WindSpeed = CFixedPoint.FromInt(20);
		province.Weather.Humidity = CFixedPoint.FromInt(50);
		province.Weather.Temperature = CFixedPoint.FromInt(-2);
		province.Weather.CloudCoverage = CFixedPoint.Parse("0.3");

		CProvinceWeather.Update(state, province);

		Assert.Equal(CFixedPoint.FromInt(15), province.Weather.WindSpeed);
		// Cloud 0.5 (+0.2) rains 1.0 on land with no front, which burns 0.2 of cloud and 10 of humidity.
		Assert.Equal(CFixedPoint.One, province.Weather.Precipitation);
		Assert.Equal(CFixedPoint.Parse("0.3"), province.Weather.CloudCoverage);
		Assert.Equal(CFixedPoint.FromInt(40), province.Weather.Humidity);
	}

	[Fact]
	public void Below_minus_5_snow_freezes_the_ground_but_between_minus_5_and_0_rain_neither_freezes_nor_thaws()
	{
		CGameState state = World();
		CProvince cold = state.Province(1);
		CProvince mild = state.Province(2);
		foreach ((CProvince province, int temperature) in new[] { (cold, -8), (mild, -3) })
		{
			province.Climate = 3;
			province.BaseTemperature = CFixedPoint.FromInt(temperature);
			province.Weather.Temperature = CFixedPoint.FromInt(temperature);
			province.Weather.CloudCoverage = CFixedPoint.Parse("0.5");
			province.Weather.Humidity = CFixedPoint.FromInt(100);
			province.Weather.Frozen = CFixedPoint.Parse("0.1");
		}
		CProvinceWeather.Update(state, cold);
		CProvinceWeather.Update(state, mild);
		Assert.Equal(CFixedPoint.Parse("0.11"), cold.Weather.Frozen);
		Assert.Equal(CFixedPoint.Parse("0.1"), mild.Weather.Frozen);
	}

	[Fact]
	public void The_arc_is_taken_by_C_s_remainder_so_a_bearing_of_1000_wraps_and_one_of_0_does_not()
	{
		Assert.True(CWeatherFronts.IsWithinArc(600, 500, 200));
		Assert.False(CWeatherFronts.IsWithinArc(250, 500, 200));
		Assert.True(CWeatherFronts.IsWithinArc(900, 1000, 200));
		Assert.True(CWeatherFronts.IsWithinArc(100, 1000, 200));
		Assert.False(CWeatherFronts.IsWithinArc(900, 0, 200));
		Assert.True(CWeatherFronts.IsWithinArc(100, 0, 200));
		Assert.True(CWeatherFronts.IsWithinArc(1030, 1000, 200));
	}

	[Fact]
	public void A_front_steps_into_the_neighbours_in_its_arc_and_hands_them_its_weather()
	{
		CGameState state = World();
		CProvince home = state.Province(2);
		home.Weather.Pressure = CFixedPoint.FromInt(900);
		home.Weather.Humidity = CFixedPoint.FromInt(50);
		home.Weather.WindSpeed = CFixedPoint.FromInt(10);
		var front = new CWeatherFront(2, direction: 500, rotation: 10);

		Assert.True(CWeatherFronts.Tick(state, front));

		Assert.Equal(new[] { 3 }, front.Provinces);
		Assert.Equal(510, front.Direction);
		Assert.Equal(8, front.Rotation);
		Assert.Equal(2, front.MoveCountdown);
		Assert.Same(front, state.Province(3).WeatherFront);
		Assert.Null(home.WeatherFront);
		CWeather taken = state.Province(3).Weather;
		Assert.Equal(CFixedPoint.FromInt(49), taken.Humidity);                       // 0.98 of the home's
		Assert.Equal(CFixedPoint.Parse("40.2"), home.Weather.Humidity);              // which keeps four fifths of it
		Assert.Equal(CFixedPoint.Parse("902.26"), taken.Pressure);                   // pulled down 0.98 of the way
		Assert.Equal(CFixedPoint.Parse("28.25"), taken.WindSpeed);                   // a quarter of the difference
		// Its next hour only marks its province.
		Assert.True(CWeatherFronts.Tick(state, front));
		Assert.Equal(new[] { 3 }, front.Provinces);
	}

	[Fact]
	public void High_pressure_sets_its_zone_and_raises_each_province_out_from_it_a_step_an_hour()
	{
		CGameState state = World();
		CWeatherFronts.PropagateHighPressure(state, 1);
		Assert.Equal(CFixedPoint.FromInt(1090), state.Province(1).Weather.Pressure);
		Assert.All(state.Provinces.Skip(2), province => Assert.Equal(CFixedPoint.FromInt(1023), province.Weather.Pressure));
	}

	[Fact]
	public void A_zone_bears_a_front_with_a_drawn_heading_then_waits_and_turns_an_eighth_for_the_next()
	{
		CGameState state = World();
		state.Weather.LowPressureZones = new[] { (2, 31) };
		state.Weather.Cooldowns.Add(0);
		state.Weather.Headings.Add(0);
		state.Province(2).Climate = 3;

		CWeatherFront? first = CWeatherFronts.CreateFront(state);
		Assert.NotNull(first);
		Assert.InRange(state.Weather.Cooldowns[0], 24, 71);
		Assert.InRange(state.Province(2).Weather.Pressure.Raw, 870000, 913000);
		Assert.Equal(-10, first!.Rotation);                                         // the north of the map curves the other way
		state.Weather.Fronts.Add(first);
		Assert.Null(CWeatherFronts.CreateFront(state));

		state.Weather.Cooldowns[0] = 0;
		int heading = state.Weather.Headings[0];
		CWeatherFront? second = CWeatherFronts.CreateFront(state);
		Assert.Equal(heading + 125 > 1000 ? heading - 875 : heading + 125, second!.Direction);

		// A zone whose climate is not in its mask bears nothing.
		state.Weather.LowPressureZones = new[] { (2, 16) };
		state.Weather.Cooldowns[0] = 0;
		Assert.Null(CWeatherFronts.CreateFront(state));
	}
}
