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

	/// <summary>The original's map style for the mode, which draws the water plain: the region mode's too.</summary>
	public const int PlainStyle = 8;

	/// <summary>The air map mode's style, whose water is drawn from the provinces' colours.</summary>
	public const int ColouredSeaStyle = 18;

	/// <summary>
	/// Whether the mode colours the sea. The original works out a colour for every sea province and then draws the water
	/// plain, the mode's style choosing the water that does not read them - a fault kept, and mended in an enhanced game
	/// by the fix <see cref="OpenHOI3.Sim.Rules.EFix.SimplifiedTerrainSea"/> (<c>docs/mechanics/mapmodes.md</c>, section 6).
	/// </summary>
	public static bool ColoursSea(CGameState state) => state.Rules.Has(OpenHOI3.Sim.Rules.EFix.SimplifiedTerrainSea);

	/// <summary>The map style the mode sets: the air mode's where it colours the sea, as the fix is made, and its own otherwise.</summary>
	public static int Style(CGameState state) => ColoursSea(state) ? ColouredSeaStyle : PlainStyle;

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
