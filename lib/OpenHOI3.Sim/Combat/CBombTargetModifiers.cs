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
/// What an hour of a bombing makes of every brigade, ship and wing under the bombs. Of
/// all of it only a ship's attack is ever asked for, by its own fire at the bombers: a
/// land unit fires by its unit's figures, which these do not touch, and nothing in a
/// bombing reads a defence - so the fort and the screens are noted here and decide
/// nothing. The original also counts difficulty, which is not built. See
/// <c>docs/mechanics/bombing.md</c>, section 9, and <c>docs/mechanics/leaders.md</c>,
/// section 10, for what the leaders bring.
/// </summary>
public static class CBombTargetModifiers
{
	/// <summary>The technology figure that is taken off a side's crowding.</summary>
	public const string FreeStacking = "unit_cooperation";

	/// <summary>What each whole level of fort adds to the defence, in thousandths.</summary>
	private const long PerFortLevel = 100;

	/// <summary>What each brigade, ship or wing on the side takes off, in thousandths.</summary>
	private const long PerSubUnit = 10;

	public static void Apply(CGameState state, CCombat bombing)
	{
		CCombatSide side = bombing.Defender;
		CProvince province = state.Province(bombing.Province);
		CDefines defines = state.Defines;
		string military = CManpower.MilitarySection;
		bool isNight = CDaylight.IsNight(state, province);

		long subUnits = 0;
		foreach (CUnit unit in side.Units)
		{
			subUnits += unit.SubUnits.Count;
		}
		long crowd = subUnits * CFixedPoint.Scale * PerSubUnit / CFixedPoint.Scale;
		long fort = province.Modifier[CCombatModifiers.FortLevel].Raw / CFixedPoint.Scale * CFixedPoint.Scale
			* PerFortLevel / CFixedPoint.Scale;

		foreach (CUnit unit in side.Units)
		{
			CCountry? owner = state.Country(unit.Owner);
			long poorScreens = unit.Kind == EUnitKind.Navy ? PoorScreens(unit) : 0;
			long missionEfficiency = CMissionEfficiency.Of(state, owner, unit.Order);
			long crowding = crowd - (owner is null ? 0 : owner.Technology[FreeStacking].Raw);
			long experienceTrait = CCommandReach.Effect(state, unit, ETraitEffect.ExperienceBonus);
			long supplyTrait = CCommandReach.Effect(state, unit, ETraitEffect.OutOfSupplyModifier);
			long nightTrait = CCommandReach.Effect(state, unit, ETraitEffect.NightAttack);
			long? leader = CCombatLeaders.Bonus(state, unit);

			// A unit of this side is no longer what its last battle made of it: the hour
			// begins by wiping that, as every kind of battle's does.
			unit.CombatModifiers.Clear();
			unit.CombatAttack = CFixedPoint.One;
			unit.CombatDefence = CFixedPoint.One;
			unit.DefencesUsed = 0;

			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				subUnit.CombatModifiers.Clear();
				subUnit.CombatAttack = CFixedPoint.One;
				subUnit.CombatDefence = CFixedPoint.One;

				if (poorScreens > 0 && CShipCensus.IsCapital(subUnit))
				{
					CBomberModifiers.Add(subUnit, ECombatModifier.PoorScreens, 0, -poorScreens);
				}
				if (fort != 0)
				{
					CBomberModifiers.Add(subUnit, ECombatModifier.Fort, 0, fort);
				}

				long experience = Math.Max(0, subUnit.Experience.Raw);
				if (subUnit.IsPride)
				{
					experience += defines[military, CNavalModifiers.PrideBonus].Raw;
				}
				experience = experience * 10 / CFixedPoint.Scale;
				if (experience != 0)
				{
					experience = CCombatModifiers.Scaled(experience, experienceTrait);
					CBomberModifiers.Add(subUnit, ECombatModifier.Experience, experience, experience);
				}

				if (missionEfficiency != 0)
				{
					CBomberModifiers.Add(subUnit, ECombatModifier.MissionEfficiency, missionEfficiency, missionEfficiency);
				}
				if (unit.SuppliesReceived != CFixedPoint.One)
				{
					long missing = CFixedPoint.Scale - unit.SuppliesReceived.Raw;
					long impact = defines[military, CCombatModifiers.SupplyLackImpact].Raw * missing / CFixedPoint.Scale;
					impact = CCombatModifiers.Scaled(impact, supplyTrait);
					CBomberModifiers.Add(subUnit, ECombatModifier.LackOfSupplies, -impact, -impact);
				}
				long radio = owner is null ? 0 : owner.Technology[CBomberModifiers.CombatEfficiency].Raw;
				if (radio != 0)
				{
					CBomberModifiers.Add(subUnit, ECombatModifier.Radio, radio, radio);
				}
				if (owner is not null && owner.Dissent != CFixedPoint.Zero)
				{
					long dissent = owner.Dissent.Raw * 10 / CFixedPoint.Scale;
					long impact = defines[military, CCombatModifiers.DissentImpact].Raw * dissent / CFixedPoint.Scale;
					CBomberModifiers.Add(subUnit, ECombatModifier.Dissent, -impact, -impact);
				}
				if (crowding > 0)
				{
					CBomberModifiers.Add(subUnit, ECombatModifier.Stacking, -crowding, -crowding);
				}
				if (isNight)
				{
					string kind = side.IsAttacker ? CCombatModifiers.AttackOf : CCombatModifiers.DefenceOf;
					long night = defines[military, CCombatModifiers.NightPenalty].Raw + CBomberModifiers.Adjuster(subUnit, kind)
						+ nightTrait;
					if (night < 0)
					{
						CBomberModifiers.Add(subUnit, ECombatModifier.Night, night, 0);
					}
				}
				if (leader is { } bonus)
				{
					CBomberModifiers.Add(subUnit, ECombatModifier.Leader, bonus, bonus);
				}
			}
		}
	}

	/// <summary>
	/// What a fleet's capital ships lose for want of screens: a third of the share of
	/// them that has no screen of its own.
	/// </summary>
	private static long PoorScreens(CUnit fleet)
	{
		int capitals = 0;
		int screens = 0;
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (CShipCensus.IsCapital(ship))
			{
				capitals++;
			}
			else if (!CShipCensus.IsSubmarine(ship) && !CShipCensus.IsTransport(ship))
			{
				screens++;
			}
		}
		if (capitals <= 0 || screens >= capitals)
		{
			return 0;
		}
		long share = screens * (long)CFixedPoint.Scale * CFixedPoint.Scale / (capitals * (long)CFixedPoint.Scale);
		return (CFixedPoint.Scale - share) * CFixedPoint.Scale / 3000;
	}
}
