using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the air map mode says of a province, in the original's own words: how many bombings
/// and how many air battles it saw in the past week, bombings first. A battle is counted
/// where the player holds or owns the province or the battle concerns the player - a test of
/// its own, not the icons'. See <c>docs/mechanics/airmap.md</c>, section 7.
/// </summary>
public static class CAirMapText
{
	public const string BombingKey = "AIR_TOOLTIP_BOMBING";
	public const string BattleKey = "AIR_TOOLTIP_DOGFIGHT";

	private const string Rebels = "REB";

	public static List<string> Lines(Func<string, string> localise, CGameState state, CProvince province, CCountryTag? player)
	{
		var lines = new List<string>();
		if (player is not { } us)
		{
			return lines;
		}
		bool ours = province.Controller == us || province.Owner == us;
		int bombings = 0;
		int battles = 0;
		foreach (CFinishedCombat entry in state.CombatHistory.Entries)
		{
			if (entry.Province != province.Id || !(ours || Concerns(state, entry, us)))
			{
				continue;
			}
			if (entry.Kind == ECombatKind.Air)
			{
				battles++;
			}
			else if (CBombing.IsBombing(entry.Kind))
			{
				bombings++;
			}
		}
		if (bombings > 0)
		{
			lines.Add(CTextTemplate.Fill(localise(BombingKey), ("NUM", bombings.ToString())));
		}
		if (battles > 0)
		{
			lines.Add(CTextTemplate.Fill(localise(BattleKey), ("NUM", battles.ToString())));
		}
		return lines;
	}

	/// <summary>
	/// Whether a finished battle concerns a country: it attacked; or the attacker, not the
	/// rebels, is on its side or fights beside it and is not its enemy; or it defended; or it
	/// is at war with the defender, or may operate in the defender's ground.
	/// </summary>
	public static bool Concerns(CGameState state, CFinishedCombat entry, CCountryTag country)
	{
		if (state.Country(country) is not { } us)
		{
			return false;
		}
		if (entry.Attacker == country)
		{
			return true;
		}
		if (entry.Attacker is { } attacker && attacker.Value != Rebels && state.Country(attacker) is { } them
			&& (CSupplyRelations.IsSameSide(us, them)
				|| (CSupplyRelations.IsFightingWith(state, us, them) && !CAirMapIcons.IsEnemy(state, country, attacker))))
		{
			return true;
		}
		if (entry.Defender == country || CAirMapIcons.IsEnemy(state, country, entry.Defender))
		{
			return true;
		}
		return entry.Defender is { } defender && state.Country(defender) is { } held
			&& CSupplyRelations.CanOperateIn(state, us, held);
	}
}
