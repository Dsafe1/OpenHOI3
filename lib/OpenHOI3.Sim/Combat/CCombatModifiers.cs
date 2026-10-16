using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What an hour's circumstances make of each unit's attack and defence in a battle.
/// Every round starts by working them out afresh: each unit's list is emptied, and each
/// thing that holds for or against it this hour is put on the list and multiplied in -
/// a tenth more is a factor of 1.1 - with no single one taking a unit below a hundredth.
/// See <c>docs/mechanics/landcombat.md</c>, section 4.
/// </summary>
public static class CCombatModifiers
{
	public const string RiverPenalty = "RIVER_CROSSING_PENALTY";
	public const string LandingPenalty = "AMPHIBIOUS_LANDING_PENALTY";
	public const string ParadropPenalty = "PARADROP_PENALTY";
	public const string FortPenalty = "BASE_FORT_PENALTY";
	public const string MultipleCombatsPenalty = "MULTIPLE_COMBATS_PENALTY";
	public const string DissentImpact = "COMBAT_DISSENT_IMPACT";
	public const string SupplyLackImpact = "COMBAT_SUPPLY_LACK_IMPACT";
	public const string DigInFactor = "DIG_IN_FACTOR";
	public const string StackingPenalty = "BASE_STACKING_PENALTY";
	public const string NightPenalty = "BASE_NIGHT_PENALTY";
	public const string EnvelopmentPenalty = "ENVELOPMENT_PENALTY";
	public const string LeaderImpact = "COMBAT_LEADER_IMPACT";

	/// <summary>The country modifier a defending unit's leader bonus is made more or less by.</summary>
	public const string LeaderDefence = "leader_defence";

	/// <summary>What each point of the theatre commander's skill takes off the stacking penalty, from a float of 10.5.</summary>
	private const long StackingPerSkill = 10;

	/// <summary>The adjusters of a unit type, and the two figures of one that a battle reads.</summary>
	public const string RiverAdjuster = "river";
	public const string AmphibiousAdjuster = "amphibious";
	public const string FortAdjuster = "fort";
	public const string NightAdjuster = "night";
	public const string AttackOf = "attack";
	public const string DefenceOf = "defence";

	/// <summary>What a province's forts come to, and the country modifier a unit fights by on its own cores.</summary>
	public const string FortLevel = "fort_level";
	public const string CoastalFortLevel = "coastal_fort_level";
	public const string TerritorialPride = "territorial_pride";

	/// <summary>The least any one thing leaves of a unit's attack or defence, in thousandths.</summary>
	private const long LeastFactor = 10;

	/// <summary>The most a landing may be made worse by, in thousandths.</summary>
	private const long WorstLanding = -990;

	/// <summary>How many units a side may bring from each direction before they crowd one another.</summary>
	private const int UnitsPerDirection = 3;

	/// <summary>How many times over the stacking penalty is counted at most.</summary>
	private const int MostStacked = 100;

	/// <summary>A defender attacked from more directions than this is enveloped.</summary>
	private const int DirectionsBeforeEnvelopment = 2;

