using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's colour in the simplified terrain map mode: its terrain's <c>color</c> from <c>map/terrain.txt</c>, at half
/// brightness where the player knows it below 2, each part held at one. See <c>docs/mechanics/mapmodes.md</c>, section 6,
/// and <c>reversing/findings/FINDINGS-mapmodesrest.md</c>, section 1.
/// </summary>
public static class CSimplifiedTerrainColours
{
	/// <summary>The colour a terrain with no <c>color</c> keeps: the original's white.</summary>
	private static readonly CColor White = new(1f, 1f, 1f);

	public static CColor Of(CGameState state, CProvince province, CCountryTag? player)
	{
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		CColor colour = province.Terrain?.Color ?? White;
		return Scaled(colour, b);
	}

	/// <summary>A colour times a brightness, each of red, green and blue held at one, its alpha kept.</summary>
	public static CColor Scaled(CColor colour, float b) =>
		new(Math.Min(colour.R * b, 1f), Math.Min(colour.G * b, 1f), Math.Min(colour.B * b, 1f), colour.A);
}
