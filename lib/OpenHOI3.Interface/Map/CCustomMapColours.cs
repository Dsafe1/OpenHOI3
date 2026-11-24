using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's colour under a custom map source: one of ten shades of a palette by its value, or a grey. See
/// <c>docs/mechanics/custommapmodes.md</c>, section 5.
/// </summary>
public static class CCustomMapColours
{
	/// <summary>The two palettes, by the names a setting keeps them under.</summary>
	public const string Green = "green";
	public const string Heat = "heat";

	public static readonly string[] Palettes = { Green, Heat };

	/// <summary>What the player has to know of a province to see how much it has: where the province view starts showing buildings.</summary>
	public const int Seen = 6;

	public static readonly CColor Light = Rgb(200, 200, 200);
	public static readonly CColor Dark = Rgb(120, 120, 120);

	/// <summary>The infrastructure map mode's ten colours, with its step back in brightness between the sixth and seventh.</summary>
	private static readonly CColor[] HeatRamp =
	{
		Rgb(25, 0, 0), Rgb(153, 0, 0), Rgb(204, 25, 0), Rgb(255, 76, 0), Rgb(255, 255, 0),
		Rgb(165, 191, 0), Rgb(25, 102, 0), Rgb(51, 127, 0), Rgb(63, 186, 0), Rgb(0, 255, 0),
	};

	/// <summary>The palette of a name, the green one for any name that is none.</summary>
	public static string Palette(string name) => Heat.Equals(name, StringComparison.OrdinalIgnoreCase) ? Heat : Green;

	/// <summary>A shade's colour, 1 to 10, in a palette. The green ramp is worked out in whole numbers.</summary>
	public static CColor Shade(string palette, int shade)
	{
		shade = Math.Clamp(shade, 1, CCustomMapScale.Shades);
		if (Palette(palette) == Heat)
		{
			return HeatRamp[shade - 1];
		}
		int other = 24 + 36 * shade / 10;
		return Rgb(other, 90 + 165 * shade / 10, other);
	}

	/// <summary>
	/// A province's colour: for a source that hides what is not seen, dark grey unseen; grey where it has nothing to
	/// show, light where the player sees into it and dark where not; else its shade, and the lowest where it is not
	/// seen - that there is some, and not how much.
	/// </summary>
	public static CColor Of(CGameState state, CProvince province, CCountryTag? player, CCustomMapSource source, CCustomMapScale scale,
		string palette)
	{
		bool seen = player is { } tag && CProvinceIntel.Level(state, province, tag) >= Seen;
		if (source.HiddenUnseen && !seen)
		{
			return Dark;
		}
		if (source.Value(state, province) is not { } value)
		{
			return seen ? Light : Dark;
		}
		return Shade(palette, seen ? scale.Shade(source, value) : 1);
	}

	private static CColor Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
}