	public static void Apply(CGameState state, CCombat combat, CCombatSide side)
	{
		CProvince province = state.Province(combat.Province);
		int directions = Directions(combat);
		long stacking = Stacking(state, side, directions);
		bool isNight = CDaylight.IsNight(state, province);
		bool isCutOff = IsIsolatedLand(state, province);
		CDefines defines = state.Defines;

		foreach (CUnit unit in side.Units)
		{
			unit.CombatModifiers.Clear();
			unit.CombatAttack = CFixedPoint.One;
			unit.CombatDefence = CFixedPoint.One;
			unit.DefencesUsed = 0;
			// A unit landing on an enemy's coast has its two scales by its progress, attacker or not.
			Transport.CLanding.SetScales(state, unit);
			if (state.Country(unit.Owner) is not { } owner)
			{
				continue;
			}

			if (side.IsAttacker)
			{
				Assault(state, province, unit);
			}

			if (!side.IsAttacker && unit.Combats.Count > 1)
			{
				Add(unit, ECombatModifier.MultipleCombats, -defines[CManpower.MilitarySection, MultipleCombatsPenalty].Raw);
			}

			// A tenth of a hundredth for each point of average experience.
			long experience = CUnitTotals.AverageExperienceWithPride(state, unit).Raw * 10 / CFixedPoint.Scale;
			if (experience != 0)
			{
				Add(unit, ECombatModifier.Experience, Scaled(experience, Trait(state, unit, ETraitEffect.ExperienceBonus)));
			}

			long combinedArms = CombinedArms(state, owner, unit);
			if (combinedArms != 0)
			{
				Add(unit, ECombatModifier.CombinedArms, combinedArms);
			}

			if (owner.Dissent != CFixedPoint.Zero)
			{
				long dissent = owner.Dissent.Raw * 10 / CFixedPoint.Scale;
				dissent = defines[CManpower.MilitarySection, DissentImpact].Raw * dissent / CFixedPoint.Scale;
				Add(unit, ECombatModifier.Dissent, -Scaled(dissent, Trait(state, unit, ETraitEffect.DissentImpact)));
			}

			// A dropped unit is spared the lack (transport.md, section 13.4).
			if (unit.SuppliesReceived != CFixedPoint.One && !unit.WasParadropped)
			{
				long missing = CFixedPoint.Scale - unit.SuppliesReceived.Raw;
				long lack = defines[CManpower.MilitarySection, SupplyLackImpact].Raw * missing / CFixedPoint.Scale;
				Add(unit, ECombatModifier.LackOfSupplies, -Scaled(lack, Trait(state, unit, ETraitEffect.OutOfSupplyModifier)));
			}

			if (!side.IsAttacker && unit.DigIn > CFixedPoint.Zero)
			{
				long dug = unit.DigIn.Raw * defines[CManpower.MilitarySection, DigInFactor].Raw / CFixedPoint.Scale;
				Add(unit, ECombatModifier.DugIn, Scaled(dug, Trait(state, unit, ETraitEffect.DiginBonus)));
			}

			// The theatre's commander takes a hundredth off for each point of his skill,
			// however far off he is.
			long crowded = stacking - TheatreSkill(unit) * StackingPerSkill;
			if (crowded > 0)
			{
				Add(unit, ECombatModifier.Stacking, -crowded);
			}

			if (province.Terrain is { } terrain)
			{
				string kind = side.IsAttacker ? AttackOf : DefenceOf;
				long ground = (side.IsAttacker ? terrain.Attack : terrain.Defence).Raw
					+ CUnitTotals.Adjuster(unit, terrain.Name, kind);
				if (ground != 0)
				{
					Add(unit, ECombatModifier.Terrain, ground);
				}
			}

			// The weather and the night are against a unit's attack and leave its defence alone.
			long weather = CWeatherEffects.LandCombat(state, province.Weather, province.Terrain, owner);
			if (weather != 0)
			{
				Add(unit, ECombatModifier.Weather, -weather, 0);
			}

			if (isNight)
			{
				long night = defines[CManpower.MilitarySection, NightPenalty].Raw
					+ CUnitTotals.Adjuster(unit, NightAdjuster, AttackOf)
					+ Trait(state, unit, ETraitEffect.NightAttack);
				if (night < 0)
				{
					Add(unit, ECombatModifier.Night, night, 0);
				}
			}

			LeaderBonus(state, province, side, owner, unit);

			if (!side.IsAttacker && directions > DirectionsBeforeEnvelopment && !isCutOff)
			{
				long envelopment = (directions - DirectionsBeforeEnvelopment) * (long)CFixedPoint.Scale
					* defines[CManpower.MilitarySection, EnvelopmentPenalty].Raw / CFixedPoint.Scale;
				envelopment = Scaled(envelopment, Trait(state, unit, ETraitEffect.EnvelopmentBonus));
				Add(unit, ECombatModifier.Envelopment, -Math.Max(envelopment, 0));
			}

			if (province.Cores.Contains(unit.Owner))
			{
				Add(unit, ECombatModifier.TerritorialPride, owner.Modifier[TerritorialPride].Raw);
			}
		}
	}

