using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The end of a round at sea for one side: each ship's notes are taken off it, whole,
/// and a ship left with under a tenth of a point of strength is sunk. See
/// <c>docs/mechanics/navalcombat.md</c>, section 5.
/// </summary>
public static class CNavalLosses
{
	/// <summary>A ship with less strength than this is gone.</summary>
	public static readonly CFixedPoint LeastStrengthAfloat = new(100);

	/// <summary>
	/// Takes every ship's notes off it and clears them: neither more strength nor more
	/// organisation than it has. A ship's notes are not divided by a hundred as a
	/// brigade's are. What the fleet's transports took is laid on the land units aboard
	/// (<c>transport.md</c>, section 6).
	/// </summary>
	public static void Settle(CGameState state, CCombatSide side)
	{
		foreach (CUnit fleet in side.Units)
		{
			long transportDamage = 0;
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				if (CShipCensus.IsTransport(ship))
				{
					transportDamage += ship.PendingStrengthDamage;
				}
			}
			Transport.CTransport.LayDamage(fleet, transportDamage);
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				long strength = Math.Min(ship.PendingStrengthDamage, ship.Strength.Raw);
				long organisation = Math.Min(ship.PendingOrganisationDamage, ship.Organisation.Raw);
				ship.Strength = new CFixedPoint(ship.Strength.Raw - strength);
				ship.Organisation = new CFixedPoint(ship.Organisation.Raw - organisation);
				ship.PendingStrengthDamage = 0;
				ship.PendingOrganisationDamage = 0;
			}
		}
	}

	/// <summary>
	/// Sinks every ship of a side that has next to no strength left: out of its fleet,
	/// out of whatever had it in its sights, and onto the side's list. A fleet with no
	/// ship left is removed from the game.
	/// </summary>
	public static void RemoveSunk(CGameState state, CCombatSide side)
	{
		foreach (CUnit fleet in new List<CUnit>(side.Units))
		{
			for (int i = fleet.SubUnits.Count - 1; i >= 0; i--)
			{
				CSubUnit ship = fleet.SubUnits[i];
				if (ship.Strength >= LeastStrengthAfloat)
				{
					continue;
				}
				fleet.SubUnits.RemoveAt(i);
				Transport.CCarriers.LetGo(state, ship);   // its air groups are let go (transport.md, section 13.1)
				ship.Unit = null;
				side.Sunk.Add(ship);
				CSunkShips.Add(state, fleet.Owner, ship);
				side.Targets.Remove(ship);
				side.Combat.Other(side).Targets.Remove(ship);
			}
			if (fleet.SubUnits.Count == 0)
			{
				CCombatRetreat.RemoveFromGame(state, fleet);
			}
			else
			{
				// What the fleet can no longer carry is lost at sea (transport.md, section 6).
				Transport.CTransport.CheckOverload(state, fleet);
			}
		}
	}
}
