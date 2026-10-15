using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The weather map mode's colour of a land province: its terrain's, greyed and lightened by how frozen the ground is,
/// and dimmed to half where the player knows little of it - what the original's terrain shader makes of the mode's
/// per-province figures. Land no country owns is greyed and dimmed as if six tenths frozen and known. See
/// <c>docs/mechanics/weather.md</c>, section 6.
/// </summary>
public static class CWeatherMapColours
{
	/// <summary>Below this much intelligence a province is drawn at half its brightness.</summary>
	public const int KnownFrom = 2;

	private const float Dimmed = 0.5f;

	/// <summary>What owner-less land is drawn with: 0x99 of 0xFF in every figure.</summary>
	private const float Unowned = 0x99 / 255f;

	/// <summary>How much brighter frozen ground is drawn, at the full of it.</summary>
	private const float WinterLight = 0.3f;

	public static CColor Of(CGameState state, CProvince province, CCountryTag? player)
	{
		CColor ground = province.Terrain?.Color ?? CColor.Gray;
		float brightness;
		float winter;
		if (province.Owner is null)
		{
			brightness = Unowned;
			winter = Unowned;
		}
		else
		{
			brightness = player is { } tag && CProvinceIntel.Level(state, province, tag) < KnownFrom ? Dimmed : 1f;
			winter = Math.Max(0f, province.Weather.Frozen.Raw / (float)CFixedPoint.Scale);
		}
		// The figures are packed into bytes on their way to the shader.
		brightness = (int)(brightness * 255f) / 255f;
		winter = (int)(Math.Min(winter, 1f) * 255f) / 255f;

		float grey = ground.R * 0.212671f + ground.G * 0.715160f + ground.B * 0.072169f;
		float Channel(float value) => Math.Clamp(((value + (grey - value) * winter) + WinterLight * winter) * brightness, 0f, 1f);
		return new CColor(Channel(ground.R), Channel(ground.G), Channel(ground.B));
	}
}
