using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Weather;

/// <summary>
/// The weather's own hour: the high pressure spreading out of its zones, fronts born at the low pressure zones, and
/// each front stepping on, handing its humidity, low pressure, wind and warmth to the provinces it takes. The original's
/// <c>CWeatherManager::Tick</c>, <c>CreateFront</c>, <c>PropagateHighPressure</c>, <c>CWeatherFront::Tick</c> and
/// <c>SpreadWeather</c>, step for step. See <c>docs/mechanics/weather.md</c>, section 3.
/// </summary>
public static class CWeatherFronts
{
	public const string MaxFromEachPressure = "MAXFROMEACHPRESSURE";
	public const string PressureReduction = "PRESSUREREDUCTION";
	public const string PressurePropagation = "PRESSUREPROPAGATION";
	public const string PressureThreshold = "PRESSURETHRESHOLD";
	public const string MovementDelay = "WEATHERMOVEMENTDELAY";
	public const string LowPressureBase = "LOWPRESSUREBASE";
	public const string LowPressureOffset = "LOWPRESSUREOFFSET";

	private const string Section = CProvinceWeather.Section;
	private const long One = CFixedPoint.Scale;

	/// <summary>A front over this many pixels' distance from home, and under four provinces, dies (four times pixels).</summary>
	private const long FarFromHome = 4000;

	/// <summary>
	/// The hour, in order: the cooldowns count down; each high pressure zone spreads, in the file's order; fronts are
	/// born until the cap or until no zone may bear one; every province is cleared of its front; and each front ticks in
	/// turn, the oldest first, one that dies freeing its zone's cooldown and the provinces it held.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		CWeatherManager manager = state.Weather;
		if (manager.LowPressureZones.Count == 0 && manager.HighPressureZones.Count == 0)
		{
			return;
		}
		if (manager.Cooldowns.Count == 0)
		{
			foreach ((int _, int _) in manager.LowPressureZones)
			{
				manager.Cooldowns.Add(0);
				manager.Headings.Add(0);
			}
		}
		for (int i = 0; i < manager.Cooldowns.Count; i++)
		{
			manager.Cooldowns[i]--;
		}
		foreach (int zone in manager.HighPressureZones)
		{
			PropagateHighPressure(state, zone);
		}

		long cap = manager.LowPressureZones.Count * (state.Defines[Section, MaxFromEachPressure].Raw / One);
		while (manager.Fronts.Count < cap && CreateFront(state) is { } front)
		{
			manager.Fronts.Add(front);
		}

