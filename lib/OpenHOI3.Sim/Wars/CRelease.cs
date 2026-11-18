using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// The original's <c>ReleaseProvinces</c>: a country given land - by <c>release</c>, or by rebels' independence - with
/// its cores, its capital, the oppressor's technology, an army in each province with industry, and the news. See
/// <c>docs/mechanics/rebels.md</c>, section 12, and <c>reversing/findings/FINDINGS-occupation.md</c>, section D.9.
/// </summary>
public static class CRelease
{
	/// <summary>The hours the released country and each former owner are counted as at war before now.</summary>
	public const int SeenHoursAgo = 24;

	/// <summary>The brigades of each army a released province with industry raises, and of what.</summary>
	public const int Brigades = 3;
	public const string Infantry = "infantry_brigade";

	/// <summary>The modifier a province's industry is read from.</summary>
	public const string Industry = "ic";

	public static void Release(CGameState state, IReadOnlyList<CProvince> provinces, CCountryTag released)
	{
		if (provinces.Count == 0 || state.Country(released) is not { } country)
		{
			return;
		}
		CCountryTag? oppressor = provinces[0].Owner;
		var seen = new List<CCountryTag>();
		foreach (CProvince province in provinces)
		{
			if (province.Owner is { } owner && !seen.Contains(owner))
			{
				seen.Add(owner);
				if (state.Country(owner) is { } former)
				{
					CGameState.Status(country, former).LastWar = state.TotalHours - SeenHoursAgo;
					CGameState.Status(former, country).LastWar = state.TotalHours - SeenHoursAgo;
				}
			}
			CProvinceHands.ChangeOwner(state, province, released, consequences: true, peaceful: true);
			if (!province.Cores.Contains(released))
			{
				province.Cores.Add(released);
			}
			// Its original capital declared again, not logged as a change of the country's own.
			if (country.OriginalCapitalOrDeclared == province.Id && country.Capital != province.Id)
			{
				country.Capital = province.Id;
			}
		}
		if (state.Country(oppressor) is { } above)
		{
			RaiseTechnology(state, country, above);
		}
		RaiseArmies(state, country, provinces);
		state.RaisePactMessage(new CPactMessage("INDEPENDANCE", "INDEPENDANCE", "INDEPENDANCE", released, oppressor ?? released)
		{
			Values = new[] { ("OPRESSOR", state.Country(oppressor)?.Name ?? "") },
		});
		CMinisterPosts.Fill(state, country, refillAll: true, electedOnly: false);
	}

	/// <summary>Every technology the oppressor has higher, raised to its level, at most the technology's highest.</summary>
	private static void RaiseTechnology(CGameState state, CCountry country, CCountry oppressor)
	{
		bool changed = false;
		foreach (CTechnology technology in state.Technologies.Values)
		{
			int ours = country.TechnologyLevels.GetValueOrDefault(technology.Name);
			int theirs = oppressor.TechnologyLevels.GetValueOrDefault(technology.Name);
			if (ours < theirs)
			{
				country.TechnologyLevels[technology.Name] = Math.Min(theirs, technology.HighestLevel);
				changed = true;
			}
		}
		if (changed)
		{
			CCountryModifiers.RebuildTechnology(state, country);
		}
	}

	/// <summary>
	/// Where the country may build infantry, an army of three infantry brigades in each released province with
	/// industry, which looks for a battle where it stands.
	/// </summary>
	private static void RaiseArmies(CGameState state, CCountry country, IReadOnlyList<CProvince> provinces)
	{
		if (!state.SubUnitTypes.TryGetValue(Infantry, out CSubUnitType? type) || !CUnitAvailability.MayUse(state, country, type))
		{
			return;
		}
		foreach (CProvince province in provinces)
		{
			if (province.Modifier[Industry] <= CFixedPoint.Zero)
			{
				continue;
			}
			var army = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province.Id };
			for (int made = 0; made < Brigades; made++)
			{
				var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = army };
				brigade.Name = CUnitNames.ForSubUnit(state, country, type);
				CUnitNames.Register(country, brigade.Name);
				Revolts.CRebelArmies.SetFull(state, country, brigade);
				army.AddSubUnit(brigade);
			}
			army.Name = CUnitNames.ForDivision(state, country, type);
			CUnitNames.Register(country, army.Name);
			country.Units.Add(army);
			state.ForgetPositions();
			CCombats.CheckForCombat(state, army, province.Id);
		}
	}
}
