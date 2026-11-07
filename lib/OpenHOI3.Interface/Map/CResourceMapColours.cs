using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's two colours in the resources map mode: grey, or green by its IC; and a strategic resource striped over it -
/// solid where there is no IC - blue where the player is given it, red where its controller is an enemy, yellow otherwise,
/// none of the three dimmed. See <c>docs/mechanics/mapmodes.md</c>, section 5, and
/// <c>reversing/findings/FINDINGS-mapmodesfigures.md</c>, section 5.
/// </summary>
public static class CResourceMapColours
{
	public static readonly CColor Given = new(0.247059f, 0.423529f, 1f);
	public static readonly CColor Enemy = new(1f, 0.152941f, 0.152941f);
	public static readonly CColor Other = new(1f, 0.847059f, 0f);

	public static (CColor Ground, CColor Stripes) Of(CGameState state, CProvince province, CCountryTag? player)
	{
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		int known = player is { } tag ? CProvinceIntel.Level(state, province, tag) : 0;
		long ic = province.Modifier["ic"].Raw;
		var first = new CColor(0.9f * b, 0.9f * b, 0.9f * b);
		if (ic > 0)
		{
			float v = known < 1 ? 0.5f : Math.Min(ic / 1000f / 10f * 0.5f + 0.5f, 1f);
			first = new CColor(0.3f * b, v * b, 0.3f * b);
		}
		CColor second = first;
		if (province.StrategicResource.Length > 0)
		{
			CCountry? holder = province.Controller is { } held ? state.Country(held) : null;
			CCountry? us = player is { } p ? state.Country(p) : null;
			second = holder is not null && us is not null && CStrategicResources.GetsFrom(state, us, holder) ? Given
				: holder is not null && player is { } enemy && CCountryLists.IsEnemy(state, holder, enemy) ? Enemy
				: Other;
			if (ic <= 0)
			{
				first = second;
			}
		}
		return (first, second);
	}
}
