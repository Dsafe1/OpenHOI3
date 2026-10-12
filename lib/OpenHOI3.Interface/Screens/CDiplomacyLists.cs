using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// Who the diplomacy screen's country list holds: its five tabs, a faction's button and a continent's,
/// and the nine sorts. See <c>docs/mechanics/diplomacyscreen.md</c>, sections 2 and 3.
/// </summary>
public static class CDiplomacyLists
{
	/// <summary>The continents no button stands for.</summary>
	private static readonly HashSet<string> Unlisted = new(StringComparer.Ordinal) { "lake", "greenland" };

	/// <summary>Majors: every country owning a province that is major or whose provinces come to more than 100 IC.</summary>
	public static List<CCountry> Majors(CGameState state)
	{
		bool[] owns = CRelations.Owning(state);
		return state.Countries.Where(country => owns[country.Index] && (country.IsMajor || country.BaseIc > 100)).ToList();
	}

	/// <summary>Neighbours: the player's, not the chosen country's.</summary>
	public static List<CCountry> Neighbours(CGameState state, CCountry player)
	{
		bool[] owns = CRelations.Owning(state);
		return CCountryLists.Neighbours(state, player).Where(country => owns[country.Index]).ToList();
	}

	/// <summary>Unaligned: every country owning a province that is in no faction.</summary>
	public static List<CCountry> Unaligned(CGameState state)
	{
		bool[] owns = CRelations.Owning(state);
		return state.Countries.Where(country => owns[country.Index] && country.Faction.Length == 0).ToList();
	}

	/// <summary>All: every country owning a province, by name.</summary>
	public static List<CCountry> All(CGameState state)
	{
		bool[] owns = CRelations.Owning(state);
		return ByName(state.Countries.Where(country => owns[country.Index]));
	}

	/// <summary>A faction's button: its members that own a province, by name.</summary>
	public static List<CCountry> FactionMembers(CGameState state, string faction)
	{
		bool[] owns = CRelations.Owning(state);
		return ByName(CCountryLists.FactionMembers(state, faction).Where(country => owns[country.Index]));
	}

	/// <summary>A continent's button: every country owning a province whose capital is on it, by name.</summary>
	public static List<CCountry> OnContinent(CGameState state, string continent)
	{
		bool[] owns = CRelations.Owning(state);
		return ByName(state.Countries.Where(country => owns[country.Index] && country.Capital > 0
			&& string.Equals(state.Province(country.Capital).Continent, continent, StringComparison.Ordinal)));
	}

	/// <summary>The continents a button stands for, in the file's order.</summary>
	public static IEnumerable<string> ListedContinents(CGameState state)
	{
		return state.Continents.Where(continent => !Unlisted.Contains(continent));
	}

	/// <summary>
	/// The names' bytes compared as unsigned numbers - case and all, accented letters after <c>z</c> -
	/// equal names keeping their order. Game text is read a byte to a character, so ordinal is that.
	/// </summary>
	public static List<CCountry> ByName(IEnumerable<CCountry> countries)
	{
		return countries.OrderBy(country => country.Name, StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// The list in a sort button's order, the flag set or not (section 3.1). The list itself is not
	/// changed: the next remaking undoes a sort. Equals keep the list's order (ours: not read).
	/// </summary>
	public static List<CCountry> Sorted(CGameState state, CCountry player, IReadOnlyList<CCountry> countries,
		EDiplomacySort sort, bool flag)
	{
		if (sort == EDiplomacySort.Name)
		{
			return flag
				? countries.OrderByDescending(country => country.Name, StringComparer.Ordinal).ToList()
				: countries.OrderBy(country => country.Name, StringComparer.Ordinal).ToList();
		}
		Func<CCountry, long> key = sort == EDiplomacySort.Threat
			? country => CGameState.Status(player, country).Threat.Raw
			: country => CTopBarTooltips.DayChange(country, Goods(sort)).Raw;
		return flag ? countries.OrderByDescending(key).ToList() : countries.OrderBy(key).ToList();
	}

	/// <summary>The good a resource sort button sorts by.</summary>
	public static EGoods Goods(EDiplomacySort sort)
	{
		return sort switch
		{
			EDiplomacySort.Energy => EGoods.Energy,
			EDiplomacySort.Metal => EGoods.Metal,
			EDiplomacySort.RareMaterials => EGoods.RareMaterials,
			EDiplomacySort.CrudeOil => EGoods.CrudeOil,
			EDiplomacySort.Supplies => EGoods.Supplies,
			EDiplomacySort.Fuel => EGoods.Fuel,
			EDiplomacySort.Money => EGoods.Money,
			_ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "not a good"),
		};
	}
}
