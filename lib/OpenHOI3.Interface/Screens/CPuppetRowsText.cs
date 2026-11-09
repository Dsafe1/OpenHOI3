using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>What the politics screen's rows for making a puppet and liberating a country say. See <c>docs/mechanics/politics.md</c>.</summary>
public static class CPuppetRowsText
{
	private const int MostNamed = 3;

	/// <summary>The target's cores the player owns and holds: what a row and its dialog count.</summary>
	public static List<CProvince> CoresHeld(CGameState state, CCountry player, CCountry target)
	{
		return state.Provinces.Where(province => province.Cores.Contains(target.Tag) && province.Owner == player.Tag && province.Controller == player.Tag).ToList();
	}

	/// <summary>The land a liberated country gets back: what it owns that the player's faction holds.</summary>
	public static int LiberatedCount(CGameState state, CCountry player, CCountry target)
	{
		return state.Provinces.Count(province => province.Owner == target.Tag && state.Country(province.Controller)?.Faction == player.Faction);
	}

	private static string Number(Func<string, string> localise, int count)
	{
		return CTextTemplate.Fill(localise("VASSALPROVNUM"), ("NUMBER", count.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>A row's line: the provinces by name, or under four, or their number.</summary>
	public static string ProvincesLine(Func<string, string> localise, CGameState state, CCountry player, CCountry target)
	{
		List<CProvince> provinces = CoresHeld(state, player, target);
		if (provinces.Count > MostNamed)
		{
			return Number(localise, provinces.Count);
		}
		return localise("VASSALPROV") + (provinces.Count == 0 ? "" : " " + string.Join(", ", provinces.Select(province => province.Name)));
	}

	public static string PuppetDialogText(Func<string, string> localise, CGameState state, CCountry player, CCountry target)
	{
		return Number(localise, CoresHeld(state, player, target).Count);
	}

	public static string LiberateDialogText(Func<string, string> localise, CGameState state, CCountry player, CCountry target)
	{
		return Number(localise, LiberatedCount(state, player, target));
	}

	public static CTooltip PuppetTooltip(Func<string, string> localise, CGameState state, CCountry player)
	{
		return new CTooltip { Text = localise(CCountryLists.PossibleVassals(state, player).Count > 0 ? "POL_PUPPET_YES" : "POL_PUPPET_NO") };
	}

	/// <summary>The liberate button's tooltip; an exile not to be had is named with who holds its capital.</summary>
	public static CTooltip LiberateTooltip(Func<string, string> localise, CGameState state, CCountry player)
	{
		if (CCountryLists.PossibleLiberations(state, player).Count > 0)
		{
			return new CTooltip { Text = localise("POL_LIB_COUNTRY_YES") };
		}
		if (!COfferRules.RealFaction(player))
		{
			return new CTooltip { Text = localise("POL_LIB_C_NO_FACTION") };
		}
		if (player.InExile)
		{
			return new CTooltip { Text = localise("POL_LIB_C_WE_EXILE") };
		}
		var lines = new List<string>();
		foreach (CCountry exile in state.Countries)
		{
			if (exile != player && exile.InExile && CCountryLists.OwnedCount(state, exile.Tag) > 0 && exile.Capital != 0 && !CCountryLists.IsEnemy(state, exile, player.Tag))
			{
				CProvince capital = state.Province(exile.Capital);
				lines.Add("@" + exile.Tag.Value + " - " + capital.Name + " (@" + (capital.Controller?.Value ?? "") + ")");
			}
		}
		return new CTooltip { Text = lines.Count == 0 ? localise("POL_LIB_C_NO_EXILES") : localise("POL_LIB_C_POSSIBLE") + "\n" + string.Join("\n", lines) };
	}
}
