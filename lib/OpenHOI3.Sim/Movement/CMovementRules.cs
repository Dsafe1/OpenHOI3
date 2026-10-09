using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// Where a unit may go: whose ground a country may walk on, which province a unit may
/// stand in, and which step of a route it may take. The rules are the original's, in
/// <c>docs/mechanics/movement.md</c>, section 3 - less what they ask of things this
/// project does not have yet, which that section lists.
/// </summary>
public static class CMovementRules
{
	/// <summary>Whether two countries are fighting each other in some war.</summary>
	public static bool IsEnemy(CGameState state, CCountryTag? a, CCountryTag? b)
	{
		if (a is not { } first || b is not { } second || first == second)
		{
			return false;
		}
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(first) && war.Defenders.Contains(second))
				|| (war.Attackers.Contains(second) && war.Defenders.Contains(first)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether a country counts another as a friend: itself, and a member of its own
	/// faction. The original also counts an overlord or a puppet, an ally, a
	/// co-belligerent and a country it has military access to.
	/// </summary>
	public static bool IsFriendly(CGameState state, CCountry country, CCountryTag? other)
	{
		if (other is null)
		{
			return false;
		}
		if (other == country.Tag)
		{
			return true;
		}
		return country.Faction.Length > 0 && state.Country(other) is { } theirs
			&& theirs.Faction == country.Faction;
	}

	/// <summary>
	/// Whose ground a country may walk on: its own or a friend's, ground that belongs to
	/// nobody, and ground of anyone it is at war with. The sea is anyone's.
	/// </summary>
	public static bool MayEnter(CGameState state, CCountry country, CProvince province)
	{
		if (province.Id == 0)
		{
			return false;
		}
		if (province.IsSea || province.Owner is null)
		{
			return true;
		}
		if (IsEnemy(state, country.Tag, province.Owner) || IsEnemy(state, country.Tag, province.Controller))
		{
			return true;
		}
		if (IsFriendly(state, country, province.Controller))
		{
			return true;
		}
		return IsFriendly(state, country, province.Owner) && !IsEnemy(state, province.Owner, province.Controller);
	}

	/// <summary>
	/// Whether a unit may stand in a province. A land unit may not stand in the sea -
	/// being carried there is not built - and stands on land held by whoever holds the
	/// ground under it now, or on ground its country may walk on.
	/// </summary>
	public static bool MayStandIn(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		if (province.Id == 0)
		{
			return false;
		}
		bool isLand = unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air;
		if (isLand && province.IsSea)
		{
			return false;
		}
		CProvince here = state.Province(unit.Location);
		return province.Controller == here.Controller || MayEnter(state, country, province);
	}

	/// <summary>
	/// Whether a unit may be ordered to a province at all: it has to be able to stand
	/// there, and one too weak, or with nothing to eat or to burn, is not sent onto an
	/// enemy's ground. Only the first is asked so far, there being no enemy to meet.
	/// </summary>
	public static bool MayMoveTo(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		return MayStandIn(state, country, unit, province);
	}

	/// <summary>
	/// Whether a unit may take one step of a route. In the original's order: not over
	/// an impassable edge; not over a crossing through a province an enemy holds; and
	/// then only into a province it may stand in.
	/// </summary>
	public static bool MayStep(CGameState state, CCountry country, CUnit unit, CProvince from, CProvince to,
		CProvinceEdge edge)
	{
		if (edge.Kind == EProvinceEdgeKind.Impassable || from.Id == to.Id)
		{
			return false;
		}
		if (IsCrossingBlocked(state, country, unit, edge))
		{
			return false;
		}
		return MayStandIn(state, country, unit, to);
	}

	/// <summary>
	/// Whether a crossing is closed to a unit: it goes through a land province an enemy
	/// holds. Through a sea province it is an enemy fleet standing there that closes it,
	/// which waits for fleets that fight. Nothing closes a crossing to an air unit.
	/// </summary>
	public static bool IsCrossingBlocked(CGameState state, CCountry country, CUnit unit, CProvinceEdge edge)
	{
		if (edge.Kind != EProvinceEdgeKind.Crossing || edge.Through == 0 || unit.Kind == EUnitKind.Air)
		{
			return false;
		}
		CProvince crossed = state.Province(edge.Through);
		return !crossed.IsSea && IsEnemy(state, country.Tag, crossed.Controller);
	}
}
