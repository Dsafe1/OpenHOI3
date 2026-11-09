using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One try at the convoys of a sea province. The raider finds a convoy by a roll, is
/// set against its escorts, and where it is the stronger fights it: a throwaway fleet of
/// destroyers and transport ships is made out of the convoy's two counts, and five
/// rounds of the battle at sea are fought against it on the spot, at point-blank. What
/// that fleet lost is then taken off the convoy. See
/// <c>docs/mechanics/navalmissions.md</c>, section 8.
/// </summary>
public static class CConvoyRaid
{
	public const string ConvoyAttack = "convoy_attack";
	public const string EscortType = "destroyer";
	public const string TransportType = "transport_ship";

	/// <summary>What a convoy is found against: a roll of this many sides, under the raider's best detection.</summary>
	private const int FindingRoll = 50;

	/// <summary>How many provinces of its route a convoy has for each share of its ships a raid meets.</summary>
	private const long RouteShare = 5000;

	/// <summary>The rounds a raid is fought for.</summary>
	public const int Rounds = 5;

	/// <summary>Before how many of them the raider gains experience.</summary>
	public const int ExperienceRounds = 3;

	/// <summary>How far apart the ships of a raid stand: as near as ships get.</summary>
	private static readonly CFixedPoint RaidDistance = CFixedPoint.One;

	/// <summary>What a point of strength lost is worth in ships of the convoy: two for a whole ship of a hundred.</summary>
	private const long ShipsForStrength = 2000;
	private const long StrengthOfAShip = 100000;

	/// <summary>Transports' damage worth more than this much of a ship, and under a whole one, counts as one.</summary>
	private const long LeastTransportLoss = 300;

	/// <summary>
	/// What a raider can do to a convoy: each ship's <c>convoy_attack</c> times a
	/// hundredth of its strength and a hundredth of its organisation. An air unit, which
	/// raids with <c>sea_attack</c>, is not built.
	/// </summary>
	public static long Attack(CUnit raider)
	{
		long attack = 0;
		foreach (CSubUnit ship in raider.SubUnits)
		{
			long figure = ship.Stats[ConvoyAttack].Raw * ship.Strength.Raw / CFixedPoint.Scale;
			figure = figure * CFixedPoint.Scale / 100000;
			figure = figure * ship.Organisation.Raw / CFixedPoint.Scale;
			attack += figure * CFixedPoint.Scale / 100000;
		}
		return attack;
	}

