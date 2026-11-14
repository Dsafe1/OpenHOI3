using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What the ground, the weather and the way there wear a land unit down by: a brigade's figure in a province, and the day's
/// loss of strength it makes out of combat. See <c>docs/mechanics/upkeep.md</c>, section 3a;
/// <c>reversing/findings/FINDINGS-manpower.md</c>, Attrition.
/// </summary>
public static class CAttrition
{
	public const string WeatherSection = "weather";
	public const string HighTemperature = "HIGHTEMPERATUREATTRITIONTHRESHOLD";
	public const string LowTemperature = "LOWTEMPERATUREATTRITIONTHRESHOLD";
	public const string Wind = "WINDATTRITIONTHRESHOLD";

	/// <summary>The province's own modifier and the three that make its infrastructure.</summary>
	public const string Modifier = "attrition";
	public const string Infrastructure = "infrastructure";
	public const string LocalInfrastructure = "local_infrastructure";
	public const string GlobalInfrastructure = "global_infrastructure";

	/// <summary>The country's modifiers that take the edge off the heat of humid ground and off the cold.</summary>
	public const string JungleEffects = "jungle_effects";
	public const string WinterEffects = "winter_effects";

	/// <summary>The technologies' total that cuts it, a fraction below nothing.</summary>
	public const string Technology = "maximum_attrition";

	private const long Scale = CFixedPoint.Scale;

	/// <summary>The least share of its infrastructure a province is counted to have: the floor of 10.5 thousandths.</summary>
	private const long LeastInfrastructure = 10;

	/// <summary>The fixed 0.010 the day's loss is taken by, the floor of 10.5 thousandths.</summary>
	private const long DailyFactor = 10;

	/// <summary>A month's rate taken a day at a time.</summary>
	private const long DaysInAMonth = 30;

	/// <summary>
	/// A brigade's attrition in a province, in thousandths - the original's, term by term: nought for a brigade that is not
	/// land; else the province's own modifier, what its infrastructure lacks, and the weather's, and the terrain's where
	/// above nothing; in a friend's province a tenth at the front and nought elsewhere; and the whole cut by the country's
	/// technology, never below nothing.
	/// </summary>
	public static long Of(CGameState state, CProvince province, CSubUnit brigade)
	{
		if (brigade.Type.Branch != EUnitBranch.Land || brigade.Unit is not { } unit || state.Country(unit.Owner) is not { } owner)
		{
			return 0;
		}
		long sum = province.Modifier[Modifier].Raw + (Scale - InfrastructureShare(province)) + Weather(state, province, owner, unit);
		if (province.Terrain is { } terrain)
		{
			long ground = terrain.Attrition.Raw
				+ (brigade.Stats.Adjusters.TryGetValue(terrain.Name, out Modifiers.CModifier? adjuster) ? adjuster[Modifier].Raw : 0);
			if (ground > 0)
			{
				sum += ground;
			}
		}
		if (province.Owner is { } holder && state.Country(holder) is { } ownerOfGround
			&& CCountryLists.IsFriend(state, ownerOfGround, unit.Owner, withAccess: true))
		{
			sum = CTheatreFigures.IsFrontProvince(state, province, false) ? sum * Scale / (10 * Scale) : 0;
		}
		return Math.Max(0, (Scale + owner.Technology[Technology].Raw) * sum / Scale);
	}

	/// <summary>How much of its infrastructure a province has standing, between a hundredth and whole.</summary>
	public static long InfrastructureShare(CProvince province)
	{
		long share = province.Modifier[Infrastructure].Raw
			* (Scale + province.Modifier[LocalInfrastructure].Raw + province.Modifier[GlobalInfrastructure].Raw) / Scale;
		return Math.Clamp(share, LeastInfrastructure, Scale);
	}

	/// <summary>
	/// The weather's part, the first that holds: heat, cut by the country's jungle effects where the ground is humid; cold,
	/// less its winter effects; wind; else nothing - and in a province below nought degrees the unit's commanders' winter
	/// trait added, the whole never below nothing.
	/// </summary>
	public static long Weather(CGameState state, CProvince province, CCountry owner, CUnit unit)
	{
		OpenHOI3.Sim.Weather.CWeather weather = province.Weather;
		long temperature = weather.Temperature.Raw;
		long effect = 0;
		if (temperature >= state.Defines[WeatherSection, HighTemperature].Raw)
		{
			effect = Scale;
			if (province.Terrain is { Humidity.Raw: > 0 })
			{
				effect = Scale * (Scale - owner.Modifier[JungleEffects].Raw) / Scale;
			}
		}
		else if (temperature <= state.Defines[WeatherSection, LowTemperature].Raw)
		{
			effect = Scale - owner.Modifier[WinterEffects].Raw;
		}
		else if (weather.WindSpeed.Raw >= state.Defines[WeatherSection, Wind].Raw)
		{
			effect = Scale;
		}
		if (temperature < 0)
		{
			effect += CCommandReach.Effect(state, unit, ETraitEffect.WinterAttrition);
		}
		return Math.Max(0, effect);
	}

	/// <summary>
	/// The day's attrition off a land unit that is in no battle: each brigade loses its figure times 0.010 times its full
	/// strength, over thirty, each step cut to the thousandth - and the loss counted on the played country's records. Not
	/// while a game is being made, as the original runs it only with a game on screen.
	/// </summary>
	public static void ApplyDaily(CGameState state, CCountry owner, CUnit unit)
	{
		if (state.IsBeingMade || unit.IsFighting || unit.Kind is EUnitKind.Navy or EUnitKind.Air || unit.Location <= 0)
		{
			return;
		}
		CProvince province = state.Province(unit.Location);
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			long most = CUnits.MaxStrength(state, owner, brigade).Raw;
			long lost = Of(state, province, brigade) * DailyFactor / Scale * most / Scale * Scale / (DaysInAMonth * Scale);
			if (lost != 0)
			{
				StrategicWarfare.CWarfareTally.Attrition(state, province, brigade, lost);
			}
			long left = Math.Max(0, Math.Min(brigade.Strength.Raw - lost, most));
			brigade.Strength = new CFixedPoint(left);
			if (brigade.Strength > brigade.Highest)
			{
				brigade.Highest = brigade.Strength;
			}
		}
	}
}