	/// <summary>
	/// What only an attacker meets: a river on its way, or a landing - from the sea, or over
	/// a strait; and the province's forts - its coastal ones where it is landing. A landing
	/// from aboard a fleet has its penalty cut by the fleet's invasion defence
	/// (<c>transport.md</c>, section 9).
	/// </summary>
	private static void Assault(CGameState state, CProvince province, CUnit unit)
	{
		CDefines defines = state.Defines;
		// Standing in the sea is a landing, unless it is the rebels'; otherwise the edge it attacks over says.
		bool fromTheSea = state.Province(unit.Location).IsSea;
		bool isLanding = fromTheSea && !Revolts.CRebelFactions.IsRebels(unit.Owner);
		CProvinceEdge? edge = fromTheSea || unit.Location == province.Id
			? null
			: state.Province(unit.Location).EdgeTo(province.Id);
		if (edge is { Kind: EProvinceEdgeKind.River })
		{
			long river = defines[CManpower.MilitarySection, RiverPenalty].Raw
				+ CUnitTotals.Adjuster(unit, RiverAdjuster, AttackOf)
				+ Trait(state, unit, ETraitEffect.RiverAttack);
			if (river < 0)
			{
				Add(unit, ECombatModifier.River, river);
			}
		}
		else if (edge is { Kind: EProvinceEdgeKind.Crossing })
		{
			isLanding = true;
		}

		// A dropped unit has the paradrop's penalty and none for landing (transport.md, section 13.4).
		if (unit.WasParadropped)
		{
			long paradrop = defines[CManpower.MilitarySection, ParadropPenalty].Raw
				+ Trait(state, unit, ETraitEffect.ParadropMission);
			if (paradrop < 0)
			{
				Add(unit, ECombatModifier.Paratroop, paradrop);
			}
			isLanding = false;
		}

		if (isLanding)
		{
			long landing = defines[CManpower.MilitarySection, LandingPenalty].Raw
				+ CUnitTotals.Adjuster(unit, AmphibiousAdjuster, AttackOf)
				+ Trait(state, unit, ETraitEffect.AmphAttack);
			if (landing < 0)
			{
				landing = Transport.CLanding.CutPenalty(state, unit, landing);
			}
			landing = Math.Max(landing, WorstLanding);
			if (landing < 0)
			{
				Add(unit, ECombatModifier.Amphibious, landing);
			}
		}

		// Whole levels only: a fort half built is no fort.
		long level = province.Modifier[isLanding ? CoastalFortLevel : FortLevel].Raw / CFixedPoint.Scale * CFixedPoint.Scale;
		if (level > 0)
		{
			long fort = level * defines[CManpower.MilitarySection, FortPenalty].Raw / CFixedPoint.Scale
				+ Trait(state, unit, ETraitEffect.FortAttack)
				+ CUnitTotals.Adjuster(unit, FortAdjuster, AttackOf);
			fort = Math.Min(fort, 0);
			if (fort != 0)
			{
				Add(unit, ECombatModifier.Fort, fort);
			}
		}
	}

	/// <summary>
	/// What the unit's leader adds, to its attack and its defence alike: his skill times
	/// <c>COMBAT_LEADER_IMPACT</c>, and the traits that come down to the unit for the side
	/// it fights on - the offensive one and the one for the province's ground when it
	/// attacks; the defensive one and the ground's, more or less by its country's
	/// <c>leader_defence</c>, when it defends. Only a leader with the rank for the unit and
	/// some skill counts; without him, his commanders' traits count for nothing here either.
	/// </summary>
	private static void LeaderBonus(CGameState state, CProvince province, CCombatSide side, CCountry owner, CUnit unit)
	{
		if (!CLeaders.HasRankFor(state, unit) || unit.Leader is not { Skill: > 0 } leader)
		{
			return;
		}
		long skill = leader.Skill * CFixedPoint.Scale * state.Defines[CManpower.MilitarySection, LeaderImpact].Raw / CFixedPoint.Scale;
		string ground = province.Terrain?.Name ?? "";
		long bonus;
		if (side.IsAttacker)
		{
			bonus = CCommandReach.TerrainEffect(state, unit, ETraitEffect.TerrainAttack, ground)
				+ Trait(state, unit, ETraitEffect.OffenceModifier) + skill;
		}
		else
		{
			bonus = Trait(state, unit, ETraitEffect.DefenceModifier)
				+ CCommandReach.TerrainEffect(state, unit, ETraitEffect.TerrainDefence, ground) + skill;
			bonus = bonus * (CFixedPoint.Scale + owner.Modifier[LeaderDefence].Raw) / CFixedPoint.Scale;
		}
		Add(unit, ECombatModifier.Leader, bonus);
	}

