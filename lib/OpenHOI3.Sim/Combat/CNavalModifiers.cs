using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What an hour's circumstances make of each ship of a side. At sea every thing is put
/// on the ship and not on its fleet, and only the attack it comes to is ever read. See
/// <c>docs/mechanics/navalcombat.md</c>, section 3, and <c>docs/mechanics/leaders.md</c>,
/// section 10, for what the leaders bring.
/// </summary>
public static class CNavalModifiers
{
	public const string SurpriseChance = "NAVAL_COMBAT_SUB_SURPRISE_CHANCE";
	public const string SurpriseBonus = "NAVAL_COMBAT_SUB_SURPRISE_BONUS";
	public const string SurpriseRounds = "NAVAL_COMBAT_SUB_SURPRISE_ROUNDS";
	public const string PrideBonus = "PRIDE_BONUS_EXP";
	public const string WindModifier = "NAVALWINDSPEEDMODIFIER";
	public const string RainModifier = "NAVALRAINMODIFIER";

	/// <summary>No one thing leaves a ship with under a hundredth of what it had.</summary>
	private const long LeastFactor = 10;

	public static void Apply(CGameState state, CCombat combat, CCombatSide side)
	{
		CProvince province = state.Province(combat.Province);
		CDefines defines = state.Defines;
		bool isNight = CDaylight.IsNight(state, province);
		long weather = province.Weather.WindSpeed.Raw * defines[CWeatherEffects.WeatherSection, WindModifier].Raw / CFixedPoint.Scale
			+ province.Weather.Precipitation.Raw * defines[CWeatherEffects.WeatherSection, RainModifier].Raw / CFixedPoint.Scale;

		// The surprise is asked for as the round before left it, and made again here.
		bool surprised = side.HadSurprise;
		side.HadSurprise = false;

		foreach (CUnit fleet in side.Units)
		{
			CCountry? owner = state.Country(fleet.Owner);
			// A played country's, by the difficulty, first on each of its ships (frontend.md, section 4).
			long difficulty = Settings.CDifficulty.Combat(state, owner);
			int capitals = 0, submarines = 0, screens = 0;
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				if (CShipCensus.IsCapital(ship))
				{
					capitals++;
				}
				else if (CShipCensus.IsSubmarine(ship))
				{
					submarines++;
				}
				else if (!CShipCensus.IsTransport(ship))
				{
					screens++;
				}
			}

			long poorScreens = 0;
			if (capitals > 0 && screens < capitals)
			{
				long share = screens * (long)CFixedPoint.Scale * CFixedPoint.Scale / (capitals * (long)CFixedPoint.Scale);
				poorScreens = (CFixedPoint.Scale - share) * CFixedPoint.Scale / 3000;
			}

			long surprise = 0;
			if (side.IsAttacker && combat.Hour < 1 && submarines > 0
				&& state.Random.Next(100) < defines[CManpower.MilitarySection, SurpriseChance].Raw / CFixedPoint.Scale)
			{
				surprised = true;
			}
			if (surprised && combat.Hour < defines[CManpower.MilitarySection, SurpriseRounds].Raw / CFixedPoint.Scale)
			{
				surprise = defines[CManpower.MilitarySection, SurpriseBonus].Raw;
			}

			// How good the fleet's country is at what the fleet was sent to do; a plain move is no mission.
			long missionEfficiency = fleet.Order == EUnitOrder.Move
				? 0
				: Missions.CMissionEfficiency.Of(state, owner, fleet.Order);

			// What the fleet's leaders bring: three traits down its chain, and its own admiral's skill.
			long experienceTrait = CCommandReach.Effect(state, fleet, ETraitEffect.ExperienceBonus);
			long supplyTrait = CCommandReach.Effect(state, fleet, ETraitEffect.OutOfSupplyModifier);
			long nightTrait = CCommandReach.Effect(state, fleet, ETraitEffect.NightAttack);
			long? leader = CCombatLeaders.Bonus(state, fleet);

			foreach (CSubUnit ship in fleet.SubUnits)
			{
				ship.CombatModifiers.Clear();
				ship.CombatAttack = CFixedPoint.One;
				ship.CombatDefence = CFixedPoint.One;

				if (difficulty != 0)
				{
					Add(ship, ECombatModifier.Difficulty, difficulty, difficulty);
				}

				if (poorScreens > 0 && CShipCensus.IsCapital(ship))
				{
					Add(ship, ECombatModifier.PoorScreens, 0, -poorScreens);
				}
				if (surprise > 0 && CShipCensus.IsSubmarine(ship))
				{
					Add(ship, ECombatModifier.Surprise, surprise, 0);
					side.HadSurprise = true;
				}

				long experience = Math.Max(0, ship.Experience.Raw);
				if (ship.IsPride)
				{
					experience += defines[CManpower.MilitarySection, PrideBonus].Raw;
				}
				experience = experience * 10 / CFixedPoint.Scale;
				if (experience != 0)
				{
					experience = CCombatModifiers.Scaled(experience, experienceTrait);
					Add(ship, ECombatModifier.Experience, experience, experience);
				}

				if (missionEfficiency != 0)
				{
					Add(ship, ECombatModifier.MissionEfficiency, missionEfficiency, missionEfficiency);
				}

				if (owner is not null && owner.Dissent != CFixedPoint.Zero)
				{
					long dissent = owner.Dissent.Raw * 10 / CFixedPoint.Scale;
					long impact = defines[CManpower.MilitarySection, CCombatModifiers.DissentImpact].Raw * dissent / CFixedPoint.Scale;
					Add(ship, ECombatModifier.Dissent, -impact, -impact);
				}

				if (fleet.SuppliesReceived != CFixedPoint.One)
				{
					long missing = CFixedPoint.Scale - fleet.SuppliesReceived.Raw;
					long impact = defines[CManpower.MilitarySection, CCombatModifiers.SupplyLackImpact].Raw * missing / CFixedPoint.Scale;
					impact = CCombatModifiers.Scaled(impact, supplyTrait);
					Add(ship, ECombatModifier.LackOfSupplies, -impact, -impact);
				}

				if (weather != 0)
				{
					Add(ship, ECombatModifier.Weather, -weather, 0);
				}

				if (isNight)
				{
					string kind = side.IsAttacker ? CCombatModifiers.AttackOf : CCombatModifiers.DefenceOf;
					long night = defines[CManpower.MilitarySection, CCombatModifiers.NightPenalty].Raw + Adjuster(ship, kind)
						+ nightTrait;
					if (night < 0)
					{
						Add(ship, ECombatModifier.Night, night, 0);
					}
				}

				if (leader is { } bonus)
				{
					Add(ship, ECombatModifier.Leader, bonus, bonus);
				}
			}
		}
	}

	/// <summary>The ship's own figure for the night, attack or defence.</summary>
	private static long Adjuster(CSubUnit ship, string kind)
	{
		return ship.Stats.Adjusters.TryGetValue(CCombatModifiers.NightAdjuster, out var night) ? night[kind].Raw : 0;
	}

	/// <summary>Notes one thing on a ship and multiplies it into what the hour makes of it.</summary>
	private static void Add(CSubUnit ship, ECombatModifier kind, long attack, long defence)
	{
		ship.CombatModifiers.Add(new CCombatModifier(kind, new CFixedPoint(attack), new CFixedPoint(defence)));
		ship.CombatAttack = new CFixedPoint(ship.CombatAttack.Raw * Math.Max(CFixedPoint.Scale + attack, LeastFactor) / CFixedPoint.Scale);
		ship.CombatDefence = new CFixedPoint(ship.CombatDefence.Raw * Math.Max(CFixedPoint.Scale + defence, LeastFactor) / CFixedPoint.Scale);
	}
}
