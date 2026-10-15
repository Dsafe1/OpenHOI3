using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The province view's <c>underground</c> window: which province it is about, its bar, its suppression, its mission and
/// its two buttons. See <c>docs/mechanics/provinceview.md</c>, section 5.
/// </summary>
public static class CProvinceUndergroundText
{
	private const string Section = "military";

	/// <summary>
	/// The province the window is about: with fewer than two selected the view's, with more the first selected - where it
	/// has an underground that is the player's or one the player could place; otherwise none, and the window is hidden.
	/// </summary>
	public static CProvince? About(CGameState state, CCountry player, CProvince shown, IReadOnlyList<CProvince> selected)
	{
		if (selected.Count < 2)
		{
			return Shows(state, player, shown) ? shown : null;
		}
		foreach (CProvince province in selected)
		{
			if (Shows(state, player, province))
			{
				return province;
			}
		}
		return null;
	}

	private static bool Shows(CGameState state, CCountry player, CProvince province)
	{
		return CUndergrounds.Has(state, province)
			&& (province.UndergroundOwner == player.Tag || CUndergrounds.CanPlaceIn(state, province, player, requireEmpty: false));
	}

	/// <summary><c>underground_status</c>'s bar: the strength in whole percent of one, rounded half to even.</summary>
	public static int Status(CGameState state, CProvince province)
	{
		return (int)Math.Round(CUndergrounds.Strength(state, province).Raw / 10.0, MidpointRounding.ToEven);
	}

	/// <summary><c>text_supression</c>: the detection define times the province's suppression, whole, and a percent sign.</summary>
	public static string Suppression(CGameState state, CProvince province)
	{
		double shown = state.Defines[Section, "UNDERGROUND_DETECT_CHANCE"].Raw / 1000.0 * (province.Suppression.Raw / 1000.0);
		return "§Y" + Math.Round(shown, MidpointRounding.AwayFromZero).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%§W";
	}

	/// <summary><c>underground_mission</c>'s frame: the action and one, or 3 with no underground.</summary>
	public static int MissionFrame(CGameState state, CProvince province)
	{
		return (CUndergrounds.Has(state, province) ? province.UndergroundAction : CUndergrounds.BuildUp) + 1;
	}

	/// <summary>The frame a step of the mission's button leaves - a left click forward, 1 2 3 1, a right click back.</summary>
	public static int Step(int frame, bool forward)
	{
		return forward ? frame % 3 + 1 : (frame + 1) % 3 + 1;
	}

	/// <summary>
	/// Whether a spawn button may be pressed: with several selected, where any selected province with the player's
	/// underground may; otherwise where the window's province may.
	/// </summary>
	public static bool MaySpawn(CGameState state, CCountry player, CProvince about, IReadOnlyList<CProvince> selected,
		bool partisans)
	{
		bool May(CProvince province) => partisans
			? CUndergrounds.MayRaisePartisans(state, player.Tag, province)
			: CUndergrounds.MaySpread(state, player.Tag, province);
		if (selected.Count >= 2)
		{
			foreach (CProvince province in selected)
			{
				if (province.UndergroundOwner == player.Tag && May(province))
				{
					return true;
				}
			}
			return false;
		}
		return May(about);
	}

	/// <summary>A strength or a limit as the tooltips write it: to one place.</summary>
	public static string Figure(CFixedPoint value) => CNumberText.Format(value, 1);
}
