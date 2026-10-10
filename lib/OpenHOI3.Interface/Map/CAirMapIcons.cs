using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What the air map mode puts on a province besides its colours: the three icons of the past
/// week's air battles and bombings, and the size of its large air base and anti-aircraft
/// pictures. See <c>docs/mechanics/airmap.md</c>, sections 4 and 5.
/// </summary>
public static class CAirMapIcons
{
	public const string BombedUsPicture = "air_bombed_us_icon";
	public const string BombedThemPicture = "air_bombed_them_icon";
	public const string BattlePicture = "air_battle_icon";
	public const string AirBasePicture = "large_air_base";
	public const string AntiAirPicture = "large_anti_air";

	/// <summary>How far an icon stands from its province's point, in map pixels.</summary>
	public const float IconOffset = 3f;

	/// <summary>The building the large anti-aircraft picture is of. The air base's is <see cref="CMovementRules.AirBase"/>.</summary>
	public const string AntiAir = "anti_air";

	private const string Rebels = "REB";

	/// <summary>
	/// The three counts of every province, by its id, out of the list of finished battles'
	/// air battles and bombings. Province 0 has none.
	/// </summary>
	public static CAirMapCount[] Count(CGameState state, CCountryTag? player)
	{
		var bombedUs = new byte[state.Provinces.Count];
		var bombedThem = new byte[state.Provinces.Count];
		var battles = new byte[state.Provinces.Count];
		foreach (CFinishedCombat entry in state.CombatHistory.Entries)
		{
			bool bombing = CBombing.IsBombing(entry.Kind);
			if ((!bombing && entry.Kind != ECombatKind.Air) || entry.Province <= 0 || entry.Province >= state.Provinces.Count)
			{
				continue;
			}
			byte[] counts;
			if (IsOurs(state, entry, player))
			{
				counts = bombing ? bombedUs : battles;
			}
			else if (IsTheirs(state, entry, player))
			{
				counts = bombing ? bombedThem : battles;
			}
			else
			{
				continue;
			}
			counts[entry.Province] = unchecked((byte)(counts[entry.Province] + 1));
		}
		var found = new CAirMapCount[state.Provinces.Count];
		for (int id = 1; id < found.Length; id++)
		{
			found[id] = new CAirMapCount(unchecked((sbyte)bombedUs[id]), unchecked((sbyte)bombedThem[id]),
				unchecked((sbyte)battles[id]));
		}
		return found;
	}

	/// <summary>
	/// Whether a finished battle counts as the player's side's: the player holds or owns the
	/// province, or is the defender, or the defender is on its side, or fights beside it and
	/// is not its enemy.
	/// </summary>
	public static bool IsOurs(CGameState state, CFinishedCombat entry, CCountryTag? player)
	{
		if (state.Country(player) is not { } us)
		{
			return false;
		}
		CProvince province = state.Province(entry.Province);
		if (province.Controller == us.Tag || province.Owner == us.Tag || entry.Defender == us.Tag)
		{
			return true;
		}
		if (entry.Defender is not { } defender || defender.Value == Rebels || state.Country(defender) is not { } them)
		{
			return false;
		}
		return CSupplyRelations.IsSameSide(us, them)
			|| (CSupplyRelations.IsFightingWith(state, us, them) && !IsEnemy(state, us.Tag, defender));
	}

	/// <summary>
	/// Whether a finished battle that is not the player's side's counts as done by it: it
	/// attacked, or it may operate in the attacker's ground, or it is at war with the defender.
	/// </summary>
	public static bool IsTheirs(CGameState state, CFinishedCombat entry, CCountryTag? player)
	{
		if (state.Country(player) is not { } us)
		{
			return false;
		}
		if (entry.Attacker == us.Tag)
		{
			return true;
		}
		if (entry.Attacker is { } attacker && state.Country(attacker) is { } them && CSupplyRelations.CanOperateIn(state, us, them))
		{
			return true;
		}
		return IsEnemy(state, us.Tag, entry.Defender);
	}

	/// <summary>
	/// The original's test of an enemy: never oneself, always the rebels, and otherwise at war.
	/// A side named by nobody is no enemy.
	/// </summary>
	public static bool IsEnemy(CGameState state, CCountryTag country, CCountryTag? other)
	{
		if (other is not { } them || them == country)
		{
			return false;
		}
		return them.Value == Rebels || country.Value == Rebels || CMovementRules.IsEnemy(state, country, them);
	}

	/// <summary>
	/// The figure a large picture is sized by: the level built of the building where the player
	/// knows the province better than 1, otherwise 1 where there is any of it, else 0.
	/// </summary>
	public static int Level(CGameState state, CProvince province, string building, CCountryTag? player)
	{
		int built = (int)(CMovementRules.BuildingLevel(state, province, building).Raw / CFixedPoint.Scale);
		bool known = player is { } tag && CIntelligence.Level(state, province, tag) > 1;
		return known ? built : built > 0 ? 1 : 0;
	}

	/// <summary>How many times its scale a large picture of this level is drawn at: (level - 1) x 0.2 + 1.</summary>
	public static float Size(int level)
	{
		return (float)((level - 1) * 0.2 + 1.0);
	}
}
