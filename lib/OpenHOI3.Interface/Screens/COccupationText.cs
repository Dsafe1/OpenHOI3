using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the politics screen says of occupation: the countries whose land the player holds at war, the policy towards
/// each, and the choice of a policy with its rows and tooltips. See <c>docs/mechanics/occupation.md</c>, section 4.
/// </summary>
public static class COccupationText
{
	public const string ListName = "policies_listbox";
	public const string RowName = "current_policy_entry";
	public const string WindowName = "policy_selection_window";
	public const string ChoiceListName = "policy_list";
	public const string ChoiceRowName = "possible_policy_entry";

	private const string Rebels = "REB";

	/// <summary>
	/// In the game's order of countries, each that owns a province, is not the player, is at war with it - or either is
	/// the rebels - and owns a province the player holds.
	/// </summary>
	public static List<CCountry> OccupiedCountries(CGameState state, CCountry player)
	{
		var owning = new HashSet<CCountryTag>();
		var heldByPlayer = new HashSet<CCountryTag>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is not { } owner)
			{
				continue;
			}
			owning.Add(owner);
			if (province.Controller == player.Tag)
			{
				heldByPlayer.Add(owner);
			}
		}
		var occupied = new List<CCountry>();
		foreach (CCountry country in state.Countries)
		{
			if (country == player || !owning.Contains(country.Tag) || !heldByPlayer.Contains(country.Tag))
			{
				continue;
			}
			if (CCountryLists.AtWar(state, player.Tag, country.Tag) || country.Tag.Value == Rebels || player.Tag.Value == Rebels)
			{
				occupied.Add(country);
			}
		}
		return occupied;
	}

	/// <summary>A policy's name: its key's text.</summary>
	public static string Name(Func<string, string> localise, COccupationPolicy policy)
	{
		return localise(policy.Name);
	}

	/// <summary>The policies the choice lists: every one of the file in its order, <c>no_occupation</c> left out.</summary>
	public static IEnumerable<COccupationPolicy> Choices(CGameState state)
	{
		foreach (COccupationPolicy policy in state.OccupationPolicies)
		{
			if (policy.IsReal)
			{
				yield return policy;
			}
		}
	}

	public static bool IsCurrent(CGameState state, CCountry player, CCountry occupied, COccupationPolicy policy)
	{
		return COccupationPolicies.Of(state, player, occupied) == policy;
	}

	/// <summary>A choice's button: pressed where its <c>allow</c> holds, the current policy's included.</summary>
	public static bool MayChoose(CGameState state, CCountry player, CCountry occupied, COccupationPolicy policy)
	{
		return COccupationPolicies.IsAllowed(state, player, occupied, policy);
	}

	/// <summary>A policy's tooltip, on a row's name and its button: its figures, a line each; delayed, its <c>_desc</c>.</summary>
	public static CTooltip Tooltip(Func<string, string> localise, COccupationPolicy policy)
	{
		return new CTooltip { Text = CModifierText.AllValues(localise, policy.Modifier), Delayed = localise(policy.Name + "_desc") };
	}

	/// <summary>A choice's button's tooltip: the <c>allow</c> block's account of itself, asked of the player with the occupied as FROM.</summary>
	public static string ChoiceTooltip(CScriptWords words, CCountry player, CCountry occupied, COccupationPolicy policy)
	{
		return policy.Allow is { } allow ? CConditionText.For(words, allow, CEventScope.Start(player.Tag, fromCountry: occupied.Tag)) : "";
	}
}
