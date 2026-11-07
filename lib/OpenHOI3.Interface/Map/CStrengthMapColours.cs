using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A land province's colour in the strength map mode: grey, and where the player knows it at 2 or more and land units stand
/// in it - anybody's - a hue by its controller - green a friend's, red an enemy's, blue anyone else's - brighter the more
/// they weigh, up to 500. What a unit weighs is what the player knows of it: at 2 a count, at 3 to 5 a figure fixed by its
/// number, at 6 and 7 its strength and organisation, from 8 its experience as well. See
/// <c>docs/mechanics/mapmodes.md</c>, section 4, and <c>reversing/findings/FINDINGS-mapmodesfigures.md</c>, section 4.
/// </summary>
public static class CStrengthMapColours
{
	/// <summary>The weight at which a province is at its brightest [exe].</summary>
	private const float Brightest = 500f;

	/// <summary>The land units standing in each province, made once for a colouring.</summary>
	public static Dictionary<int, List<CUnit>> LandUnits(CGameState state)
	{
		var units = new Dictionary<int, List<CUnit>>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsRemoved || unit.Kind is EUnitKind.Navy or EUnitKind.Air || unit.Location <= 0)
				{
					continue;
				}
				if (!units.TryGetValue(unit.Location, out List<CUnit>? here))
				{
					units[unit.Location] = here = new List<CUnit>();
				}
				here.Add(unit);
			}
		}
		return units;
	}

	public static CColor Of(CGameState state, CProvince province, CCountryTag? player, Dictionary<int, List<CUnit>> units)
	{
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		var grey = new CColor(0.5f * b, 0.5f * b, 0.5f * b);
		if (player is not { } us)
		{
			return grey;
		}
		int known = CProvinceIntel.Level(state, province, us);
		if (known < 2 || !units.TryGetValue(province.Id, out List<CUnit>? here))
		{
			return grey;
		}
		float total = 0f;
		foreach (CUnit unit in here)
		{
			total += Weight(unit, known);
		}
		if (total <= 0f)
		{
			return grey;
		}
		float k = Math.Min(total, Brightest) / Brightest * 0.5f + 0.5f;
		CCountry? holder = province.Controller is { } held ? state.Country(held) : null;
		if (holder is not null && CCountryLists.IsFriend(state, holder, us, withAccess: false))
		{
			return new CColor(0.3f * b, k * b, 0.3f * b);
		}
		if (holder is not null && CCountryLists.IsEnemy(state, holder, us))
		{
			return new CColor(k * b, 0.3f * b, 0.3f * b);
		}
		return new CColor(0.3f * b, 0.3f * b, k * b);
	}

	/// <summary>What a land unit weighs by what is known of the province it stands in.</summary>
	public static float Weight(CUnit unit, int known)
	{
		if (known <= 2)
		{
			return 1f;
		}
		float strength = CUnitTotals.Strength(unit).Raw / 1000f * 0.01f;
		if (known <= 5)
		{
			return strength * (unit.Id % 37 + 15);
		}
		float organisation = CUnitTotals.AverageOrganisation(unit).Raw / 1000f;
		if (known <= 7)
		{
			return strength * organisation;
		}
		return strength * organisation * (CUnitTotals.AverageExperience(unit).Raw / 1000f + 1f);
	}
}
