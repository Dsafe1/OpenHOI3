using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What the naval map mode puts on a province besides its colours: four icons of the past week's naval battles, and the large
/// naval base picture - sized as the air mode's large pictures are (<see cref="CAirMapIcons.Level"/>). Made again when the mode
/// is chosen and each day. See <c>docs/mechanics/mapmodes.md</c>, section 11, and
/// <c>reversing/findings/FINDINGS-mapmodesrest.md</c>, section 3.2.
/// </summary>
public static class CNavalMapIcons
{
	public const string ConvoysRaidedPicture = "naval_convoy_raided_icon";
	public const string ConvoysSunkPicture = "naval_convoy_sunk_icon";
	public const string RaiderDefendedPicture = "naval_raider_defended_icon";
	public const string RaiderAttackedPicture = "naval_raider_attacked_icon";
	public const string NavalBasePicture = "large_naval_base";

	private const string Rebels = "REB";

	/// <summary>The four flags of every province, by its id, out of the list of finished battles' naval battles.</summary>
	public static CNavalMapFlags[] Flags(CGameState state, CCountryTag? player)
	{
		var flags = new CNavalMapFlags[state.Provinces.Count];
		if (state.Country(player) is not { } us)
		{
			return flags;
		}
		foreach (CFinishedCombat entry in state.CombatHistory.Entries)
		{
			// A naval bombing is not counted.
			if (entry.Kind != ECombatKind.Naval || entry.Province <= 0 || entry.Province >= state.Provinces.Count)
			{
				continue;
			}
			bool attacking = Attacking(state, us, entry);
			bool defending = entry.Defender == us.Tag
				|| (state.Country(entry.Defender) is { } defender && CSupplyRelations.CanOperateIn(state, us, defender));
			CNavalMapFlags here = flags[entry.Province];
			if (entry.IsConvoy)
			{
				if (!here.ConvoysSunk && attacking)
				{
					here = here with { ConvoysSunk = true };
				}
				else if (!here.ConvoysRaided && defending)
				{
					here = here with { ConvoysRaided = true };
				}
			}
			else if (!here.RaiderAttacked && attacking)
			{
				here = here with { RaiderAttacked = true };
			}
			else if (!here.RaiderDefended && defending)
			{
				here = here with { RaiderDefended = true };
			}
			flags[entry.Province] = here;
		}
		return flags;
	}

	/// <summary>
	/// Whether the player's side attacked: the player did, or a country on its side, or one fighting beside it that is no
	/// enemy - the rebels never - or the player is at war with the defender.
	/// </summary>
	private static bool Attacking(CGameState state, CCountry us, CFinishedCombat entry)
	{
		if (entry.Attacker == us.Tag)
		{
			return true;
		}
		if (entry.Attacker is { } tag && tag.Value != Rebels && state.Country(tag) is { } attacker
			&& (CSupplyRelations.IsSameSide(us, attacker)
				|| (CSupplyRelations.IsFightingWith(state, us, attacker) && !CAirMapIcons.IsEnemy(state, us.Tag, tag))))
		{
			return true;
		}
		return CAirMapIcons.IsEnemy(state, us.Tag, entry.Defender);
	}
}
