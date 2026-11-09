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
/// What an hour of a bombing makes of every wing of the bombers: each thing that bears on
/// it noted on the wing and multiplied into its attack and its defence. It is the air
/// battle's list with three differences - every wing crowds the others, a bomber that is
/// also in an air battle fires the less for it, and bombers of ships may have a surprise -
/// and without the nearness of the base and the fighter's bonus. The difficulty comes first
/// on each wing. See <c>docs/mechanics/bombing.md</c>, section 9, and
/// <c>docs/mechanics/leaders.md</c>, section 10, for what the leaders bring.
/// </summary>
public static class CBomberModifiers
{
	public const string CagStackingPenalty = "CAG_STACKING_PENALTY";
	public const string SurpriseChance = "AIR_COMBAT_NAV_SURPRISE_CHANCE";
	public const string SurpriseBonus = "AIR_COMBAT_NAV_SURPRISE_BONUS";
	public const string SurpriseRounds = "AIR_COMBAT_NAV_SURPRISE_ROUNDS";
	public const string AirCombatOnBombing = "AIR_COMBAT_ON_BOMBING";

	/// <summary>The technology figure that goes straight onto a unit's attack and defence: the radio's.</summary>
	public const string CombatEfficiency = "combat_efficiency";

	public const string WindModifier = "BOMBWINDSPEEDMODIFIER";
	public const string CloudModifier = "BOMBCLOUDMODIFIER";
	public const string RainModifier = "BOMBRAINMODIFIER";
	public const string LowTemperature = "BOMBLOWTEMPERATURETHRESHOLD";
	public const string LowTemperatureImpact = "BOMBLOWTEMPERATUREIMPACT";
	public const string HighTemperature = "BOMBHIGHTEMPERATURETHRESHOLD";
	public const string HighTemperatureImpact = "BOMBHIGHTEMPERATUREIMPACT";

	/// <summary>No one thing leaves a wing with under a hundredth of what it had.</summary>
	private const long LeastFactor = 10;

	/// <summary>The least either side of an air battle is taken to have, in thousandths of strength.</summary>
	private const long LeastSide = 1000;

