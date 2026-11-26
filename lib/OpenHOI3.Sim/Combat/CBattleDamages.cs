using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The counting behind <see cref="CBattleDamage"/>, in an enhanced game alone. A shot that lands is noted against its
/// target with who fired it and what it asked; when the hour's damage is settled, **what was really taken off the
/// target** - a hundredth of what the hits came to, and no more than it had - is the target's taken and is shared out
/// to those who fired, each by its part of what was asked, as their dealt. So what a unit dealt is what its targets
/// lost by it, and the two sides of a battle add up. Nothing here changes a figure of the battle.
/// </summary>
public static class CBattleDamages
{
	/// <summary>A shot of a land battle about to be noted on its target.</summary>
	public static void Note(CGameState state, CCombat combat, CUnit attacker, CUnit target, long strength, long organisation)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		Of(attacker, combat);
		Of(target, combat).Asked.Add(new CBattleDamageAsk(attacker, strength, organisation));
	}

	/// <summary>A unit's record for a battle: a shot of another battle than the one it holds starts it afresh.</summary>
	private static CBattleDamage Of(CUnit unit, CCombat combat)
	{
		if (unit.BattleDamage is not { } record || !ReferenceEquals(record.Combat, combat))
		{
			record = new CBattleDamage { Combat = combat };
			unit.BattleDamage = record;
		}
		return record;
	}

	/// <summary>
	/// A unit's hour settled: so much strength and so much organisation, summed over its brigades, were taken off it.
	/// Only a unit with a record is counted - one a land battle's shot has reached.
	/// </summary>
	public static void Settled(CGameState state, CUnit target, long strength, long organisation)
	{
		if (target.BattleDamage is not { } record)
		{
			return;
		}
		long hour = state.TotalHours;
		record.AddTaken(hour, strength, organisation);
		long askedStrength = 0;
		long askedOrganisation = 0;
		foreach (CBattleDamageAsk ask in record.Asked)
		{
			askedStrength += ask.Strength;
			askedOrganisation += ask.Organisation;
		}
		int brigades = target.SubUnits.Count;
		foreach (CBattleDamageAsk ask in record.Asked)
		{
			if (ask.Attacker.BattleDamage is not { } dealer || !ReferenceEquals(dealer.Combat, record.Combat))
			{
				continue;
			}
			// A unit's organisation is its brigades' average, so what is dealt is the sum over the target's number.
			dealer.AddDealt(hour,
				askedStrength > 0 ? strength * ask.Strength / askedStrength * CBattleDamage.Fine
					+ strength * ask.Strength % askedStrength * CBattleDamage.Fine / askedStrength : 0,
				askedOrganisation > 0 && brigades > 0
					? organisation * CBattleDamage.Fine / brigades * ask.Organisation / askedOrganisation : 0);
		}
		record.Asked.Clear();
	}

	/// <summary>A unit out of a battle, or a battle over: its figures of that battle go.</summary>
	public static void Forget(CUnit unit, CCombat combat)
	{
		if (unit.BattleDamage is { } record && ReferenceEquals(record.Combat, combat))
		{
			unit.BattleDamage = null;
		}
	}
}
