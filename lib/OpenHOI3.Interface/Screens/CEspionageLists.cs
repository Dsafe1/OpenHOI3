using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// Who the intelligence screen's country list holds - its tabs, a faction's button and a continent's - and its five
/// sorts. The player is on none of them. See <c>docs/mechanics/espionagescreen.md</c>, section 2.
/// </summary>
public static class CEspionageLists
{
	/// <summary>The IC above which a country that is not declared major counts as one.</summary>
	private const int MajorIc = 100;

	/// <summary>All: every country owning a province but the player, by name.</summary>
	public static List<CCountry> All(CGameState state, CCountry player)
	{
		bool[] owns = CRelations.Owning(state);
		return CDiplomacyLists.ByName(state.Countries.Where(country => owns[country.Index] && country != player));
	}

	/// <summary>Majors: owning a province, not the player, major or with provinces of more than 100 IC, in the countries' order.</summary>
	public static List<CCountry> Majors(CGameState state, CCountry player)
	{
		bool[] owns = CRelations.Owning(state);
		return state.Countries.Where(country => owns[country.Index] && country != player
			&& (country.IsMajor || country.BaseIc > MajorIc)).ToList();
	}

	/// <summary>Neighbours: the player's, owning a province, in their order.</summary>
	public static List<CCountry> Neighbours(CGameState state, CCountry player)
	{
		bool[] owns = CRelations.Owning(state);
		return CCountryLists.Neighbours(state, player).Where(country => owns[country.Index]).ToList();
	}

	/// <summary>Unaligned: owning a province, in no faction, neither the player nor the chosen country.</summary>
	public static List<CCountry> Unaligned(CGameState state, CCountry player, CCountry? chosen)
	{
		bool[] owns = CRelations.Owning(state);
		return state.Countries.Where(country => owns[country.Index] && country.Faction.Length == 0 && country != player
			&& country != chosen).ToList();
	}

	/// <summary>A faction's button: its members owning a province, not the player, by name.</summary>
	public static List<CCountry> FactionMembers(CGameState state, CCountry player, string faction)
	{
		return CDiplomacyLists.FactionMembers(state, faction).Where(country => country != player).ToList();
	}

	/// <summary>A continent's button: every country owning a province whose capital is on it, not the player, by name.</summary>
	public static List<CCountry> OnContinent(CGameState state, CCountry player, string continent)
	{
		return CDiplomacyLists.OnContinent(state, continent).Where(country => country != player).ToList();
	}

	/// <summary>
	/// The list in a sort's order, largest or Z first where <paramref name="descending"/> is set - the Priority sort
	/// always so - equals keeping their order.
	/// </summary>
	public static List<CCountry> Sorted(CCountry player, IReadOnlyList<CCountry> countries, ESpySort sort, bool descending)
	{
		if (sort == ESpySort.Name)
		{
			return descending
				? countries.OrderByDescending(country => country.Name, StringComparer.Ordinal).ToList()
				: countries.OrderBy(country => country.Name, StringComparer.Ordinal).ToList();
		}
		Func<CCountry, long> key = sort switch
		{
			ESpySort.Threat => country => CGameState.Status(player, country).Threat.Raw,
			ESpySort.Covert => country => CGameState.Presence(player, country).CovertOpsPoints,
			ESpySort.SpyLevel => country => CGameState.Presence(player, country).Level,
			_ => country => CGameState.Presence(player, country).Priority,
		};
		return descending || sort == ESpySort.Priority ? countries.OrderByDescending(key).ToList() : countries.OrderBy(key).ToList();
	}

	/// <summary>
	/// The country a new game's screen starts on: the one the player feels the most threat from, of every country owning
	/// a province, the first of equals winning - with no threat anywhere the first of them.
	/// </summary>
	public static CCountry? FirstChosen(CGameState state, CCountry player)
	{
		bool[] owns = CRelations.Owning(state);
		CCountry? best = null;
		long most = -1000000;
		foreach (CCountry country in state.Countries)
		{
			if (!owns[country.Index] || country == player)
			{
				continue;
			}
			long threat = CGameState.Status(player, country).Threat.Raw;
			if (threat > most)
			{
				most = threat;
				best = country;
			}
		}
		return best;
	}
}
