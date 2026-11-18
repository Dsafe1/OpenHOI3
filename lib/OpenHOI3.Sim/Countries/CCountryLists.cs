using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The lists of countries the original rebuilds once a day - who we are at war with, our allies,
/// our neighbours, the nations we could release - worked out here when they are asked for. Each
/// comes in the order the countries were declared. See <c>docs/mechanics/triggers.md</c>,
/// sections 2 and 6.
/// </summary>
public static class CCountryLists
{
	/// <summary>The original's <c>IsEnemy</c>: not itself, and at war with it or either is the rebels.</summary>
	public static bool IsEnemy(CGameState state, CCountry country, CCountryTag other)
	{
		if (other == country.Tag)
		{
			return false;
		}
		return AtWar(state, country.Tag, other) || other.Value == "REB" || country.Tag.Value == "REB";
	}

	/// <summary>
	/// The original's <c>IsFriend</c>, in its order: itself; either the other's overlord, or one overlord over both; one
	/// real faction; an alliance in the other's record towards it; fighting on one side and not its enemy; and, with
	/// <paramref name="withAccess"/>, its own record's military access through the other.
	/// </summary>
	public static bool IsFriend(CGameState state, CCountry country, CCountryTag tag, bool withAccess)
	{
		if (tag == country.Tag)
		{
			return true;
		}
		CCountry? other = state.Country(tag);
		if (other?.Overlord == country.Tag || country.Overlord == tag)
		{
			return true;
		}
		if (country.Overlord is { } overlord && other?.Overlord == overlord)
		{
			return true;
		}
		if (country.Faction.Length > 0 && other?.Faction == country.Faction)
		{
			return true;
		}
		if (HasAlliance(state, tag, country.Tag))
		{
			return true;
		}
		if (FightingTogether(state, tag, country.Tag) && !IsEnemy(state, country, tag))
		{
			return true;
		}
		return withAccess && other is not null && CGameState.Status(country, other).HasMilitaryAccess;
	}
	/// <summary>Whether the two are on opposite sides of a war.</summary>
	public static bool AtWar(CGameState state, CCountryTag one, CCountryTag other)
	{
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(one) && war.Defenders.Contains(other))
				|| (war.Attackers.Contains(other) && war.Defenders.Contains(one)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The countries at war with the country.</summary>
	public static List<CCountry> Enemies(CGameState state, CCountry country)
	{
		// The other sides of its own wars, and then the countries in their order: the same as asking each country.
		HashSet<CCountryTag>? opposed = null;
		foreach (CWar war in state.Wars)
		{
			if (war.Attackers.Contains(country.Tag))
			{
				(opposed ??= new HashSet<CCountryTag>()).UnionWith(war.Defenders);
			}
			if (war.Defenders.Contains(country.Tag))
			{
				(opposed ??= new HashSet<CCountryTag>()).UnionWith(war.Attackers);
			}
		}
		var enemies = new List<CCountry>();
		if (opposed is null)
		{
			return enemies;
		}
		foreach (CCountry other in state.Countries)
		{
			if (opposed.Contains(other.Tag))
			{
				enemies.Add(other);
			}
		}
		return enemies;
	}

	/// <summary>
	/// The country's allies: every country it has an alliance with that owns a province, then
	/// every other member of its faction not already there.
	/// </summary>
	public static List<CCountry> Allies(CGameState state, CCountry country)
	{
		var allies = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (other != country && HasAlliance(state, country.Tag, other.Tag) && OwnedCount(state, other.Tag) > 0)
			{
				allies.Add(other);
			}
		}
		foreach (CCountry member in FactionMembers(state, country.Faction))
		{
			if (member != country && !allies.Contains(member))
			{
				allies.Add(member);
			}
		}
		return allies;
	}

	/// <summary>
	/// The allies a country can count on, the original's list after its allies (<c>strong_allies_at_war</c>): of
	/// <see cref="Allies"/>, in its order, each not a government in exile, whose acting capital is on the continent of
	/// the country's, whose maximum IC is at least half the country's - halved toward nought - and which is at war.
	/// See <c>docs/mechanics/politics.md</c>, section 6.
	/// </summary>
	public static List<CCountry> StrongAlliesAtWar(CGameState state, CCountry country)
	{
		var strong = new List<CCountry>();
		string continent = state.Province(country.ActingCapital).Continent;
		foreach (CCountry ally in Allies(state, country))
		{
			if (!ally.InExile && state.Province(ally.ActingCapital).Continent == continent
				&& ally.BaseIc >= country.BaseIc / 2 && state.IsAtWar(ally.Tag))
			{
				strong.Add(ally);
			}
		}
		return strong;
	}