	/// <summary>The convoys whose way goes through a province, country by country in the order they were made.</summary>
	public static List<CConvoy> Through(CGameState state, int province)
	{
		var convoys = new List<CConvoy>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CConvoy convoy in country.Convoys)
			{
				if (convoy.Path.Contains(province))
				{
					convoys.Add(convoy);
				}
			}
		}
		return convoys;
	}

	/// <summary>
	/// The convoy a raider goes for: among the enemy's convoys through its province that
	/// have a transport and that it finds, one whose defence is no more than its attack
	/// where there is such, and otherwise the first it found.
	/// </summary>
	public static CConvoy? Find(CGameState state, CUnit raider, long attack)
	{
		long detect = 0;
		foreach (CSubUnit ship in raider.SubUnits)
		{
			detect = Math.Max(detect, ship.Stats[CNavalSpotting.SurfaceDetection].Raw);
		}
		CConvoy? target = null;
		foreach (CConvoy convoy in Through(state, raider.Location))
		{
			if (convoy.Owner == raider.Owner || !CMovementRules.IsEnemyIn(state, convoy.Owner, raider.Owner, raider.Location)
				|| convoy.Transports <= 0)
			{
				continue;
			}
			if (state.Random.Next(FindingRoll) >= detect / CFixedPoint.Scale)
			{
				continue;
			}
			if (CConvoyRules.Defence(convoy) <= attack || target is null)
			{
				target = convoy;
			}
		}
		return target;
	}

	/// <summary>
	/// One try. Answers whether a convoy was fought - which it is not where none was
	/// found or its escorts were too many.
	/// </summary>
	public static bool Resolve(CGameState state, CCountry country, CUnit raider)
	{
		if (raider.Kind != EUnitKind.Navy)
		{
			return false;
		}
		long attack = Attack(raider);
		if (Find(state, raider, attack) is not { } convoy || raider.SubUnits.Count == 0)
		{
			return false;
		}
		if (CConvoyRules.Defence(convoy) > attack)
		{
			// Beaten off: a little national unity for the convoy's owner (politics.md, section 6).
			Politics.CStrategicWarfareImpacts.RaidBeatenOff(state, state.Country(convoy.Owner));
			return false;
		}

		CUnit? stand = MakeFleet(state, convoy, raider.Location);
		if (stand is not null)
		{
			CCombat fought = Fight(state, raider, stand);
			Count(state, convoy, stand, raider, fought);
			state.Province(raider.Location).LastConvoyAttackHour = state.TotalHours;
			stand.IsRemoved = true;
		}
		return true;
	}

	/// <summary>
	/// The fleet a convoy becomes for a raid: its escorts and its transports, each
	/// divided by a fifth of the provinces of its route - by one at least, and to one ship
	/// at least where there was any - as destroyers and transport ships. The destroyers
	/// have their country's technologies and the transport ships none, which is the
	/// original's. Null where it comes to no ship.
	/// </summary>
	public static CUnit? MakeFleet(CGameState state, CConvoy convoy, int province)
	{
		long scale = Math.Max(CFixedPoint.Scale, convoy.Path.Count * (long)CFixedPoint.Scale * CFixedPoint.Scale / RouteShare);
		long escorts = Scaled(convoy.Escorts, scale);
		long transports = Scaled(convoy.Transports, scale);
		long ships = RoundToNearest(escorts + transports);
		long escortShips = escorts / CFixedPoint.Scale;
		if (ships <= 0 || !state.SubUnitTypes.TryGetValue(EscortType, out CSubUnitType? destroyer)
			|| !state.SubUnitTypes.TryGetValue(TransportType, out CSubUnitType? transport))
		{
			return null;
		}

		CCountry? owner = state.Country(convoy.Owner);
		var fleet = new CUnit(EUnitKind.Navy, convoy.Owner) { Id = ++state.LastUnitId, Location = province, Name = "convoy" };
		for (long index = 0; index < ships; index++)
		{
			bool isEscort = index < escortShips;
			var ship = new CSubUnit(isEscort ? destroyer : transport) { Id = ++state.LastUnitId, Unit = fleet };
			if (isEscort && owner is not null)
			{
				foreach (string technology in new List<string>(ship.TechnologyLevels.Keys))
				{
					ship.TechnologyLevels[technology] = owner.TechnologyLevels.GetValueOrDefault(technology);
				}
			}
			CUnits.RebuildStats(ship);
			fleet.SubUnits.Add(ship);
		}
		return fleet;
	}

	private static long Scaled(int count, long scale)
	{
		long scaled = count * (long)CFixedPoint.Scale * CFixedPoint.Scale / scale;
		return scaled > 0 && scaled < CFixedPoint.Scale ? CFixedPoint.Scale : scaled;
	}

	/// <summary>
	/// The battle: the raider attacking, the convoy's fleet defending, every ship of both
	/// as near as ships get, and five rounds at once. It is a battle at sea like any
	/// other but for what <see cref="CCombat.IsConvoyBattle"/> changes, and it is on no
	/// list: nobody else joins it and it is gone when the rounds are fought.
	/// </summary>
	public static CCombat Fight(CGameState state, CUnit raider, CUnit stand)
	{
		var combat = new CCombat(raider.Location, ECombatKind.Naval) { IsConvoyBattle = true };
		CNavalBattle.Join(state, combat, combat.Attacker, raider);
		CNavalBattle.Join(state, combat, combat.Defender, stand);
		CNavalBattle.Begin(state, combat);
		foreach (CUnit fleet in new[] { raider, stand })
		{
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				ship.Distance = RaidDistance;
			}
		}
		for (int round = 0; round < Rounds; round++)
		{
			// The raider's experience, before each of the first three rounds.
			if (round < ExperienceRounds)
			{
				CCombatExperience.Grant(state, raider, convoyRaid: true);
			}
			CNavalBattle.RunHour(state, combat);
		}
		// Nothing here counts the hours the raider's leader banked: the original takes the
		// battle off by hand and never ends it, so they wait for his next battle.
		foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
		{
			foreach (CUnit fleet in side.Units)
			{
				fleet.Combats.Remove(combat);
			}
			side.Units.Clear();
		}
		// Like any battle that ends, it goes on the list of finished battles.
		state.CombatHistory.Record(combat, state.TotalHours);
		return combat;
	}

	/// <summary>
	/// What the raid cost the convoy: the strength its destroyers lost and the strength
	/// its transport ships lost, each counted at two ships to the hundred points; and
	/// with a whole ship of either, the convoy loses that many of each, rounded.
	/// </summary>
	public static void Count(CGameState state, CConvoy convoy, CUnit stand, CUnit? raider = null, CCombat? fought = null)
	{
		CCountry? owner = state.Country(convoy.Owner);
		long lostEscorts = 0;
		long lostTransports = 0;
		foreach (CSubUnit ship in stand.SubUnits)
		{
			long most = owner is null
				? ship.Stats[CSubUnitType.MaxStrength].Raw
				: CUnits.MaxStrength(state, owner, ship, false, false).Raw;
			long lost = most - ship.Strength.Raw;
			if (CShipCensus.IsTransport(ship))
			{
				lostTransports += lost;
			}
			else
			{
				lostEscorts += lost;
			}
		}
		long escorts = lostEscorts * ShipsForStrength / CFixedPoint.Scale * CFixedPoint.Scale / StrengthOfAShip;
		long transports = lostTransports * ShipsForStrength / CFixedPoint.Scale * CFixedPoint.Scale / StrengthOfAShip;
		if (transports > LeastTransportLoss && transports < CFixedPoint.Scale)
		{
			transports = CFixedPoint.Scale;
		}
		if (escorts >= CFixedPoint.Scale || transports >= CFixedPoint.Scale)
		{
			bool hadEscort = convoy.Escorts > 0;
			StrategicWarfare.CWarfareTally.ConvoyRaid(state, convoy, fought, raider, RoundToNearest(transports));
			CConvoyLosses.Lose(state, convoy, (int)RoundToNearest(transports), (int)RoundToNearest(escorts),
				state.Country(raider?.Owner));
			// What the owner's national unity makes of it (politics.md, section 6).
			Politics.CStrategicWarfareImpacts.RaidSank(state, owner, transports, escorts, hadEscort);
		}
	}

	/// <summary>A number of thousandths as a whole number, a half going to the even one - as the original's processor rounds.</summary>
	public static long RoundToNearest(long thousandths)
	{
		long whole = thousandths / CFixedPoint.Scale;
		long rest = thousandths % CFixedPoint.Scale;
		if (rest > CFixedPoint.Scale / 2 || (rest == CFixedPoint.Scale / 2 && whole % 2 != 0))
		{
			whole++;
		}
		return whole;
	}
}
