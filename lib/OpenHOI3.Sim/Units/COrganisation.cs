using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What holds a brigade's organisation and what brings it back: the most it may have,
/// and the hourly return towards that. See <c>docs/mechanics/organisation.md</c>,
/// sections 2 to 5.
///
/// The arithmetic is the original's step for step, in thousandths with every division
/// dropping its remainder, because a brigade's organisation is a figure the savegames
/// show to the thousandth.
/// </summary>
public static class COrganisation
{
	/// <summary>The key of <c>CUnits.ReservesPenaltySize</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey ReservesPenaltySizeKey = CModifierKey.Of(CUnits.ReservesPenaltySize);

	/// <summary>The key of <c>RegainModifier</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey RegainModifierKey = CModifierKey.Of(RegainModifier);

	/// <summary>The key of <c>Infrastructure</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey InfrastructureKey = CModifierKey.Of(Infrastructure);

	/// <summary>The key of <c>LocalInfrastructure</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey LocalInfrastructureKey = CModifierKey.Of(LocalInfrastructure);

	/// <summary>The key of <c>GlobalInfrastructure</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey GlobalInfrastructureKey = CModifierKey.Of(GlobalInfrastructure);

	/// <summary>The key of <c>CSubUnitStats.BuildCostManpower</c>, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey BuildCostManpowerKey = CModifierKey.Of(CSubUnitStats.BuildCostManpower);

	/// <summary>The keys of the three branches' organisation modifiers, asked every hour (<see cref="CModifierKey"/>).</summary>
	private static readonly CModifierKey LandOrganisationKey = CModifierKey.Of(LandOrganisation);
	private static readonly CModifierKey NavalOrganisationKey = CModifierKey.Of(NavalOrganisation);
	private static readonly CModifierKey AirOrganisationKey = CModifierKey.Of(AirOrganisation);

	public const string LandOrganisation = "land_organisation";
	public const string NavalOrganisation = "naval_organisation";
	public const string AirOrganisation = "air_organisation";

	/// <summary>The country modifier that speeds or slows the return of organisation.</summary>
	public const string RegainModifier = "org_regain";

	/// <summary>A brigade's own figure for how fast its organisation comes back.</summary>
	public const string Morale = "default_morale";

	/// <summary>The province's own infrastructure, and the two modifiers that scale it.</summary>
	public const string Infrastructure = "infrastructure";
	public const string LocalInfrastructure = "local_infrastructure";
	public const string GlobalInfrastructure = "global_infrastructure";

	/// <summary>The define a brigade low on organisation gains faster by, and one nearly full slower.</summary>
	public const string LowRegainBonus = "LOW_ORG_REGAIN_BONUS";

	/// <summary>The define taken off the hour's gain of a land brigade whose unit is being strategically redeployed.</summary>
	public const string RedeploymentLoss = "STRAT_REDEP_ORG_LOSS";

	/// <summary>However bad the roads, a hundredth of the usual comes back.</summary>
	private const long LeastInfrastructure = 10;

	/// <summary>An hour at full speed brings back a fifth of a brigade's morale.</summary>
	private const long ShareOfAnHour = 200;

	/// <summary>Under a quarter of what it may have a brigade is low, and over three quarters nearly full.</summary>
	private const long Low = 250;
	private const long NearlyFull = 750;

	/// <summary>What a unit that got no supplies at all gains: it loses instead.</summary>
	private const long Unsupplied = -1000;

	/// <summary>
	/// The most organisation a brigade may have once a game is running: its own figure
	/// and what its army's commander adds, less the share a reserve is kept short of,
	/// times its country's officer ratio, and more or less by the country's modifier for
	/// its branch.
	///
	/// A brigade that is in no unit - one just built, at the moment it is made - is not
	/// held back as a reserve, not measured against the officers and given nothing by a
	/// commander: it has its own figure and the branch's modifier, and that is all.
	///
	/// Before a game is under way - the two days a new game runs before its first hour -
	/// the most a brigade may have is its own figure and nothing else: no commander, no
	/// reserve's share, no officers, no modifier. So an upgrade finished in those days
	/// holds a brigade to its figure, and the hour run at their end brings nobody down.
	/// </summary>
	/// <param name="penaltyAtWar">What to do for a reserve of a country at war that has not mobilised.</param>
	/// <param name="penaltyAlways">Keep a reserve short whatever its country is doing.</param>
	public static CFixedPoint Ceiling(CGameState state, CCountry owner, CSubUnit subUnit,
		bool penaltyAtWar = false, bool penaltyAlways = false)
	{
		long bonus = !state.IsBeingMade && subUnit.Unit is { } unit ? CommanderBonus(state, unit) : 0;
		return CeilingWithBonus(state, owner, subUnit, bonus, penaltyAtWar, penaltyAlways);
	}

