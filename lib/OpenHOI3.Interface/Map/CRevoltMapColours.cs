using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A land province's colour in the revolt map mode, by the revolt risk shown, at a brightness by what the player knows of
/// it. See <c>docs/mechanics/revolt.md</c>, section 7:
///
///     grey     the player neither holds it nor knows it at 3 or more
///     green    no revolt risk, at full brightness whatever is known
///     yellow   above nought
///     orange   from 2 points
///     red      above 4 points, brighter the higher - and wherever there is an underground
/// </summary>
public static class CRevoltMapColours
{
	public static CColor Of(CGameState state, CProvince province, CCountryTag? player)
	{
		int known = player is { } tag ? CProvinceIntel.Level(state, province, tag) : 0;
		float b = known >= 2 ? 1f : 0.5f;
		if (province.Controller != player && known < 3)
		{
			return new CColor(0.9f * b, 0.9f * b, 0.9f * b);
		}
		// An underground paints it red over whatever its risk would (undergrounds.md, section 8).
		if (CUndergrounds.Has(state, province))
		{
			return new CColor(b, 0.3f * b, 0.3f * b);
		}
		long shown = CRevoltRisk.Shown(state, province);
		if (shown > 4000)
		{
			float f = Math.Min(1f, 0.7f + 0.02f * (shown / 1000f));
			return new CColor(f * b, 0.3f * b, 0.3f * b);
		}
		if (shown >= 2000)
		{
			return new CColor(0.9f * b, 0.6f * b, 0.3f * b);
		}
		if (shown > 0)
		{
			return new CColor(0.9f * b, 0.9f * b, 0.3f * b);
		}
		return new CColor(0.3f, 0.9f, 0.3f);
	}
}
