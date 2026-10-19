using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// How near a country is to surrendering: the share of its cores' victory points occupied, against its
/// national unity. See <c>docs/mechanics/war.md</c>, section 12.
/// </summary>
public static class CSurrender
{
	/// <summary>The progress at which a country surrenders, and the most it reaches.</summary>
	public const long Brink = 1000;

	/// <summary>
	/// The day's count, before any country's day: over each country's cores, the victory points of those it
	/// owns and of those it owns and holds - a core it does not own counts in neither.
	/// </summary>
	public static void CountVictoryPoints(CGameState state)
	{
		var owned = new Dictionary<CCountry, long>();
		var lost = new Dictionary<CCountry, long>();
		foreach (CCountry country in state.Countries)
		{
			country.OwnedCoreVictoryPoints = 0;
			country.HeldCoreVictoryPoints = 0;
			country.OwnedVictoryPoints = 0;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } anyOwner && state.Country(anyOwner) is { } holder)
			{
				holder.OwnedVictoryPoints += province.Points * 1000L;
			}
			if (province.Owner is not { } owner || !province.Cores.Contains(owner) || state.Country(owner) is not { } country)
			{
				continue;
			}
			long points = province.Points * 1000L;
			country.OwnedCoreVictoryPoints += points;
			owned[country] = owned.GetValueOrDefault(country) + 1000;
			if (province.Controller == owner)
			{
				country.HeldCoreVictoryPoints += points;
			}
			else
			{
				lost[country] = lost.GetValueOrDefault(country) + 1000;
			}
		}
		// And what lost_national asks: the share of its owned cores a country does not hold, in thousandths; nought with
		// none owned (triggers.md, section 5).
		foreach (CCountry country in state.Countries)
		{
			long total = owned.GetValueOrDefault(country);
			country.LostNationalProvinces = new CFixedPoint(total > 0 ? lost.GetValueOrDefault(country) * 1000 / total : 0);
		}
		CountCoreBuildingLevels(state);
	}

	/// <summary>
	/// Made in the same pass in the original: each building's levels built in the provinces a country owns and holds a
	/// core in, and a whole level for every line of it in the queue - a line with no province always, a placed one only
	/// on a core - whatever the line's size (<c>ministers.md</c>, section 7).
	/// </summary>
	public static void CountCoreBuildingLevels(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.CoreBuildingLevels.Clear();
			for (int index = 0; index < state.Buildings.Count; index++)
			{
				country.CoreBuildingLevels.Add(CFixedPoint.Zero);
			}
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is not { } owner || !province.Cores.Contains(owner) || state.Country(owner) is not { } country)
			{
				continue;
			}
			for (int index = 0; index < state.Buildings.Count; index++)
			{
				country.CoreBuildingLevels[index] += province.BuildingMaxLevel(state.Buildings[index]);
			}
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (Construction.CConstruction line in country.Constructions)
			{
				if (line.Building is not { } building
					|| line.Location > 0 && !state.Province(line.Location).Cores.Contains(country.Tag))
				{
					continue;
				}
				int index = IndexOf(state, building);
				if (index >= 0)
				{
					country.CoreBuildingLevels[index] += CFixedPoint.One;
				}
			}
		}
	}

	private static int IndexOf(CGameState state, CBuilding building)
	{
		for (int index = 0; index < state.Buildings.Count; index++)
		{
			if (ReferenceEquals(state.Buildings[index], building))
			{
				return index;
			}
		}
		return -1;
	}

	/// <summary>
	/// The original's <c>GetSurrenderProgress</c>, 0 to 1000: 1000 for a country holding no province; else the
	/// occupied share of its owned cores' victory points, in thousandths, over its national unity in tenths of a
	/// per cent, held to 1000. A government in exile answers 0.
	/// </summary>
	public static long Progress(CGameState state, CCountry country)
	{
		if (country.InExile)
		{
			return 0;
		}
		if (Controlled(state, country) == 0)
		{
			return Brink;
		}
		long unity = country.NationalUnity.Raw * 1000 / 100000;
		if (unity == 0)
		{
			return Brink;
		}
		return Math.Clamp(Occupied(country) * 1000 / unity, 0, Brink);
	}

	/// <summary>The days a country must wait after surrendering before it surrenders again.</summary>
	public const int DaysBetween = 30;

	/// <summary>
	/// In each country's day, late: a country owning a province, at the brink, at war, and thirty days or more past
	/// its last surrender surrenders to its best-scoring enemy - <c>on_surrender</c> fired first, THIS it and FROM
	/// the victor, then the capitulation - and its last surrender is now. So one that stays at the brink surrenders
	/// again every thirty days.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (CCountryLists.OwnedCount(state, country.Tag) == 0 || Progress(state, country) < Brink || !state.IsAtWar(country.Tag)
				|| state.TotalHours - country.LastSurrender < DaysBetween * CGameState.HoursPerDay)
			{
				continue;
			}
			if (Victor(state, country) is not { } victor)
			{
				continue;
			}
			var scope = Script.CEventScope.Start(country.Tag, 0, fromCountry: victor.Tag, randomState: (uint)state.Random.Next());
			Events.COnActions.Fire(state, "on_surrender", scope);
			CCapitulation.Capitulate(state, country, victor);
			country.LastSurrender = state.TotalHours;
		}
	}

	/// <summary>
	/// The enemy a country surrenders to: the one scoring highest, the first of equals - 1000, or 2000 for the leader
	/// of a real faction; 5000 more where it holds the country's capital; and for each province of the country's it
	/// holds 1000, 3000 a victory point and a third of the province's industry.
	/// </summary>
	public static CCountry? Victor(CGameState state, CCountry country)
	{
		CCountry? best = null;
		long bestScore = long.MinValue;
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			long score = CWarSides.LeadsRealFaction(state, enemy) ? 2000 : 1000;
			if (state.Province(country.Capital).Controller == enemy.Tag && country.Capital != 0)
			{
				score += 5000;
			}
			foreach (CProvince province in state.Provinces)
			{
				if (province.Owner == country.Tag && province.Controller == enemy.Tag)
				{
					score += 1000 + province.Points * 3000L + province.Modifier[Economy.CIndustrialCapacity.ProvinceIc].Raw / 3;
				}
			}
			if (best is null || score > bestScore)
			{
				best = enemy;
				bestScore = score;
			}
		}
		return best;
	}

	/// <summary>The occupied share of its owned cores' victory points, in thousandths; 0 where it owns none.</summary>
	public static long Occupied(CCountry country)
	{
		long owned = country.OwnedCoreVictoryPoints;
		return owned <= 0 ? 0 : 1000 - Math.Clamp(country.HeldCoreVictoryPoints * 1000 / owned, 0, 1000);
	}

	private static int Controlled(CGameState state, CCountry country)
	{
		int count = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == country.Tag)
			{
				count++;
			}
		}
		return count;
	}
}