	/// <summary>Whether either guarantees the other.</summary>
	public static bool HasGuarantee(CGameState state, CCountryTag one, CCountryTag other)
	{
		foreach (CGuarantee guarantee in state.Guarantees)
		{
			if (guarantee.Joins(one, other))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The original's <c>CDiplomacyStatus::HasFriendlyAgreement</c>, of one country's record towards another, in its order: an
	/// alliance, the holder's military access, a vassal bond with either at either end, or a guarantee either way.
	/// </summary>
	public static bool HasFriendlyAgreement(CGameState state, CCountryTag holder, CCountryTag other)
	{
		if (HasAlliance(state, holder, other))
		{
			return true;
		}
		CCountry? holding = state.Country(holder);
		CCountry? towards = state.Country(other);
		if (holding is not null && towards is not null && CGameState.Status(holding, towards).HasMilitaryAccess)
		{
			return true;
		}
		if (holding?.Overlord == other || towards?.Overlord == holder)
		{
			return true;
		}
		return HasGuarantee(state, holder, other);
	}

	public static bool HasAlliance(CGameState state, CCountryTag one, CCountryTag other)
	{
		foreach (CAlliance alliance in state.Alliances)
		{
			if (alliance.Joins(one, other))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The countries in the faction of that name; none for no name.</summary>
	public static List<CCountry> FactionMembers(CGameState state, string faction)
	{
		var members = new List<CCountry>();
		if (faction.Length == 0)
		{
			return members;
		}
		foreach (CCountry other in state.Countries)
		{
			if (other.Faction.Equals(faction, StringComparison.Ordinal))
			{
				members.Add(other);
			}
		}
		// By seat, and of one seat in the countries' order: a stable sort.
		if (members.Exists(member => member.FactionSeat != 0))
		{
			members = new List<CCountry>(System.Linq.Enumerable.OrderBy(members, member => member.FactionSeat));
		}
		return members;
	}

	/// <summary>The countries whose overlord the country is.</summary>
	public static int VassalCount(CGameState state, CCountry country)
	{
		int count = 0;
		foreach (CCountry other in state.Countries)
		{
			if (other.Overlord == country.Tag)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// The owners of the provinces beside the ones the country owns, where that province's owner
	/// also controls it. The country itself is not among them (ours: not read).
	/// </summary>
	public static List<CCountry> Neighbours(CGameState state, CCountry country)
	{
		var found = new HashSet<CCountryTag>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != country.Tag)
			{
				continue;
			}
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.Owner is { } owner && beside.Controller == owner && owner != country.Tag)
				{
					found.Add(owner);
				}
			}
		}
		var neighbours = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (found.Contains(other.Tag))
			{
				neighbours.Add(other);
			}
		}
		return neighbours;
	}

	/// <summary>
	/// The controllers of the provinces beside the ones the country controls, where that province is
	/// owned by somebody and neither owned nor controlled by the country - so occupation moves a
	/// country from <see cref="Neighbours"/> to this list. See <c>docs/mechanics/lua.md</c>, section 4.
	/// </summary>
	public static List<CCountry> ControllerNeighbours(CGameState state, CCountry country)
	{
		var found = new HashSet<CCountryTag>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller != country.Tag)
			{
				continue;
			}
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.Owner is { } owner && owner != country.Tag && beside.Controller is { } controller
					&& controller != country.Tag)
				{
					found.Add(controller);
				}
			}
		}
		var neighbours = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (found.Contains(other.Tag))
			{
				neighbours.Add(other);
			}
		}
		return neighbours;
	}

	/// <summary>Whether the two, being two, are on one side of a war.</summary>
	public static bool FightingTogether(CGameState state, CCountryTag one, CCountryTag other)
	{
		if (one == other)
		{
			return false;
		}
		foreach (CWar war in state.Wars)
		{
			if ((war.Attackers.Contains(one) && war.Attackers.Contains(other))
				|| (war.Defenders.Contains(one) && war.Defenders.Contains(other)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The nations the country could release: a country that owns no province, has a core on a
	/// province the country owns and controls other than its capital, and has its capital owned by
	/// the country.
	/// </summary>
	public static List<CCountry> PossibleVassals(CGameState state, CCountry country)
	{
		// By the provinces it owns and holds and each one's cores, so in the order they are first come on.
		var vassals = new List<CCountry>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == country.Capital || province.Owner != country.Tag || province.Controller != country.Tag)
			{
				continue;
			}
			foreach (CCountryTag core in province.Cores)
			{
				if (state.Country(core) is { } other && other != country && !vassals.Contains(other) && OwnedCount(state, other.Tag) == 0
					&& other.Capital != 0 && state.Province(other.Capital).Owner == country.Tag)
				{
					vassals.Add(other);
				}
			}
		}
		return vassals;
	}

	/// <summary>
	/// The governments in exile a country may liberate: each that still owns land, whose capital this country holds and that is not
	/// its enemy - of any faction or none, whatever the screen's words say. See <c>docs/mechanics/politics.md</c>.
	/// </summary>
	public static List<CCountry> PossibleLiberations(CGameState state, CCountry country)
	{
		var exiles = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (other != country && other.InExile && OwnedCount(state, other.Tag) > 0 && other.Capital != 0
				&& state.Province(other.Capital).Controller == country.Tag && !IsEnemy(state, other, country.Tag))
			{
				exiles.Add(other);
			}
		}
		return exiles;
	}

	/// <summary>How many provinces the country owns.</summary>
	public static int OwnedCount(CGameState state, CCountryTag? country)
	{
		return country is { } tag ? COwnedCounts.Of(state).Count(tag) : 0;
	}

	/// <summary>
	/// The provinces a country controls, in the order of the original's list of them: as it took them
	/// (<see cref="CProvince.ControlOrder"/>), the province's number between equals.
	/// </summary>
	public static List<CProvince> Controlled(CGameState state, CCountryTag? country)
	{
		// A list of the caller's own, copied from the one kept until a province changes hands (CControlledLists).
		return country is { } tag ? new List<CProvince>(CControlledLists.Of(state, tag)) : new List<CProvince>();
	}

	/// <summary>Puts provinces into the order of their controllers' lists, the province's number between equals.</summary>
	public static void SortByControlOrder(List<CProvince> provinces)
	{
		provinces.Sort((a, b) => a.ControlOrder != b.ControlOrder ? a.ControlOrder.CompareTo(b.ControlOrder) : a.Id.CompareTo(b.Id));
	}
}
