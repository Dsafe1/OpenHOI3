using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The lists of countries the original rebuilds once a day - who we are at war with, our allies,
/// our neighbours, the nations we could release - worked out here when they are asked for. Each
/// comes in the order the countries were declared. See <c>docs/mechanics/triggers.md</c>,
/// sections 2 and 6.
/// </summary>
public static class CCountryLists
{
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
		var enemies = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (AtWar(state, country.Tag, other.Tag))
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
	/// The nations the country could release: a country that owns no province, has a core on a
	/// province the country owns and controls other than its capital, and has its capital owned by
	/// the country.
	/// </summary>
	public static List<CCountry> PossibleVassals(CGameState state, CCountry country)
	{
		var vassals = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (other == country || OwnedCount(state, other.Tag) > 0
				|| state.Province(other.Capital).Owner != country.Tag || other.Capital == 0)
			{
				continue;
			}
			foreach (CProvince province in state.Provinces)
			{
				if (province.Id != country.Capital && province.Owner == country.Tag && province.Controller == country.Tag
					&& province.Cores.Contains(other.Tag))
				{
					vassals.Add(other);
					break;
				}
			}
		}
		return vassals;
	}

	/// <summary>How many provinces the country owns.</summary>
	public static int OwnedCount(CGameState state, CCountryTag? country)
	{
		if (country is null)
		{
			return 0;
		}
		int count = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == country)
			{
				count++;
			}
		}
		return count;
	}
}
