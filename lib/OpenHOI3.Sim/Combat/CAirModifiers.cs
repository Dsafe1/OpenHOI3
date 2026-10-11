using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What an hour of a battle in the air makes of every wing: each thing that bears on it
/// noted on the wing and multiplied into its attack and its defence. Only the attack
/// decides anything - nothing in an air battle reads a wing's defence. The original also
/// counts difficulty, which is not built. See <c>docs/mechanics/aircombat.md</c>, section 8,
/// and <c>docs/mechanics/leaders.md</c>, section 10, for what the leaders bring.
/// </summary>
public static class CAirModifiers
{
	public const string StackingPenalty = "AIR_STACKING_PENALTY";
	public const string MostStackingPenalty = "AIR_STACKING_PENALTY_MAX";
	public const string BaseProximityBonus = "BASE_PROXIMITY_BONUS";
	public const string InterceptBonus = "INTERCEPT_ATTACK_BONUS";
	public const string SuperiorityBonus = "AIR_SUP_DEFEND_BONUS";
	public const string AirDetection = "air_detection";
	public const string IsBomber = "is_bomber";

	public const string WindModifier = "AIRWINDSPEEDMODIFIER";
	public const string CloudModifier = "AIRCLOUDMODIFIER";
	public const string RainModifier = "AIRRAINMODIFIER";
	public const string LowTemperature = "AIRLOWTEMPERATURETHRESHOLD";
	public const string LowTemperatureImpact = "AIRLOWTEMPERATUREIMPACT";
	public const string HighTemperature = "AIRHIGHTEMPERATURETHRESHOLD";
	public const string HighTemperatureImpact = "AIRHIGHTEMPERATUREIMPACT";

	/// <summary>No one thing leaves a wing with under a hundredth of what it had.</summary>
	private const long LeastFactor = 10;

	public static void Apply(CGameState state, CCombat combat, CCombatSide side)
	{
		CProvince province = state.Province(combat.Province);
		CDefines defines = state.Defines;
		bool isNight = CDaylight.IsNight(state, province);
		long weather = Weather(state, province);
		long radar = CRadarCover.Level(state, province, side);

		int wings = 0;
		foreach (CUnit unit in side.Units)
		{
			wings += unit.SubUnits.Count;
		}
		long crowding = Math.Max(
			(wings * (long)CFixedPoint.Scale - CFixedPoint.Scale) * defines[CManpower.MilitarySection, StackingPenalty].Raw / CFixedPoint.Scale,
			defines[CManpower.MilitarySection, MostStackingPenalty].Raw);

		foreach (CUnit unit in side.Units)
		{
			CCountry? owner = state.Country(unit.Owner);
			bool nearBase = unit.Base != 0
				&& (unit.Base == province.Id || state.Province(unit.Base).EdgeTo(province.Id) is not null);
			long missionEfficiency = CMissionEfficiency.Of(state, owner, unit.Order);
			long experienceTrait = CCommandReach.Effect(state, unit, ETraitEffect.ExperienceBonus);
			long supplyTrait = CCommandReach.Effect(state, unit, ETraitEffect.OutOfSupplyModifier);
			long nightTrait = CCommandReach.Effect(state, unit, ETraitEffect.NightAttack);
			long? leader = CCombatLeaders.Bonus(state, unit);

			foreach (CSubUnit wing in unit.SubUnits)
			{
				wing.CombatModifiers.Clear();
				wing.CombatAttack = CFixedPoint.One;
				wing.CombatDefence = CFixedPoint.One;

				long experience = Math.Max(0, wing.Experience.Raw);
				if (wing.IsPride)
				{
					experience += defines[CManpower.MilitarySection, CNavalModifiers.PrideBonus].Raw;
				}
				experience = experience * 10 / CFixedPoint.Scale;
				if (experience != 0)
				{
					experience = CCombatModifiers.Scaled(experience, experienceTrait);
					Add(wing, ECombatModifier.Experience, experience, experience);
				}

				if (nearBase)
				{
					long bonus = defines[CManpower.MilitarySection, BaseProximityBonus].Raw;
					Add(wing, ECombatModifier.BaseProximity, bonus, bonus);
				}

				// Both go on the attack alone, the one called a defender's bonus among them.
				if (unit.Order == EUnitOrder.AirIntercept && !wing.Type.Flag(IsBomber))
				{
					Add(wing, ECombatModifier.Intercept, defines[CManpower.MilitarySection, InterceptBonus].Raw, 0);
				}
				if (unit.Order == EUnitOrder.AirSuperiority)
				{
					Add(wing, ECombatModifier.Intercept, defines[CManpower.MilitarySection, SuperiorityBonus].Raw, 0);
				}

				if (missionEfficiency != 0)
				{
					Add(wing, ECombatModifier.MissionEfficiency, missionEfficiency, missionEfficiency);
				}

				if (unit.SuppliesReceived != CFixedPoint.One)
				{
					long missing = CFixedPoint.Scale - unit.SuppliesReceived.Raw;
					long impact = defines[CManpower.MilitarySection, CCombatModifiers.SupplyLackImpact].Raw * missing / CFixedPoint.Scale;
					impact = CCombatModifiers.Scaled(impact, supplyTrait);
					Add(wing, ECombatModifier.LackOfSupplies, -impact, -impact);
				}

				long radio = owner is null ? 0 : owner.Technology[CBomberModifiers.CombatEfficiency].Raw;
				if (radio != 0)
				{
					Add(wing, ECombatModifier.Radio, radio, radio);
				}

				if (owner is not null && owner.Dissent != CFixedPoint.Zero)
				{
					long dissent = owner.Dissent.Raw * 10 / CFixedPoint.Scale;
					long impact = defines[CManpower.MilitarySection, CCombatModifiers.DissentImpact].Raw * dissent / CFixedPoint.Scale;
					Add(wing, ECombatModifier.Dissent, -impact, -impact);
				}

				if (crowding < 0)
				{
					Add(wing, ECombatModifier.Stacking, crowding, crowding);
				}

				if (radar != 0)
				{
					long bonus = CRadarCover.Bonus(state, owner, radar);
					Add(wing, ECombatModifier.Radar, bonus, bonus);
				}

				if (weather != 0)
				{
					Add(wing, ECombatModifier.Weather, -weather, 0);
				}

				if (isNight)
				{
					string kind = side.IsAttacker ? CCombatModifiers.AttackOf : CCombatModifiers.DefenceOf;
					long night = defines[CManpower.MilitarySection, CCombatModifiers.NightPenalty].Raw
						+ Adjuster(wing, kind) + nightTrait + wing.Stats[AirDetection].Raw * 10 / CFixedPoint.Scale;
					if (night < 0)
					{
						Add(wing, ECombatModifier.Night, night, 0);
					}
				}

				if (leader is { } led)
				{
					Add(wing, ECombatModifier.Leader, led, led);
				}
			}
		}
	}