		foreach (CProvince province in state.Provinces)
		{
			province.WeatherFront = null;
		}
		foreach (CWeatherFront front in new List<CWeatherFront>(manager.Fronts))
		{
			if (Tick(state, front))
			{
				continue;
			}
			manager.Fronts.Remove(front);
			// The zone is found by its place in the whole list, where a new front's slot counts only the zones whose
			// climate let them through: the original's, kept.
			for (int j = 0; j < manager.LowPressureZones.Count && j < manager.Cooldowns.Count; j++)
			{
				if (manager.LowPressureZones[j].Province == front.Home)
				{
					manager.Cooldowns[j] = 0;
				}
			}
			foreach (int id in front.Provinces)
			{
				state.Province(id).WeatherFront = null;
			}
		}
	}

	/// <summary>
	/// The first zone, in the file's order, that may bear a front now: its climate in its mask, no front over it (as the
	/// hour before left the markers), its cooldown run out and fewer than its share of fronts alive. Its heading is
	/// drawn the first time and then turned on by an eighth of the compass at each front; its cooldown is drawn; the new
	/// low's depth is drawn. None where no zone may.
	/// </summary>
	public static CWeatherFront? CreateFront(CGameState state)
	{
		CWeatherManager manager = state.Weather;
		long most = state.Defines[Section, MaxFromEachPressure].Raw;
		int i = 0;
		foreach ((int id, int mask) in manager.LowPressureZones)
		{
			CProvince home = state.Province(id);
			if (home.Climate == 0)
			{
				home.Climate = CClimate.Of(state, home);
			}
			if ((mask & (1 << home.Climate)) == 0)
			{
				continue;
			}
			if (home.WeatherFront is not null || manager.Cooldowns[i] > 0)
			{
				i++;
				continue;
			}
			manager.Cooldowns[i] = 0;
			int alive = 0;
			foreach (CWeatherFront front in manager.Fronts)
			{
				alive += front.Home == id ? 1 : 0;
			}
			if (alive >= most / One)
			{
				i++;
				continue;
			}

			int heading = manager.Headings[i];
			if (heading == 0)
			{
				heading = state.Random.Next(1000);
			}
			else
			{
				heading += most == 0 ? -1 : (int)(1000000 / most);
				if (heading > 1000)
				{
					heading -= 1000;
				}
			}
			manager.Headings[i] = heading;
			manager.Cooldowns[i] = 24 + state.Random.Next(48);
			return Born(state, home, heading, home.IsNorthern ? -10 : 10);
		}
		return null;
	}

	/// <summary>A front born at its home: the home alone under it, and the home's pressure dropped by a drawn depth.</summary>
	private static CWeatherFront Born(CGameState state, CProvince home, int heading, int rotation)
	{
		var front = new CWeatherFront(home.Id, heading, rotation);
		long offset = state.Defines[Section, LowPressureOffset].Raw / One;
		long drop = state.Defines[Section, LowPressureBase].Raw + One * state.Random.Next((int)Math.Max(offset, 0));
		home.Weather.Pressure = new CFixedPoint(home.Weather.Pressure.Raw - drop);
		CProvinceWeather.Clamp(state, home.Weather);
		return front;
	}

	/// <summary>
	/// A front's hour. Between moves it only marks its provinces as its own. On a move it takes, from each province it
	/// stands on still below the default pressure, the neighbours in its arc with no front and not much higher pressure -
	/// only one each once it covers over a hundred - hands that province's weather to them, and leaves everything it
	/// stood on. Its arc narrows as it grows. Answers whether it lives.
	/// </summary>
	public static bool Tick(CGameState state, CWeatherFront front)
	{
		if (--front.MoveCountdown > 0)
		{
			foreach (int id in front.Provinces)
			{
				state.Province(id).WeatherFront = front;
			}
			return true;
		}
		int count = front.Provinces.Count;
		front.MoveCountdown = (int)(state.Defines[Section, MovementDelay].Raw / One);
		long arc = 200;
		if (count < 3)
		{
			arc += 150;
		}
		if (count > 30)
		{
			arc = arc * One / 2000;
		}
		if (count > 100)
		{
			arc = arc * One / 2000;
		}
		front.Direction += front.Rotation;
		front.Rotation = (int)(front.Rotation * 800L / One);
		if (count < 1)
		{
			return false;
		}
		if (count < 4 && Distance(state, state.Province(front.Provinces[0]), state.Province(front.Home)) > FarFromHome)
		{
			return false;
		}

		var left = new List<int>(front.Provinces);
		foreach (int id in left)
		{
			state.Province(id).WeatherFront = front;
		}
		front.Provinces.Clear();
		long pressureDefault = state.Defines[Section, CProvinceWeather.PressureDefault].Raw;
		long threshold = state.Defines[Section, PressureThreshold].Raw;
		foreach (int id in left)
		{
			CProvince from = state.Province(id);
			if (from.Weather.Pressure.Raw >= pressureDefault)
			{
				continue;
			}
			var taken = new List<CProvince>();
			bool spread = false;
			foreach (CProvinceEdge edge in from.Edges)
			{
				CProvince to = state.Province(edge.To);
				if (to.WeatherFront is not null
					|| to.Weather.Pressure.Raw - threshold > from.Weather.Pressure.Raw
					|| (left.Count > 100 && spread)
					|| !IsWithinArc(front.Direction, edge.Bearing, (int)arc))
				{
					continue;
				}
				spread = true;
				taken.Add(to);
				to.WeatherFront = front;
				front.Provinces.Add(to.Id);
			}
			Spread(from.Weather, taken);
		}
		foreach (int id in left)
		{
			state.Province(id).WeatherFront = null;
		}
		return front.Provinces.Count > 0;
	}

	/// <summary>
	/// What a province a front leaves hands on: nearly all its humidity to each province taken - it keeps four fifths of
	/// what it gave, once - its low pressure, wind out of the difference in pressure or its own, and nearly all of the
	/// difference in warmth, at most ten degrees. Nothing is held to its range here.
	/// </summary>
	public static void Spread(CWeather from, IReadOnlyList<CProvince> to)
	{
		long rain = from.Humidity.Raw * 980 / One;
		from.Humidity = new CFixedPoint(from.Humidity.Raw - rain / 5);
		foreach (CProvince province in to)
		{
			CWeather weather = province.Weather;
			weather.Humidity = new CFixedPoint(weather.Humidity.Raw + rain);
			long wind = (from.WindSpeed.Raw - weather.WindSpeed.Raw) * 970 / One;
			long difference = weather.Pressure.Raw - from.Pressure.Raw;
			if (difference > 0)
			{
				weather.Pressure = new CFixedPoint(weather.Pressure.Raw + -difference * 980 / One);
				wind = Math.Max(difference * 250 / One, wind);
			}
			weather.WindSpeed = new CFixedPoint(weather.WindSpeed.Raw + wind);
			long warmth = (from.Temperature.Raw - weather.Temperature.Raw) * 950 / One;
			weather.Temperature = new CFixedPoint(weather.Temperature.Raw + Math.Clamp(warmth, -10000, 10000));
		}
	}

	/// <summary>
	/// Whether a value lies in the arc of half-width <paramref name="half"/> about a centre, all in thousandths of a
	/// turn, by C's remainder: a centre under the half-width does not wrap, a centre of 1000 does, and the value is never
	/// brought back into range.
	/// </summary>
	public static bool IsWithinArc(int value, int centre, int half)
	{
		int high = (centre + half) % 1000;
		int low = (centre - half) % 1000;
		return low < high ? low <= value && value <= high : value >= low || value <= high;
	}

	/// <summary>
	/// A high pressure zone's hour: its province set to the most pressure there is, and a walk outward in the order
	/// provinces are reached, each step costing the next province's size, raising any province below what its cost
	/// leaves by one step - and going on through it while that is above nothing.
	/// </summary>
	public static void PropagateHighPressure(CGameState state, int seed)
	{
		var distance = new long[state.Provinces.Count];
		Array.Fill(distance, 99999);
		long max = state.Defines[Section, CProvinceWeather.PressureMax].Raw;
		long reduction = state.Defines[Section, PressureReduction].Raw;
		long step = state.Defines[Section, PressurePropagation].Raw;
		CProvince start = state.Province(seed);
		start.Weather.Pressure = new CFixedPoint(max);
		distance[seed] = 0;
		var queue = new Queue<CProvince>();
		queue.Enqueue(start);
		while (queue.Count > 0)
		{
			CProvince current = queue.Dequeue();
			foreach (CProvinceEdge edge in current.Edges)
			{
				CProvince next = state.Province(edge.To);
				long cost = CSupplyFigures.SizeClass(next) + distance[current.Id];
				if (distance[next.Id] <= cost)
				{
					continue;
				}
				long target = max - cost * One * reduction / One;
				if (target <= next.Weather.Pressure.Raw)
				{
					continue;
				}
				next.Weather.Pressure = new CFixedPoint(next.Weather.Pressure.Raw + step);
				distance[next.Id] = cost;
				if (target > 0)
				{
					queue.Enqueue(next);
				}
			}
		}
	}

	/// <summary>
	/// Four times the distance in pixels between two provinces' bounding-box centres, the short way round the map, as a
	/// whole number rounded down.
	/// </summary>
	public static long Distance(CGameState state, CProvince a, CProvince b)
	{
		long dx = Math.Abs(a.CentreX - b.CentreX);
		if (state.MapWidth > 0 && dx > state.MapWidth / 2)
		{
			dx = state.MapWidth - dx;
		}
		long dy = a.CentreY - b.CentreY;
		long square = 16 * (dx * dx + dy * dy);
		long root = (long)Math.Sqrt(square);
		while (root * root > square)
		{
			root--;
		}
		while ((root + 1) * (root + 1) <= square)
		{
			root++;
		}
		return root;
	}
}
