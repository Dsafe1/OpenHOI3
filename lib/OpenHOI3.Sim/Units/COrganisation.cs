using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

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
	/// The most organisation a brigade may have once a game is running: its own figure,
	/// less the share a reserve is kept short of, times its country's officer ratio,
	/// and more or less by the country's modifier for its branch.
	///
	/// The original adds the skill of the army's commander to the figure first. Leaders
	/// are not built, so nothing is added here.
	///
	/// A brigade that is in no unit - one just built, at the moment it is made - is not
	/// held back as a reserve and not measured against the officers: it has its own
	/// figure and the branch's modifier, and that is all.
	/// </summary>
	/// <param name="penaltyAtWar">What to do for a reserve of a country at war that has not mobilised.</param>
	/// <param name="penaltyAlways">Keep a reserve short whatever its country is doing.</param>
	public static CFixedPoint Ceiling(CGameState state, CCountry owner, CSubUnit subUnit,
		bool penaltyAtWar = false, bool penaltyAlways = false)
	{
		long most = subUnit.Stats[CSubUnitType.DefaultOrganisation].Raw;
		if (subUnit.Unit is not null)
		{
			if (subUnit.IsReserve && CUnits.ReservePenaltyApplies(state, owner, penaltyAtWar, penaltyAlways))
			{
				most = (CFixedPoint.Scale + owner.Modifier[CUnits.ReservesPenaltySize].Raw) * most / CFixedPoint.Scale;
			}
			most = most * owner.OfficerRatio.Raw / CFixedPoint.Scale;
		}

		string branch = subUnit.Type.Branch switch
		{
			EUnitBranch.Land => LandOrganisation,
			EUnitBranch.Naval => NavalOrganisation,
			_ => AirOrganisation,
		};
		most = most * (CFixedPoint.Scale + owner.Modifier[branch].Raw) / CFixedPoint.Scale;
		return new CFixedPoint(most);
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
		long factor = unit.SuppliesReceived.Raw;

		CProvince province = state.Province(unit.Location);
		if (province.Id != 0 && !province.IsSea)
		{
			long roads = province.Modifier[Infrastructure].Raw
				* (province.Modifier[LocalInfrastructure].Raw + province.Modifier[GlobalInfrastructure].Raw + CFixedPoint.Scale)
				/ CFixedPoint.Scale;
			roads = Math.Min(Math.Max(roads, LeastInfrastructure), CFixedPoint.Scale);
			factor = roads * factor / CFixedPoint.Scale;
		}

		factor = owner.OfficerRatio.Raw * factor / CFixedPoint.Scale;
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			factor = crowding.Of(unit) * factor / CFixedPoint.Scale;
		}
		factor = (owner.Modifier[RegainModifier].Raw + CFixedPoint.Scale) * factor / CFixedPoint.Scale;

		return unit.SuppliesReceived.Raw == 0 ? Unsupplied : factor;
	}

	/// <summary>
	/// One hour of one brigade: it gains a fifth of its morale times the unit's factor -
	/// more of it while it is low, less once it is nearly full - and is then held to
	/// what it may have. So this is also what brings a brigade down when its ceiling
	/// has fallen below it.
	/// </summary>
	public static void Regain(CSubUnit subUnit, long factor, long ceiling, long lowBonus)
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

		long gain = subUnit.Stats[Morale].Raw * factor / CFixedPoint.Scale;
		gain = gain * ShareOfAnHour / CFixedPoint.Scale;

		organisation = Math.Min(organisation + gain, ceiling);
		subUnit.Organisation = new CFixedPoint(Math.Max(organisation, 0));
	}

	/// <summary>
	/// The hourly update of every unit, as far as organisation goes. The original skips
	/// a unit that is fighting or marching onto enemy ground, and an army aboard a fleet
	/// at sea; none of the three can happen yet.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		var crowding = new CBaseCrowding(state);
		long lowBonus = state.Defines[CManpower.MilitarySection, LowRegainBonus].Raw;
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				long factor = RegainFactor(state, country, unit, crowding);
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					Regain(subUnit, factor, Ceiling(state, country, subUnit).Raw, lowBonus);
				}
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
					long full = subUnit.Stats[CSubUnitType.MaxStrength].Raw;
					long share = full == 0 ? -1 : over * CFixedPoint.Scale / full;
					long men = share * subUnit.Stats[CSubUnitStats.BuildCostManpower].Raw / CFixedPoint.Scale;
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