	/// <summary>
	/// The same, with what the army's commander adds already worked out for the
	/// brigade's unit: the hour works it out once a unit and not once a brigade.
	/// </summary>
	public static CFixedPoint CeilingWithBonus(CGameState state, CCountry owner, CSubUnit subUnit, long bonus,
		bool penaltyAtWar = false, bool penaltyAlways = false)
	{
		owner = (subUnit.Unit is { } lent ? state.Country(lent.FiguresOwner) : null) ?? owner;
		return new CFixedPoint(CeilingOf(state, owner, subUnit, bonus, penaltyAtWar, penaltyAlways));
	}

	/// <summary>
	/// The ceiling with the country whose figures the brigade's unit goes by already found - its lender's for a unit
	/// lent - which the hour finds once a unit and not once a brigade.
	/// </summary>
	private static long CeilingOf(CGameState state, CCountry owner, CSubUnit subUnit, long bonus,
		bool penaltyAtWar = false, bool penaltyAlways = false)
	{
		long most = subUnit.Stats.DefaultOrganisation.Raw;
		if (state.IsBeingMade)
		{
			return most;
		}
		if (subUnit.Unit is not null)
		{
			most += bonus;
			if (subUnit.IsReserve && CUnits.ReservePenaltyApplies(state, owner, penaltyAtWar, penaltyAlways))
			{
				most = (CFixedPoint.Scale + owner.Modifier[ReservesPenaltySizeKey].Raw) * most / CFixedPoint.Scale;
			}
			most = most * owner.OfficerRatio.Raw / CFixedPoint.Scale;
		}

		CModifierKey branch = subUnit.Type.Branch switch
		{
			EUnitBranch.Land => LandOrganisationKey,
			EUnitBranch.Naval => NavalOrganisationKey,
			_ => AirOrganisationKey,
		};
		return most * (CFixedPoint.Scale + owner.Modifier[branch].Raw) / CFixedPoint.Scale;
	}

	/// <summary>
	/// What the army's commander adds to the most organisation each brigade of a unit
	/// may have: his skill, whole, times how much of the army's command reaches the unit.
	/// Nothing where there is no army above it, or no commander of it.
	/// </summary>
	public static long CommanderBonus(CGameState state, CUnit unit)
	{
		if (ArmyAbove(unit) is not { } army || army.Leader is not { } leader || leader.Skill == 0)
		{
			return 0;
		}
		long skill = leader.Skill * CFixedPoint.Scale;
		return CCommandReach.Through(state, unit, CCommandReach.Level(army)) * skill / CFixedPoint.Scale;
	}

	/// <summary>
	/// The army whose commander the original adds to a brigade's organisation for: the
	/// unit itself when it is an army, and otherwise the army a corps or a division
	/// answers to, with at most a corps between them. Null where there is none - a
	/// division straight under a theatre has no army.
	/// </summary>
	public static CUnit? ArmyAbove(CUnit unit)
	{
		switch (unit.Kind)
		{
			case EUnitKind.Army:
				return unit;
			case EUnitKind.Corps:
				return unit.Parent is { Kind: EUnitKind.Army } ? unit.Parent : null;
			case EUnitKind.Division:
				if (unit.Parent is { Kind: EUnitKind.Army })
				{
					return unit.Parent;
				}
				return unit.Parent is { Kind: EUnitKind.Corps, Parent.Kind: EUnitKind.Army } ? unit.Parent.Parent : null;
			default:
				return null;
		}
	}

