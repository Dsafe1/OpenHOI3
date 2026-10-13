using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// How two countries stand to each other where supply asks. See
/// <c>docs/mechanics/supply.md</c>, section 3.
/// </summary>
public static class CSupplyRelations
{
	private const string Rebels = "REB";

	/// <summary>
	/// On one side: the same country, a puppet and its master, and two puppets of one
	/// master. Never the rebels.
	/// </summary>
	public static bool IsSameSide(CCountry country, CCountry other)
	{
		if (other.Tag.Value == Rebels)
		{
			return false;
		}
		if (other.Tag == country.Tag || other.Overlord == country.Tag || country.Overlord == other.Tag)
		{
			return true;
		}
		return country.Overlord is { } master && other.Overlord == master;
	}

	/// <summary>
	/// Fighting a war on the same side as each other: both among the attackers of one
	/// war, or both among its defenders.
	/// </summary>
	public static bool IsFightingWith(CGameState state, CCountry country, CCountry other)
	{
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(country.Tag) && war.Attackers.Contains(other.Tag))
				|| (war.Defenders.Contains(country.Tag) && war.Defenders.Contains(other.Tag)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// May use the other's ground: on one side with it, or fighting a war beside it and
	/// not at war with it.
	/// </summary>
	public static bool CanOperateIn(CGameState state, CCountry country, CCountry other)
	{
		if (other.Tag.Value == Rebels)
		{
			return false;
		}
		return IsSameSide(country, other)
			|| (IsFightingWith(state, country, other) && !CMovementRules.IsEnemy(state, country.Tag, other.Tag));
	}

	/// <summary>
	/// A friend: on one side, in one faction, allied, or fighting a war beside it and
	/// not at war with it.
	/// </summary>
	public static bool IsFriendly(CGameState state, CCountry country, CCountry other)
	{
		if (IsSameSide(country, other))
		{
			return true;
		}
		if (country.Faction.Length > 0 && country.Faction == other.Faction)
		{
			return true;
		}
		foreach (CAlliance alliance in state.Alliances)
		{
			if (alliance.Joins(country.Tag, other.Tag))
			{
				return true;
			}
		}
		return IsFightingWith(state, country, other) && !CMovementRules.IsEnemy(state, country.Tag, other.Tag);
	}
}
