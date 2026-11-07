using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's colour in the intel map mode, by what the player knows of it: dark red at nothing, olive at 1, and from 2 a
/// green brighter with each level, to full at 8; land and sea alike, no stripes. See <c>docs/mechanics/mapmodes.md</c>,
/// section 9, and <c>reversing/findings/FINDINGS-mapmodescountries.md</c>, section 3.
/// </summary>
public static class CIntelMapColours
{
	public static CColor Of(CGameState state, CProvince province, CCountryTag? player)
	{
		int known = player is { } tag ? CProvinceIntel.Level(state, province, tag) : 0;
		return OfLevel(known);
	}

	public static CColor OfLevel(int known)
	{
		if (known <= 0)
		{
			return new CColor(0.3f, 0f, 0f);
		}
		if (known == 1)
		{
			return new CColor(0.2f, 0.3f, 0f);
		}
		// Each step in double, rounded to a float, the 0.2 a float widened.
		float green = (float)Math.Min((float)(known / 9.0) + (double)0.2f, 1.0);
		return new CColor(0.2f, green, 0.2f);
	}
}
