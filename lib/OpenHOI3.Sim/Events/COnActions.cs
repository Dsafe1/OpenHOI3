using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Events;

/// <summary>
/// The events the game fires where something happens: the first of an on_action's events whose
/// trigger holds, its weight never read, and the firing dropping a fire-once event that has fired -
/// which so stands in front of those after it for good. See <c>docs/mechanics/events.md</c>, section 8.
/// </summary>
public static class COnActions
{
	/// <summary>Picks and fires an on_action's event in the scope; nothing found, nothing done.</summary>
	public static void Fire(CGameState state, string onAction, CEventScope scope)
	{
		if (!state.OnActions.TryGetValue(onAction, out IReadOnlyList<int>? ids))
		{
			return;
		}
		foreach (int id in ids)
		{
			if (Find(state, id) is { } picked && CConditions.Holds(state, picked.Trigger, scope))
			{
				CEventFiring.Fire(state, picked, scope);
				return;
			}
		}
	}

	/// <summary>
	/// A battle decided: for each country still in the fight on the winning side, its count of
	/// battles of the kind raised and the <c>*_won</c> event picked; then the same for the losing
	/// side with <c>*_lost</c>. A raid on a convoy and a bombing fire none; a land or an air battle
	/// where a side has no country fires none, a battle at sea fires all the same.
	/// </summary>
	public static void Battle(CGameState state, CCombat combat, bool attackerLost)
	{
		string? stem = combat.Kind switch
		{
			ECombatKind.Land => "on_battle",
			ECombatKind.Air => "on_air_battle",
			ECombatKind.Naval => "on_naval_battle",
			_ => null,
		};
		if (stem is null || combat.IsConvoyBattle)
		{
			return;
		}
		CCombatSide winner = attackerLost ? combat.Defender : combat.Attacker;
		CCombatSide loser = combat.Other(winner);
		if (combat.Kind != ECombatKind.Naval && (winner.Standing.Count == 0 || loser.Standing.Count == 0))
		{
			return;
		}
		Side(state, combat, winner, loser, stem + "_won");
		Side(state, combat, loser, winner, stem + "_lost");
	}

	private static void Side(CGameState state, CCombat combat, CCombatSide side, CCombatSide other, string onAction)
	{
		CCountryTag? from = other.Standing.Count > 0 ? other.Standing[0] : null;
		foreach (CCountryTag tag in new List<CCountryTag>(side.Standing))
		{
			if (state.Country(tag) is { } country)
			{
				switch (combat.Kind)
				{
					case ECombatKind.Land:
						country.LandBattlesFought++;
						break;
					case ECombatKind.Air:
						country.AirBattlesFought++;
						break;
					default:
						country.NavalBattlesFought++;
						break;
				}
			}
			CEventScope scope = CEventScope.Start(tag, combat.Province, fromCountry: from, randomState: (uint)state.Random.Next());
			Fire(state, onAction, scope);
		}
	}

	/// <summary>
	/// A pride of the fleet sunk by a shot: the shooter gains 5 experience, to at most 100, the game
	/// remembers both ships, and the shooter's country gets <c>on_enemy_pride_sunk</c> and then the
	/// pride's <c>on_our_pride_sunk</c>, FROM each time the other, with a seed of nought.
	/// </summary>
	public static void PrideSunk(CGameState state, CSubUnit pride, CSubUnit shooter)
	{
		CFixedPoint gained = CFixedPoint.FromInt(5) + (shooter.Experience.Raw < 0 ? CFixedPoint.Zero : shooter.Experience);
		shooter.Experience = gained > MostExperience ? MostExperience : gained;
		state.LastPrideSunk = pride;
		state.LastPrideSinker = shooter;
		CCountryTag? sinking = shooter.Unit?.Owner;
		CCountryTag? sunk = pride.Unit?.Owner;
		Fire(state, "on_enemy_pride_sunk", CEventScope.Start(sinking, fromCountry: sunk));
		Fire(state, "on_our_pride_sunk", CEventScope.Start(sunk, fromCountry: sinking));
	}

	private static readonly CFixedPoint MostExperience = CFixedPoint.FromInt(100);

	/// <summary>The event of an id - the later of two that share one, as the original finds it.</summary>
	private static CEvent? Find(CGameState state, int id)
	{
		CEvent? found = null;
		foreach (CEvent each in state.Events)
		{
			if (each.Id == id)
			{
				found = each;
			}
		}
		return found;
	}
}