	public static void Apply(CGameState state, CCombat bombing)
	{
		CCombatSide side = bombing.Attacker;
		CProvince province = state.Province(bombing.Province);
		CDefines defines = state.Defines;
		string military = CManpower.MilitarySection;
		bool isNight = CDaylight.IsNight(state, province);
		long weather = Weather(state, province);
		long radar = CRadarCover.Level(state, province, side);

		// Every wing counts, and a carrier's over water for less.
		long crowd = 0;
		foreach (CUnit unit in side.Units)
		{
			foreach (CSubUnit wing in unit.SubUnits)
			{
				crowd += wing.Type.Flag(CAirFire.IsCag) && province.IsSea
					? defines[military, CagStackingPenalty].Raw
					: CFixedPoint.Scale;
			}
		}
		long crowding = Math.Max(defines[military, CAirModifiers.StackingPenalty].Raw * crowd / CFixedPoint.Scale,
			defines[military, CAirModifiers.MostStackingPenalty].Raw);

		// The surprise is asked for as the round before left it, and made again here.
		bool hadSurprise = side.HadAirSurprise;
		side.HadAirSurprise = false;
		long surprise = 0;
		if (bombing.Kind == ECombatKind.NavalBombing)
		{
			bool has = side.IsAttacker && bombing.Hour < 1
				? state.Random.Next(100) < defines[military, SurpriseChance].Raw / CFixedPoint.Scale || hadSurprise
				: hadSurprise;
			if (has && bombing.Hour < defines[military, SurpriseRounds].Raw / CFixedPoint.Scale)
			{
				surprise = defines[military, SurpriseBonus].Raw;
			}
		}

		foreach (CUnit unit in side.Units)
		{
			CCountry? owner = state.Country(unit.FiguresOwner);
			// A played country's, by the difficulty, first on each of its wings (frontend.md, section 4).
			long difficulty = Settings.CDifficulty.Combat(state, owner);
			long airCombat = AirCombat(state, unit);
			long missionEfficiency = unit.Order == EUnitOrder.Move ? 0 : CMissionEfficiency.Of(state, owner, unit.Order);
			long experienceTrait = CCommandReach.Effect(state, unit, ETraitEffect.ExperienceBonus);
			long supplyTrait = CCommandReach.Effect(state, unit, ETraitEffect.OutOfSupplyModifier);
			long nightTrait = CCommandReach.Effect(state, unit, ETraitEffect.NightAttack);
			long? leader = CCombatLeaders.Bonus(state, unit);

			foreach (CSubUnit wing in unit.SubUnits)
			{
				wing.CombatModifiers.Clear();
				wing.CombatAttack = CFixedPoint.One;
				wing.CombatDefence = CFixedPoint.One;

				if (difficulty != 0)
				{
					Add(wing, ECombatModifier.Difficulty, difficulty, difficulty);
				}

				if (surprise > 0)
				{
					Add(wing, ECombatModifier.Surprise, surprise, 0);
					side.HadAirSurprise = true;
				}
				if (airCombat != 0)
				{
					Add(wing, ECombatModifier.AirCombat, airCombat, airCombat);
				}
				if (unit.SuppliesReceived != CFixedPoint.One)
				{
					long missing = CFixedPoint.Scale - unit.SuppliesReceived.Raw;
					long impact = defines[military, CCombatModifiers.SupplyLackImpact].Raw * missing / CFixedPoint.Scale;
					impact = CCombatModifiers.Scaled(impact, supplyTrait);
					Add(wing, ECombatModifier.LackOfSupplies, -impact, -impact);
				}

				long experience = Math.Max(0, wing.Experience.Raw);
				if (wing.IsPride)
				{
					experience += defines[military, CNavalModifiers.PrideBonus].Raw;
				}
				experience = experience * 10 / CFixedPoint.Scale;
				if (experience != 0)
				{
					experience = CCombatModifiers.Scaled(experience, experienceTrait);
					Add(wing, ECombatModifier.Experience, experience, experience);
				}

				if (missionEfficiency != 0)
				{
					Add(wing, ECombatModifier.MissionEfficiency, missionEfficiency, missionEfficiency);
				}
				long radio = owner is null ? 0 : owner.Technology[CombatEfficiency].Raw;
				if (radio != 0)
				{
					Add(wing, ECombatModifier.Radio, radio, radio);
				}
				if (owner is not null && owner.Dissent != CFixedPoint.Zero)
				{
					long dissent = owner.Dissent.Raw * 10 / CFixedPoint.Scale;
					long impact = defines[military, CCombatModifiers.DissentImpact].Raw * dissent / CFixedPoint.Scale;
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
					long night = defines[military, CCombatModifiers.NightPenalty].Raw + Adjuster(wing, kind) + nightTrait;
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
	/// What being in battles of its own costs a bombing unit: for each battle on its
	/// list, <c>AIR_COMBAT_ON_BOMBING</c> times the attackers' strength over the
	/// defenders' in its air battles so far, each side taken for a wing's worth at least.
	/// </summary>
	public static long AirCombat(CGameState state, CUnit unit)
	{
		long penalty = 0;
		long attackers = 0;
		long defenders = 0;
		long perBattle = state.Defines[CManpower.MilitarySection, AirCombatOnBombing].Raw;
		foreach (CCombat battle in unit.Combats)
		{
			if (battle.Kind == ECombatKind.Air)
			{
				defenders += Strength(battle.Defender);
				attackers += Strength(battle.Attacker);
			}
			defenders = Math.Max(defenders, LeastSide);
			attackers = Math.Max(attackers, LeastSide);
			penalty += perBattle * (attackers * CFixedPoint.Scale / defenders) / CFixedPoint.Scale;
		}
		return penalty;
	}

	/// <summary>
	/// What the weather over a province takes off a bomber's attack: the wind, the cold
	/// or the heat past their thresholds, the cloud and the rain, by the bombing's own
	/// figures.
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

	private static long Strength(CCombatSide side)
	{
		long strength = 0;
		foreach (CUnit unit in side.Units)
		{
			strength += CUnitTotals.Strength(unit).Raw;
		}
		return strength;
	}

	/// <summary>The wing's own figure for the night, attack or defence.</summary>
	internal static long Adjuster(CSubUnit subUnit, string kind)
	{
		return subUnit.Stats.Adjusters.TryGetValue(CCombatModifiers.NightAdjuster, out var night) ? night[kind].Raw : 0;
	}

	/// <summary>Notes one thing on a sub unit and multiplies it into what the hour makes of it.</summary>
	internal static void Add(CSubUnit subUnit, ECombatModifier kind, long attack, long defence)
	{
		subUnit.CombatModifiers.Add(new CCombatModifier(kind, new CFixedPoint(attack), new CFixedPoint(defence)));
		subUnit.CombatAttack = new CFixedPoint(subUnit.CombatAttack.Raw * Math.Max(CFixedPoint.Scale + attack, LeastFactor) / CFixedPoint.Scale);
		subUnit.CombatDefence = new CFixedPoint(subUnit.CombatDefence.Raw * Math.Max(CFixedPoint.Scale + defence, LeastFactor) / CFixedPoint.Scale);
	}
}