	/// <summary>The skill of the leader of the theatre a unit is under, or is: nothing where there is none.</summary>
	private static long TheatreSkill(CUnit unit)
	{
		for (CUnit? above = unit; above is not null; above = above.Parent)
		{
			if (above.Kind == EUnitKind.Theatre)
			{
				return above.Leader?.Skill ?? 0;
			}
		}
		return 0;
	}

	/// <summary>What one kind of trait effect comes to for a unit, down its chain of command.</summary>
	private static long Trait(CGameState state, CUnit unit, ETraitEffect kind)
	{
		return CCommandReach.Effect(state, unit, kind);
	}

	/// <summary>A figure made more or less by a trait: figure x (1 + trait).</summary>
	public static long Scaled(long figure, long trait)
	{
		return figure * (CFixedPoint.Scale + trait) / CFixedPoint.Scale;
	}

	/// <summary>How many provinces the attackers stand in: the directions the battle is fought from.</summary>
	public static int Directions(CCombat combat)
	{
		var seen = new List<int>();
		foreach (CUnit unit in combat.Attacker.Units)
		{
			if (!seen.Contains(unit.Location))
			{
				seen.Add(unit.Location);
			}
		}
		return seen.Count;
	}

	/// <summary>
	/// What crowding takes off every unit of a side, in thousandths: its front and the
	/// reserves that could fight, less three for each direction of the attack, and the
	/// penalty counted once for each unit after the first that leaves.
	/// </summary>
	public static long Stacking(CGameState state, CCombatSide side, int directions)
	{
		int crowd = side.Front.Count - UnitsPerDirection * directions;
		foreach (CUnit unit in side.Reserves)
		{
			if (CUnitTotals.CanFight(unit))
			{
				crowd++;
			}
		}
		long each = CFixedPoint.Scale + state.Defines[CManpower.MilitarySection, StackingPenalty].Raw;
		long left = CFixedPoint.Scale;
		for (int counted = 1; counted < crowd && counted < MostStacked; counted++)
		{
			left = each * left / CFixedPoint.Scale;
		}
		return CFixedPoint.Scale - left;
	}

	/// <summary>
	/// What a unit's mix of arms is worth, in thousandths, with what the leaders'
	/// <c>combined_arms_bonus</c> comes to for it, or nothing for a unit with no brigade of
	/// a kind that makes the mix count.
	/// </summary>
	private static long CombinedArms(CGameState state, CCountry owner, CUnit unit)
	{
		List<CSubUnitType> types = CountedTypes(unit);
		return CCombinedArms.BaseBonus(state, types) > CFixedPoint.Zero
			? CCombinedArms.Bonus(state, owner, types).Raw + Trait(state, unit, ETraitEffect.CombinedArmsBonus)
			: 0;
	}

	/// <summary>Whether a unit's brigades make combined arms at all: its base bonus is above nothing.</summary>
	public static bool HasCombinedArms(CGameState state, CUnit unit)
	{
		return CCombinedArms.BaseBonus(state, CountedTypes(unit)) > CFixedPoint.Zero;
	}

	private static List<CSubUnitType> CountedTypes(CUnit unit)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				types.Add(subUnit.Type);
			}
		}
		return types;
	}

	/// <summary>Land with no land beside it. A defender there cannot be enveloped.</summary>
	public static bool IsIsolatedLand(CGameState state, CProvince province)
	{
		if (province.IsSea)
		{
			return false;
		}
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (!state.Province(edge.To).IsSea)
			{
				return false;
			}
		}
		return true;
	}

	private static void Add(CUnit unit, ECombatModifier kind, long both)
	{
		Add(unit, kind, both, both);
	}

	/// <summary>Puts one thing on a unit's list and multiplies it into what the unit fights with.</summary>
	private static void Add(CUnit unit, ECombatModifier kind, long attack, long defence)
	{
		unit.CombatModifiers.Add(new CCombatModifier(kind, new CFixedPoint(attack), new CFixedPoint(defence)));
		unit.CombatAttack = new CFixedPoint(Math.Max(attack + CFixedPoint.Scale, LeastFactor) * unit.CombatAttack.Raw / CFixedPoint.Scale);
		unit.CombatDefence = new CFixedPoint(unit.CombatDefence.Raw * Math.Max(defence + CFixedPoint.Scale, LeastFactor) / CFixedPoint.Scale);
	}
}