	/// <summary>
	/// What the weather over a province takes off a wing's attack: the wind, the cold or
	/// the heat past their thresholds, the cloud and the rain.
	/// </summary>
	public static long Weather(CGameState state, CProvince province)
	{
		CDefines defines = state.Defines;
		string section = CWeatherEffects.WeatherSection;
		CWeather weather = province.Weather;
		long effect = weather.WindSpeed.Raw * defines[section, WindModifier].Raw / CFixedPoint.Scale;
		if (weather.Temperature.Raw <= defines[section, LowTemperature].Raw)
		{
			effect += defines[section, LowTemperatureImpact].Raw;
		}
		if (weather.Temperature.Raw >= defines[section, HighTemperature].Raw)
		{
			effect += defines[section, HighTemperatureImpact].Raw;
		}
		effect += weather.CloudCoverage.Raw * defines[section, CloudModifier].Raw / CFixedPoint.Scale;
		effect += weather.Precipitation.Raw * defines[section, RainModifier].Raw / CFixedPoint.Scale;
		return effect;
	}

	/// <summary>The wing's own figure for the night, attack or defence.</summary>
	private static long Adjuster(CSubUnit wing, string kind)
	{
		return wing.Stats.Adjusters.TryGetValue(CCombatModifiers.NightAdjuster, out var night) ? night[kind].Raw : 0;
	}

	/// <summary>Notes one thing on a wing and multiplies it into what the hour makes of it.</summary>
	private static void Add(CSubUnit wing, ECombatModifier kind, long attack, long defence)
	{
		wing.CombatModifiers.Add(new CCombatModifier(kind, new CFixedPoint(attack), new CFixedPoint(defence)));
		wing.CombatAttack = new CFixedPoint(wing.CombatAttack.Raw * Math.Max(CFixedPoint.Scale + attack, LeastFactor) / CFixedPoint.Scale);
		wing.CombatDefence = new CFixedPoint(wing.CombatDefence.Raw * Math.Max(CFixedPoint.Scale + defence, LeastFactor) / CFixedPoint.Scale);
	}
}
