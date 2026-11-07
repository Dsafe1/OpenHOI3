using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's colour in the victory point map mode: grey, and where it has victory points one of three fixed hues by how
/// its controller stands towards the player - green its own or its faction's, red an enemy's or the rebels', yellow anyone
/// else's - brighter from 15 points; all at half brightness where the player knows it below 2. See
/// <c>docs/mechanics/mapmodes.md</c>, section 3, and <c>reversing/findings/FINDINGS-mapmodesfigures.md</c>, section 3.
/// </summary>
public static class CVictoryPointMapColours
{
	public static CColor Of(CGameState state, CProvince province, CCountryTag? player)
	{
		float b = Brightness(state, province, player);
		if (province.Points <= 0)
		{
			return new CColor(0.9f * b, 0.9f * b, 0.9f * b);
		}
		// An integer divide: 0.7 for 1 to 14 points, 1 from 15.
		float k = Math.Min(province.Points / 15 * 0.3f + 0.7f, 1f);
		return Hue(state, province.Controller, player) switch
		{
			EHue.Green => new CColor(0.3f * b, k * b, 0.3f * b),
			EHue.Red => new CColor(k * b, 0.3f * b, 0.3f * b),
			_ => new CColor(k * b, k * b, 0.3f * b),
		};
	}

	/// <summary>One where the player knows the province at 2 or more, a half below.</summary>
	public static float Brightness(CGameState state, CProvince province, CCountryTag? player) =>
		player is { } tag && CProvinceIntel.Level(state, province, tag) >= 2 ? 1f : 0.5f;

	private enum EHue
	{
		Green,
		Red,
		Yellow,
	}

	/// <summary>The player's own green; at war with it or the rebels' red; its real faction's green; anyone else yellow.</summary>
	private static EHue Hue(CGameState state, CCountryTag? controller, CCountryTag? player)
	{
		if (controller is not { } held || player is not { } us)
		{
			return EHue.Yellow;
		}
		if (held == us)
		{
			return EHue.Green;
		}
		if (CCountryLists.AtWar(state, held, us) || held.Value == "REB" || us.Value == "REB")
		{
			return EHue.Red;
		}
		CCountry? theirs = state.Country(held);
		CCountry? ours = state.Country(us);
		return theirs is { Faction.Length: > 0 } && theirs.Faction == ours?.Faction ? EHue.Green : EHue.Yellow;
	}
}
