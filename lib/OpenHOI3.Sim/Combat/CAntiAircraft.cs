using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The fire that comes back at bombers. In every bombing the province fires one lump of
/// shots: its anti-aircraft guns, and one and a half times the air attack of every
/// friendly unit standing in it, whether that unit is in the bombing or not. Beside
/// that, each ship under the bombs fires its own air attack, and each land unit its
/// own - a land unit its plain figure, whatever the hour holds for or against it. An
/// aircraft at its base fires nothing. Written to roll in the original's order. See
/// <c>docs/mechanics/bombing.md</c>, section 8.
/// </summary>
public static class CAntiAircraft
{
	public const string StaticScale = "STATIC_AA_SCALE";
	public const string ProvincialEfficiency = "provincial_aa_efficiency";

	/// <summary>What the air attack of the units in the province counts for in its own fire, in thousandths.</summary>
	private const long UnitsShare = 1500;

	/// <summary>The most a shot is ever avoided by, in thousandths of a hundred.</summary>
	private const long MostAvoided = 99000;

	private const int MostTargets = 1024;

	public static void Attack(CGameState state, CCombat bombing)
	{
		CCombatSide side = bombing.Defender;
		CTraitGain.BankHour(state, side);
		List<CSubUnit> wings = side.Targets;
		wings.Clear();
		foreach (CUnit unit in bombing.Attacker.Units)
		{
			foreach (CSubUnit wing in unit.SubUnits)
			{
				if (CBomberFire.StrengthLeft(wing) >= 0 && wings.Count < MostTargets)
				{
					wings.Add(wing);
				}
			}
		}
		// The other two kinds do nothing at all where there is nobody to fire at; the
		// province's own fire asks the same for itself.
		if (wings.Count == 0)
		{
			return;
		}
		long avoid = Math.Min(state.Defines[CManpower.MilitarySection, CAirFire.AvoidChance].Raw, MostAvoided)
			/ CFixedPoint.Scale;
		FireFromProvince(state, bombing, wings, avoid);
		if (bombing.Kind == ECombatKind.NavalBombing)
		{
			foreach (CUnit fleet in new List<CUnit>(side.Units))
			{
				foreach (CSubUnit ship in new List<CSubUnit>(fleet.SubUnits))
				{
					long worth = ship.Stats[CAirMissions.AirAttack].Raw * ship.CombatAttack.Raw / CFixedPoint.Scale;
					Fire(state, wings, worth, avoid, organisationSides: 7, organisationLeast: 3, strengthSides: 3,
						strengthLeast: 1, critical: 10);
				}
			}
		}
		else if (bombing.Kind == ECombatKind.LandBombing)
		{
			foreach (CUnit unit in new List<CUnit>(side.Units))
			{
				// The unit's own figure, and the land battle's two products - which the
				// hour has just put back to one for every unit of this side.
				long worth = CUnitTotals.Sum(unit, CAirMissions.AirAttack).Raw;
				Fire(state, wings, worth, avoid, organisationSides: 9, organisationLeast: 4, strengthSides: 4,
					strengthLeast: 2, critical: 10);
			}
		}
	}

	/// <summary>
	/// The province's own fire: its guns, bettered by its holder's technology and
	/// scaled, and one and a half times the air attack of every unit standing in it
	/// whose country is a friend of its holder - a fleet in its port counted twice, being
	/// on both of the province's lists.
	/// </summary>
	public static long ProvinceFire(CGameState state, CProvince province)
	{
		long efficiency = state.Country(province.Controller) is { } holder ? holder.Technology[ProvincialEfficiency].Raw : 0;
		long guns = province.Modifier[CBombingOrders.AntiAir].Raw * (efficiency + CFixedPoint.Scale) / CFixedPoint.Scale;
		guns = state.Defines[CManpower.MilitarySection, StaticScale].Raw * guns / CFixedPoint.Scale;

		long units = 0;
		bool hasPort = CBombing.HasPort(state, province);
		foreach (CUnit unit in CBombing.Standing(state, province))
		{
			if (!IsFriendOfHolder(state, province, unit))
			{
				continue;
			}
			long attack = CUnitTotals.Sum(unit, CAirMissions.AirAttack).Raw;
			units += attack;
			if (hasPort && unit.Kind == EUnitKind.Navy)
			{
				units += attack;
			}
		}
		return guns + UnitsShare * units / CFixedPoint.Scale;
	}

	private static bool IsFriendOfHolder(CGameState state, CProvince province, CUnit unit)
	{
		return province.Controller is { } holder
			&& (unit.Owner == holder || CIntelligence.IsFriend(state, holder, unit.Owner));
	}

	private static void FireFromProvince(CGameState state, CCombat bombing, List<CSubUnit> wings, long avoid)
	{
		long worth = ProvinceFire(state, state.Province(bombing.Province));
		long shots = worth / CFixedPoint.Scale;
		if (state.Random.Next(100) < worth % CFixedPoint.Scale * 100 / CFixedPoint.Scale)
		{
			shots++;
		}
		for (; shots > 0; shots--)
		{
			CSubUnit target = wings[state.Random.Next(wings.Count)];
			state.Random.Next();                               // a roll nothing uses
			if (state.Random.Next(100) < avoid)
			{
				continue;
			}
			long organisation = (state.Random.Next(8) + 3) * (long)CFixedPoint.Scale;
			long strength = (state.Random.Next(2) + 1) * (long)CFixedPoint.Scale;
			if (state.Random.Next(100) < 10)
			{
				strength *= 5;
			}
			target.PendingOrganisationDamage += organisation;
			target.PendingStrengthDamage += strength;
		}
	}

	/// <summary>
	/// One ship's or one land unit's fire: its worth in shots, the fraction a chance at
	/// one more, each at a wing drawn at random.
	/// </summary>
	private static void Fire(CGameState state, List<CSubUnit> wings, long worth, long avoid, int organisationSides,
		int organisationLeast, int strengthSides, int strengthLeast, int critical)
	{
		long shots = worth / CFixedPoint.Scale;
		if (state.Random.Next(100) < worth % CFixedPoint.Scale * 100 / CFixedPoint.Scale)
		{
			shots++;
		}
		for (; shots > 0; shots--)
		{
			CSubUnit target = wings[state.Random.Next(wings.Count)];
			state.Random.Next();                               // a roll nothing uses
			if (state.Random.Next(100) < avoid)
			{
				continue;
			}
			long organisation = (state.Random.Next(organisationSides) + organisationLeast) * (long)CFixedPoint.Scale;
			long strength = (state.Random.Next(strengthSides) + strengthLeast) * (long)CFixedPoint.Scale;
			if (state.Random.Next(100) < 10)
			{
				strength *= critical;
			}
			target.PendingOrganisationDamage += organisation;
			target.PendingStrengthDamage += strength;
		}
	}
}
