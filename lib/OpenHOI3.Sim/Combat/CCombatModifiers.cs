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
	public const string EncircledPenalty = "ENCIRCLED_PENALTY";
	public const string LeaderImpact = "COMBAT_LEADER_IMPACT";
	public const string ShoreBombardmentMod = "SHORE_BOMBARDMENT_MOD";
	public const string ShoreBombardmentCap = "SHORE_BOMBARDMENT_CAP";

	/// <summary>A unit type's figure for what it does to a shore from the sea beside it.</summary>
	public const string ShoreBombardmentStatistic = "shore_bombardment";

	/// <summary>The country modifier a defending unit's leader bonus is made more or less by.</summary>
	public const string LeaderDefence = "leader_defence";

	/// <summary>The technology figure taken off the stacking penalty before the theatre commander's skill.</summary>
	public const string FreeStacking = "unit_cooperation";

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

	/// <summary>
	/// What a battle's hour holds for every unit of a side alike, worked out once for the side: how many provinces the
	/// attackers come from, the side's crowding before anything of a unit's own comes off it, the night and whether the
	/// province is cut off, what enemy fleets beside it do to the side, and whether an attacker comes from the sea.
	/// </summary>
	public readonly record struct Setting(int Directions, long Stacking, bool IsNight, bool IsCutOff, long Shore, bool FromTheSea);

	/// <summary>What the terms of one unit's hour are put on: its list, where one is kept, and the two products.</summary>
	private sealed class CTerms
	{
		private readonly List<CCombatModifier>? _list;

		public CTerms(List<CCombatModifier>? list)
		{
			_list = list;
		}

		public long Attack { get; private set; } = CFixedPoint.Scale;

		public long Defence { get; private set; } = CFixedPoint.Scale;

		/// <summary>Puts one thing on the list and multiplies it into what the unit fights with.</summary>
		public void Add(ECombatModifier kind, long attack, long defence)
		{
			_list?.Add(new CCombatModifier(kind, new CFixedPoint(attack), new CFixedPoint(defence)));
			Attack = Math.Max(attack + CFixedPoint.Scale, LeastFactor) * Attack / CFixedPoint.Scale;
			Defence = Defence * Math.Max(defence + CFixedPoint.Scale, LeastFactor) / CFixedPoint.Scale;
		}
	}

	public static void Apply(CGameState state, CCombat combat, CCombatSide side)
	{
		CProvince province = state.Province(combat.Province);
		int directions = Directions(combat);
		var setting = new Setting(directions, Stacking(state, side, directions), CDaylight.IsNight(state, province),
			IsIsolatedLand(state, province), ShoreBombardment(state, province, side),
			!side.IsAttacker && state.Rules.Has(Rules.EFix.TraitFortDefence) && FromTheSea(state, combat.Attacker.Units));

		foreach (CUnit unit in side.Units)
		{
			unit.CombatModifiers.Clear();
			unit.CombatAttack = CFixedPoint.One;
			unit.CombatDefence = CFixedPoint.One;
			unit.HasCombatProducts = true;
			unit.DefencesUsed = 0;
			// A unit landing on an enemy's coast has its two scales by its progress, attacker or not.
			Transport.CLanding.SetScales(state, unit);
			if (state.Country(unit.FiguresOwner) is not { } owner)
			{
				continue;
			}
			var terms = new CTerms(unit.CombatModifiers);
			Terms(state, province, unit, owner, side.IsAttacker, setting, terms);
			unit.CombatAttack = new CFixedPoint(terms.Attack);
			unit.CombatDefence = new CFixedPoint(terms.Defence);
		}
	}

	/// <summary>
	/// The defence product a battle's hour over a province would give a unit, with nothing of the unit touched: every
	/// term <see cref="Apply"/> gives it, multiplied as there. For the AI's odds of a battle not yet fought, under an
	/// enhanced game's fix <c>ai_odds_fresh_defence</c> (<c>docs/mechanics/moving.md</c>, section 11.4). One for a unit
	/// whose figures are nobody's.
	/// </summary>
	public static long DefenceProduct(CGameState state, CProvince province, CUnit unit, bool isAttacker, Setting setting)
	{
		if (state.Country(unit.FiguresOwner) is not { } owner)
		{
			return CFixedPoint.Scale;
		}
		var terms = new CTerms(null);
		Terms(state, province, unit, owner, isAttacker, setting, terms);
		return terms.Defence;
	}

	/// <summary>Whether any of the units stands at sea: an attack from there meets the coastal fort.</summary>
	public static bool FromTheSea(CGameState state, IReadOnlyList<CUnit> attackers)
	{
		foreach (CUnit attacker in attackers)
		{
			if (attacker.Location > 0 && state.Province(attacker.Location).IsSea)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Every term of one unit's hour, in the battle's order. It reads the unit and changes nothing of it.</summary>
	private static void Terms(CGameState state, CProvince province, CUnit unit, CCountry owner, bool isAttacker,
		Setting setting, CTerms terms)
	{
		CDefines defines = state.Defines;
		if (isAttacker)
		{
			Assault(state, province, unit, terms);
		}
		else if (state.Rules.Has(Rules.EFix.TraitFortDefence))
		{
			FortDefence(state, province, setting.FromTheSea, unit, terms);
		}

		if (setting.Shore != 0)
		{
			Add(terms, ECombatModifier.ShoreBombardment, setting.Shore);
		}

		if (!isAttacker && unit.Combats.Count > 1)
		{
			Add(terms, ECombatModifier.MultipleCombats, -defines[CManpower.MilitarySection, MultipleCombatsPenalty].Raw);
		}

		// A played country's unit, by the difficulty (frontend.md, section 4).
		long difficulty = Settings.CDifficulty.Combat(state, owner);
		if (difficulty != 0)
		{
			Add(terms, ECombatModifier.Difficulty, difficulty);
		}

		// A tenth of a hundredth for each point of average experience.
		long experience = CUnitTotals.AverageExperienceWithPride(state, unit).Raw * 10 / CFixedPoint.Scale;
		if (experience != 0)
		{
			Add(terms, ECombatModifier.Experience, Scaled(experience, Trait(state, unit, ETraitEffect.ExperienceBonus)));
		}

		// How good the country is at the unit's order - a plain move counting on land, where at sea and in the air it does
		// not (reversing/findings/FINDINGS-combatmods.md, BM_MISSION_EFFICIENCY; landcombat.md, section 4).
		long mission = Missions.CMissionEfficiency.Of(state, owner, unit.Order);
		if (mission != 0)
		{
			Add(terms, ECombatModifier.MissionEfficiency, mission);
		}

		long combinedArms = CombinedArms(state, owner, unit);
		if (combinedArms != 0)
		{
			Add(terms, ECombatModifier.CombinedArms, combinedArms);
		}

		if (owner.Dissent != CFixedPoint.Zero)
		{
			long dissent = owner.Dissent.Raw * 10 / CFixedPoint.Scale;
			dissent = defines[CManpower.MilitarySection, DissentImpact].Raw * dissent / CFixedPoint.Scale;
			Add(terms, ECombatModifier.Dissent, -Scaled(dissent, Trait(state, unit, ETraitEffect.DissentImpact)));
		}

		// A dropped unit is spared the lack (transport.md, section 13.4).
		if (unit.SuppliesReceived != CFixedPoint.One && !unit.WasParadropped)
		{
			long missing = CFixedPoint.Scale - unit.SuppliesReceived.Raw;
			long lack = defines[CManpower.MilitarySection, SupplyLackImpact].Raw * missing / CFixedPoint.Scale;
			Add(terms, ECombatModifier.LackOfSupplies, -Scaled(lack, Trait(state, unit, ETraitEffect.OutOfSupplyModifier)));
		}

		if (!isAttacker && unit.DigIn > CFixedPoint.Zero)
		{
			long dug = unit.DigIn.Raw * defines[CManpower.MilitarySection, DigInFactor].Raw / CFixedPoint.Scale;
			Add(terms, ECombatModifier.DugIn, Scaled(dug, Trait(state, unit, ETraitEffect.DiginBonus)));
		}

		// The country's unit_cooperation comes off it, and the theatre's commander takes a hundredth off for each
		// point of his skill, however far off he is (reversing/findings/FINDINGS-combatmods.md, BM_DIVISION_PENALTY).
		long crowded = setting.Stacking - owner.Technology[FreeStacking].Raw - TheatreSkill(unit) * StackingPerSkill;
		if (crowded > 0)
		{
			Add(terms, ECombatModifier.Stacking, -crowded);
		}

		if (province.Terrain is { } terrain)
		{
			string kind = isAttacker ? AttackOf : DefenceOf;
			long ground = (isAttacker ? terrain.Attack : terrain.Defence).Raw
				+ CUnitTotals.Adjuster(unit, terrain.Name, kind);
			if (ground != 0)
			{
				Add(terms, ECombatModifier.Terrain, ground);
			}
		}

		// The weather and the night are against a unit's attack and leave its defence alone.
		long weather = CWeatherEffects.LandCombat(state, province.Weather, province.Terrain, owner);
		if (weather != 0)
		{
			terms.Add(ECombatModifier.Weather, -weather, 0);
		}

		if (setting.IsNight)
		{
			long night = defines[CManpower.MilitarySection, NightPenalty].Raw
				+ CUnitTotals.Adjuster(unit, NightAdjuster, AttackOf)
				+ Trait(state, unit, ETraitEffect.NightAttack);
			if (night < 0)
			{
				terms.Add(ECombatModifier.Night, night, 0);
			}
		}

		LeaderBonus(state, province, isAttacker, owner, unit, terms);

		if (!isAttacker && setting.Directions > DirectionsBeforeEnvelopment && !setting.IsCutOff)
		{
			long envelopment = (setting.Directions - DirectionsBeforeEnvelopment) * (long)CFixedPoint.Scale
				* defines[CManpower.MilitarySection, EnvelopmentPenalty].Raw / CFixedPoint.Scale;
			envelopment = Scaled(envelopment, Trait(state, unit, ETraitEffect.EnvelopmentBonus));
			Add(terms, ECombatModifier.Envelopment, -Math.Max(envelopment, 0));
		}

		if (!isAttacker && !province.IsSea && IsEncircled(state, province, unit.Owner))
		{
			long encircled = Scaled(defines[CManpower.MilitarySection, EncircledPenalty].Raw,
				Trait(state, unit, ETraitEffect.EncirclementBonus));
			Add(terms, ECombatModifier.Encirclement, -Math.Max(encircled, 0));
		}

		if (province.Cores.Contains(unit.Owner))
		{
			Add(terms, ECombatModifier.TerritorialPride, owner.Modifier[TerritorialPride].Raw);
		}
	}

	/// <summary>
	/// What enemy fleets in the seas beside the battle do to a side, worked out once for it: every unit in each sea
	/// province the battle's province has an edge to that is going nowhere, and whose owner is not the owner of the side's
	/// first unit but at war with it - or either is the rebels - gives its brigades' <c>shore_bombardment</c> times
	/// <c>SHORE_BOMBARDMENT_MOD</c>, each truncated; the sum is held at <c>SHORE_BOMBARDMENT_CAP</c>, the defines being
	/// negative. Nothing asks whether the side has a fleet of its own (<c>landcombat.md</c>, section 4).
	/// </summary>
	public static long ShoreBombardment(CGameState state, CProvince province, CCombatSide side)
	{
		return ShoreBombardment(state, province, side.Units);
	}

	/// <summary>The same for the units a side would be, the first of them standing for it.</summary>
	public static long ShoreBombardment(CGameState state, CProvince province, IReadOnlyList<CUnit> units)
	{
		if (units.Count == 0)
		{
			return 0;
		}
		CCountryTag first = units[0].Owner;
		CDefines defines = state.Defines;
		long factor = defines[CManpower.MilitarySection, ShoreBombardmentMod].Raw;
		long shore = 0;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (!state.Province(edge.To).IsSea)
			{
				continue;
			}
			foreach (CUnit fleet in state.UnitsIn(edge.To))
			{
				if (fleet.Path.Count > 0 || fleet.Owner == first)
				{
					continue;
				}
				if (!CCountryLists.AtWar(state, fleet.Owner, first)
					&& !Revolts.CRebelFactions.IsRebels(first) && !Revolts.CRebelFactions.IsRebels(fleet.Owner))
				{
					continue;
				}
				shore += CUnitTotals.Sum(fleet, ShoreBombardmentStatistic).Raw * factor / CFixedPoint.Scale;
			}
		}
		return Math.Max(shore, defines[CManpower.MilitarySection, ShoreBombardmentCap].Raw);
	}

	/// <summary>
	/// What only an attacker meets: a river on its way, or a landing - from the sea, or over
	/// a strait; and the province's forts - its coastal ones where it is landing. A landing
	/// from aboard a fleet has its penalty cut by the fleet's invasion defence
	/// (<c>transport.md</c>, section 9).
	/// </summary>
	private static void Assault(CGameState state, CProvince province, CUnit unit, CTerms terms)
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
				Add(terms, ECombatModifier.River, river);
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
				Add(terms, ECombatModifier.Paratroop, paradrop);
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
				Add(terms, ECombatModifier.Amphibious, landing);
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
				Add(terms, ECombatModifier.Fort, fort);
			}
		}
	}

	/// <summary>
	/// An enhanced game's fix <c>trait_fort_defence</c>: a defender behind a fort - a whole level of the land fort, or
	/// of the coastal fort where an attacker comes from the sea - has its leaders' <c>fort_defence</c> on its attack and
	/// its defence, under the fort's own name. The original reaches the trait only for an attacker, where it does nothing.
	/// </summary>
	private static void FortDefence(CGameState state, CProvince province, bool fromTheSea, CUnit unit, CTerms terms)
	{
		bool behind = province.Modifier[FortLevel].Raw >= CFixedPoint.Scale
			|| (fromTheSea && province.Modifier[CoastalFortLevel].Raw >= CFixedPoint.Scale);
		long trait = behind ? Trait(state, unit, ETraitEffect.FortDefence) : 0;
		if (trait != 0)
		{
			Add(terms, ECombatModifier.Fort, trait);
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
	private static void LeaderBonus(CGameState state, CProvince province, bool isAttacker, CCountry owner, CUnit unit,
		CTerms terms)
	{
		if (!CLeaders.HasRankFor(state, unit) || unit.Leader is not { Skill: > 0 } leader)
		{
			return;
		}
		long skill = leader.Skill * CFixedPoint.Scale * state.Defines[CManpower.MilitarySection, LeaderImpact].Raw / CFixedPoint.Scale;
		string ground = province.Terrain?.Name ?? "";
		long bonus;
		if (isAttacker)
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
		Add(terms, ECombatModifier.Leader, bonus);
	}

	/// <summary>The skill of the leader of the theatre a unit is under, or is: nothing where there is none.</summary>
	public static long TheatreSkill(CUnit unit)
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
	public static long Trait(CGameState state, CUnit unit, ETraitEffect kind)
	{
		return CCommandReach.Effect(state, unit, kind);
	}

	/// <summary>
	/// What dissent costs a unit at sea, in the air or under bombs: the figure as it is, the original reading a
	/// leader's <c>dissent_impact</c> on land alone - and scaled by it with an enhanced game's fix
	/// <c>trait_dissent_impact</c>.
	/// </summary>
	public static long DissentOffLand(CGameState state, CUnit unit, long impact)
	{
		return state.Rules.Has(Rules.EFix.TraitDissentImpact)
			? Scaled(impact, Trait(state, unit, ETraitEffect.DissentImpact))
			: impact;
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
	public static long CombinedArms(CGameState state, CCountry owner, CUnit unit)
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

	/// <summary>
	/// Every brigade's type, spent or not: the original's definition rebuild marks each brigade's group for combined arms on
	/// both of its paths, though a spent support brigade adds only its softness to the unit's other figures
	/// (FINDINGS-liveread.md, section 28).
	/// </summary>
	private static List<CSubUnitType> CountedTypes(CUnit unit)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			types.Add(subUnit.Type);
		}
		return types;
	}

	/// <summary>
	/// Whether a country's unit is encircled in a province: the province's area is of one
	/// province, the province is not land with only sea beside it, and no area beside it is
	/// held by a friend of the country by the original's test, military access counted
	/// (<c>reversing/findings/FINDINGS-aihelpers.md</c>; seen in the running game,
	/// <c>FINDINGS-liveread.md</c> section 31). A province in no area is not.
	/// </summary>
	public static bool IsEncircled(CGameState state, CProvince province, CCountryTag owner)
	{
		if (Supply.CSupplyNetworks.Refresh(state).AreaOf(province.Id) is not { } area || area.Provinces.Count >= 2
			|| IsIsolatedLand(state, province))
		{
			return false;
		}
		foreach (Supply.CSupplyArea beside in area.Neighbours)
		{
			if (beside.Provinces.Count > 0 && state.Country(beside.Controller) is { } holder
				&& Countries.CCountryLists.IsFriend(state, holder, owner, withAccess: true))
			{
				return false;
			}
		}
		return true;
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

	/// <summary>One thing that counts for a unit's attack and its defence alike.</summary>
	private static void Add(CTerms terms, ECombatModifier kind, long both)
	{
		terms.Add(kind, both, both);
	}
}