	/// <summary>
	/// How fast a unit's brigades get their organisation back this hour, in thousandths
	/// of the usual: by the supplies it was given, the roads where it stands, its
	/// country's officers, how crowded its base is and the country's modifier. A unit
	/// given no supplies at all loses organisation instead.
	/// </summary>
	public static long RegainFactor(CGameState state, CCountry owner, CUnit unit, CBaseCrowding crowding)
	{
		owner = state.Country(unit.FiguresOwner) ?? owner;
		long factor = unit.SuppliesReceived.Raw;

		CProvince province = state.Province(unit.Location);
		if (province.Id != 0 && !province.IsSea)
		{
			factor = Roads(province) * factor / CFixedPoint.Scale;
		}

		factor = owner.OfficerRatio.Raw * factor / CFixedPoint.Scale;
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			factor = crowding.Of(unit) * factor / CFixedPoint.Scale;
		}
		factor = (owner.Modifier[RegainModifierKey].Raw + CFixedPoint.Scale) * factor / CFixedPoint.Scale;

		return unit.SuppliesReceived.Raw == 0 ? Unsupplied : factor;
	}

	/// <summary>
	/// What the organisation bar's tooltip says a unit regains an hour, in thousandths; null for a unit that is fighting
	/// or aboard a fleet at sea, which say nothing. The hour's factor worked out again, not taken from it, and not quite
	/// the same way: the roads count wherever the unit stands, at sea too, and a unit given no supplies is not turned to a
	/// loss; then times its brigades' mean morale and the fifth of it an hour brings back, with no low or high bonus
	/// (<c>docs/mechanics/organisation.md</c>, section 3; <c>unitpanel.md</c>, section 10.1).
	/// </summary>
	public static long? ShownRegain(CGameState state, CCountry owner, CUnit unit)
	{
		if (unit.IsFighting || (unit.Transport is not null && state.Province(unit.Location).IsSea))
		{
			return null;
		}
		owner = state.Country(unit.FiguresOwner) ?? owner;
		long factor = unit.SuppliesReceived.Raw;
		CProvince province = state.Province(unit.Location);
		// The roads count wherever the unit stands, at sea too - the original's, which shows a fleet at sea a hundredth of
		// what it regains; with the fix on land alone, as the hour counts them.
		if (province.Id != 0 && !(province.IsSea && state.Rules.Has(Rules.EFix.ShownFigures)))
		{
			factor = Roads(province) * factor / CFixedPoint.Scale;
		}
		factor = owner.OfficerRatio.Raw * factor / CFixedPoint.Scale;
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			factor = new CBaseCrowding(state).Of(unit) * factor / CFixedPoint.Scale;
		}
		factor = (owner.Modifier[RegainModifierKey].Raw + CFixedPoint.Scale) * factor / CFixedPoint.Scale;
		factor = CUnitTotals.Mean(unit, Morale).Raw * factor / CFixedPoint.Scale;
		return factor * ShareOfAnHour / CFixedPoint.Scale;
	}

	/// <summary>The roads' term: a province's infrastructure by its two modifiers, held between a hundredth and the whole.</summary>
	private static long Roads(CProvince province)
	{
		long roads = province.Modifier[InfrastructureKey].Raw
			* (province.Modifier[LocalInfrastructureKey].Raw + province.Modifier[GlobalInfrastructureKey].Raw + CFixedPoint.Scale)
			/ CFixedPoint.Scale;
		return Math.Min(Math.Max(roads, LeastInfrastructure), CFixedPoint.Scale);
	}

	/// <summary>
	/// One hour of one brigade: it gains a fifth of its morale times the unit's factor -
	/// more of it while it is low, less once it is nearly full - and is then held to
	/// what it may have. So this is also what brings a brigade down when its ceiling
	/// has fallen below it. With <paramref name="redeploymentLoss"/> - a land brigade whose unit is being strategically
	/// redeployed - that much is taken off the gain and what is left held at nought, so such a brigade never loses
	/// organisation in its hour, not even with no supplies (<c>docs/mechanics/organisation.md</c>, section 3).
	/// </summary>
	public static void Regain(CSubUnit subUnit, long factor, long ceiling, long lowBonus, long? redeploymentLoss = null)
	{
		long organisation = subUnit.Organisation.Raw;
		long share = ceiling == 0 ? -1 : organisation * CFixedPoint.Scale / ceiling;
		if (share < Low)
		{
			factor = (lowBonus + CFixedPoint.Scale) * factor / CFixedPoint.Scale;
		}
		else if (share > NearlyFull)
		{
			factor = (CFixedPoint.Scale - lowBonus) * factor / CFixedPoint.Scale;
		}

		long gain = subUnit.Stats.Morale.Raw * factor / CFixedPoint.Scale;
		gain = gain * ShareOfAnHour / CFixedPoint.Scale;
		if (redeploymentLoss is { } loss)
		{
			gain = Math.Max(gain - loss, 0);
		}

		organisation = Math.Min(organisation + gain, ceiling);
		subUnit.Organisation = new CFixedPoint(Math.Max(organisation, 0));
	}

	/// <summary>
	/// Whether a unit is on its way into a province an enemy holds, with no attack delay
	/// running. Such a unit gets no organisation back, any more than one that is fighting.
	/// </summary>
	public static bool IsMovingOntoEnemyGround(CGameState state, CUnit unit)
	{
		return unit.IsMoving && unit.AttackDelay == CFixedPoint.Zero
			&& Movement.CMovementRules.IsEnemy(state, state.Province(unit.Path[0]).Controller, unit.Owner);
	}

	/// <summary>
	/// The hourly update of every unit, as far as organisation goes. A unit that is
	/// fighting gets none back, nor one marching onto enemy ground, nor one aboard a fleet
	/// at sea (<c>transport.md</c>, section 5).
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		var crowding = new CBaseCrowding(state);
		// Counted before the hour's walk, which settles dropped units as it goes.
		crowding.CountNow();
		long lowBonus = state.Defines[CManpower.MilitarySection, LowRegainBonus].Raw;
		long? redeploymentLoss = state.Defines.Has(CManpower.MilitarySection, RedeploymentLoss)
			? state.Defines[CManpower.MilitarySection, RedeploymentLoss].Raw
			: null;
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (!unit.IsFighting && !IsMovingOntoEnemyGround(state, unit)
					&& !(unit.Transport is not null && state.Province(unit.Location).IsSea))
				{
					long factor = RegainFactor(state, country, unit, crowding);
					long bonus = CommanderBonus(state, unit);
					long? loss = unit.Order == EUnitOrder.StrategicRedeployment && Movement.CMovementRules.IsLandUnit(unit)
						? redeploymentLoss ?? 0
						: null;
					// The country the unit's figures go by, found once for all its brigades.
					CCountry figures = state.Country(unit.FiguresOwner) ?? country;
					foreach (CSubUnit subUnit in unit.SubUnits)
					{
						long ceiling = subUnit.Unit == unit
							? CeilingOf(state, figures, subUnit, bonus)
							: CeilingWithBonus(state, country, subUnit, bonus).Raw;
						Regain(subUnit, factor, ceiling, lowBonus, loss);
					}
				}
				// Then a dropped unit's first hour out of a battle (transport.md, section 13.4).
				Transport.CAirTransport.Settle(state, unit);
			}
		}
	}

	/// <summary>
	/// Holds every brigade of a country to the strength and the organisation it may have
	/// now, and gives the country back the men of the strength taken off. It is what the
	/// original does when a law changes, since a law can move both ceilings at once.
	/// </summary>
	public static void HoldToCeilings(CGameState state, CCountry country)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				long most = CUnits.MaxStrength(state, country, subUnit).Raw;
				long over = subUnit.Strength.Raw - most;
				if (over > 0)
				{
					long full = subUnit.Stats.MaxStrength.Raw;
					long share = full == 0 ? -1 : over * CFixedPoint.Scale / full;
					long men = share * subUnit.Stats[BuildCostManpowerKey].Raw / CFixedPoint.Scale;
					country.Manpower = new CFixedPoint(Math.Max(0, country.Manpower.Raw + men));

					// The most it has ever had starts again from here.
					subUnit.Strength = new CFixedPoint(Math.Max(0, most));
					subUnit.Highest = subUnit.Strength;
				}

				CFixedPoint ceiling = Ceiling(state, country, subUnit);
				if (ceiling < subUnit.Organisation)
				{
					subUnit.Organisation = CFixedPoint.Max(ceiling, CFixedPoint.Zero);
				}
			}
		}
	}
}
