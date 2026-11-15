using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What the upgrade share of a country's IC does: brings each brigade's equipment up to
/// what the country has researched, a level at a time. See
/// <c>docs/mechanics/upkeep.md</c>, section 5.
/// </summary>
public static class CUpgrades
{
	public const string ShipSpeed = "SHIP_UPGRADE_SPEED_MOD";
	public const string AirSpeed = "AIR_UPGRADE_SPEED_MOD";
	public const string LandSpeed = "LAND_UPGRADE_SPEED_MOD";

	/// <summary>A level is done in half the days the brigade would take to build.</summary>
	private const long TwiceAsFast = 2000;

	/// <summary>However long it takes to build, a day moves a level on by this much.</summary>
	private const long LeastRate = 1;

	/// <summary>Half the brigade's cost is spread over the technologies it holds, one level each.</summary>
	private const long ShareOfCost = 500;

	private const long WholeLevel = CFixedPoint.Scale;

	/// <summary>How far a day moves one level of one technology on for this brigade, in thousandths of the level.</summary>
	public static long Rate(CGameState state, CCountry country, CSubUnit subUnit)
	{
		long days = CBuildCosts.BuildTime(country, subUnit) * CFixedPoint.Scale;
		long aDay = days == 0 ? -1 : CFixedPoint.Scale * CFixedPoint.Scale / days;
		long rate = Math.Max(aDay * TwiceAsFast / CFixedPoint.Scale, LeastRate);
		string define = subUnit.Type.Branch switch
		{
			EUnitBranch.Naval => ShipSpeed,
			EUnitBranch.Air => AirSpeed,
			_ => LandSpeed,
		};
		return rate * state.Defines[CManpower.MilitarySection, define].Raw / CFixedPoint.Scale;
	}

	/// <summary>
	/// What a day of bringing one technology up costs for this brigade, in thousandths
	/// of IC. The further behind it is, the less each day costs.
	/// </summary>
	public static long DailyCost(CCountry country, CSubUnit subUnit, int levelsBehind)
	{
		long technologies = (long)subUnit.Type.Technologies.Count * CFixedPoint.Scale;
		long each = CBuildCosts.BuildCost(country, subUnit) * CFixedPoint.Scale / technologies;
		long share = ShareOfCost * CFixedPoint.Scale / ((long)levelsBehind * CFixedPoint.Scale);
		return each * share / CFixedPoint.Scale;
	}

	/// <summary>
	/// The level of a technology a country's brigades are brought up to, and a new one is built at: its own - or, for a
	/// government in exile, its faction's first member's where that is higher (<c>docs/mechanics/war.md</c>, section 14;
	/// <c>reversing/findings/FINDINGS-surrender.md</c>, C.7).
	/// </summary>
	public static int EquipmentLevel(CGameState state, CCountry country, string technology) =>
		EquipmentLevel(country, ExileLeader(state, country), technology);

	/// <summary>The same, with the exile's faction leader looked up beforehand (<see cref="ExileLeader"/>).</summary>
	public static int EquipmentLevel(CCountry country, CCountry? exileLeader, string technology)
	{
		int level = country.TechnologyLevels.GetValueOrDefault(technology);
		return exileLeader is null ? level : Math.Max(level, exileLeader.TechnologyLevels.GetValueOrDefault(technology));
	}

	/// <summary>Whose levels a government in exile builds at where they are higher: its real faction's first member, not itself; else none.</summary>
	public static CCountry? ExileLeader(CGameState state, CCountry country)
	{
		if (!country.InExile || !Agreements.COfferRules.RealFaction(country))
		{
			return null;
		}
		CCountry? leader = state.Country(Agreements.COfferRules.Leader(state, country.Faction));
		return leader == country ? null : leader;
	}

	/// <summary>
	/// What entering a game does with the share: every country's run once, as the original's entry runs the six shares of
	/// IC and the four of leadership with nothing to share out. Nothing is spent and no equipment moves on, but a doctrine
	/// costs nothing, so every brigade behind its country in one gains a level - which is how a brigade three levels behind
	/// at a later start is level with its country two days in (<c>docs/mechanics/upkeep.md</c>, section 5;
	/// <c>reversing/findings/FINDINGS-unitdaily.md</c>, section 5). The original's entry does it for a loaded game too.
	/// </summary>
	public static void RunAtEntry(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			Run(state, country, CFixedPoint.Zero, apply: true);
		}
	}

	/// <summary>
	/// One day of the share. Works out what a day of everything outstanding costs - the
	/// share's need - and, when <paramref name="apply"/> is set, moves each level on as
	/// far as the allocation reaches, in the units' own order. Answers the IC it spent.
	/// </summary>
	public static CFixedPoint Run(CGameState state, CCountry country, CFixedPoint allocation, bool apply)
	{
		long budget = allocation.Raw;
		long spent = 0;
		long cost = 0;
		int upgrading = 0;
		CCountry? exileLeader = ExileLeader(state, country);

		foreach (CUnit unit in CUnits.InWorkingOrder(country, unit => unit.MayUpgrade, lentFirst: true))
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				bool behind = false;
				bool gained = false;
				long rate = -1;
				foreach (CTechnology technology in subUnit.Type.Technologies)
				{
					if (!technology.CanUpgrade)
					{
						continue;
					}
					int held = subUnit.TechnologyLevels.GetValueOrDefault(technology.Name);
					int reached = EquipmentLevel(country, exileLeader, technology.Name);
					if (reached <= held)
					{
						continue;
					}
					behind = true;

					if (rate < 0)
					{
						rate = Rate(state, country, subUnit);
					}
					long daily = DailyCost(country, subUnit, reached - held);
					long progress = rate;

					// A doctrine needs no equipment: it costs nothing and is learnt in a day.
					if (!technology.Change)
					{
						daily = 0;
						progress = WholeLevel;
					}

					long paid = daily;
					if (daily > budget)
					{
						// The last of the allocation buys what it can, and then there is none.
						progress = budget * TwiceAsFast / CFixedPoint.Scale * progress / CFixedPoint.Scale;
						paid = budget;
					}

					if (apply && progress > 0)
					{
						long done = subUnit.TechnologyProgress.GetValueOrDefault(technology.Name).Raw + progress;
						if (done >= WholeLevel)
						{
							done = 0;
							subUnit.TechnologyLevels[technology.Name] = held + 1;
							gained = true;

							// Its equipment is now its owner's, so it is named by its owner's models - but not a
							// unit this country has lent out, whose owner is the host. A doctrine changes nothing.
							if (technology.Change && unit.Lender != country.Tag)
							{
								subUnit.Builder = unit.Owner;
							}
						}
						subUnit.TechnologyProgress[technology.Name] = new CFixedPoint(done);
						spent += paid;
						budget -= paid;
					}

					cost += daily;
				}

				if (behind)
				{
					upgrading++;
				}
				if (gained)
				{
					// Its figures are worked out again, and it is held to what they allow.
					CUnits.RebuildStats(subUnit);
					subUnit.Strength = CFixedPoint.Min(subUnit.Strength, CUnits.MaxStrength(state, country, subUnit));
					subUnit.Organisation = CFixedPoint.Min(subUnit.Organisation, COrganisation.Ceiling(state, country, subUnit));
					if (subUnit.Strength > subUnit.Highest)
					{
						subUnit.Highest = subUnit.Strength;
					}
				}
			}
		}

		country.UpgradeCost = new CFixedPoint(cost);
		country.SubUnitsUpgrading = upgrading;
		return new CFixedPoint(spent);
	}
}
