using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Transport;

/// <summary>
/// The warning <c>INVADEDUS</c>: the landings on each player's ground, by the country making them, noted when a unit is
/// given its path and told the first thing the next hour. See <c>docs/mechanics/transport.md</c>, section 11.
/// </summary>
public static class CInvasionWarnings
{
	public const string Message = "INVADEDUS";

	/// <summary>
	/// A unit has been given a path. Where it is landing on ground a player holds whose country is at war with its
	/// owner, it is listed under its owner - and a country new to the list, or a unit new under it, is marked new. A
	/// unit already listed and given a path again clears the mark.
	/// </summary>
	public static void Note(CGameState state, CUnit unit)
	{
		foreach (CCountryTag player in state.Played)
		{
			if (!IsInvading(state, unit, player))
			{
				continue;
			}
			CInvasionWarning? warning = null;
			foreach (CInvasionWarning each in state.InvasionWarnings)
			{
				if (each.Player == player && each.Invader == unit.Owner)
				{
					warning = each;
					break;
				}
			}
			if (warning is null)
			{
				warning = new CInvasionWarning(player, unit.Owner) { IsNew = true };
				state.InvasionWarnings.Add(warning);
			}
			if (warning.Units.Contains(unit))
			{
				warning.IsNew = false;
				continue;
			}
			warning.Units.Add(unit);
			warning.IsNew = true;
		}
	}

	/// <summary>An enemy of the player's country landing from the sea on a province the player holds.</summary>
	public static bool IsInvading(CGameState state, CUnit unit, CCountryTag player)
	{
		return !unit.IsRemoved && CLanding.IsLanding(state, unit)
			&& CMovementRules.IsEnemy(state, player, unit.Owner)
			&& state.Province(unit.Path[0]).Controller == player;
	}

	/// <summary>
	/// The first thing each hour does: each country's units that are gone or no longer landing are struck off, and where
	/// some remain and the country is marked new, the mark is cleared and the player is told - the invader, and every
	/// province its listed units are landing on.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		foreach (CInvasionWarning warning in new List<CInvasionWarning>(state.InvasionWarnings))
		{
			if (!state.Played.Contains(warning.Player))
			{
				state.InvasionWarnings.Remove(warning);
				continue;
			}
			warning.Units.RemoveAll(unit => !IsInvading(state, unit, warning.Player));
			if (warning.Units.Count == 0)
			{
				state.InvasionWarnings.Remove(warning);
				continue;
			}
			if (!warning.IsNew)
			{
				continue;
			}
			warning.IsNew = false;
			var provinces = new List<string>();
			foreach (CUnit unit in warning.Units)
			{
				provinces.Add(unit.Path[0].ToString(CultureInfo.InvariantCulture));
			}
			state.RaisePactMessage(new CPactMessage(Message, "", "", warning.Invader, warning.Player)
			{
				Told = warning.Player,
				Messenger = "MESSAGE_HEAD_MARS",
				Values = new[] { ("ENEMY", state.Country(warning.Invader)?.Name ?? warning.Invader.Value),
					("PROV", string.Join(",", provinces)) },
			});
		}
	}
}
