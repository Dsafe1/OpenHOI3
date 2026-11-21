using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// What the AI does with a country's undergrounds, every hour for every country nobody plays: each area's are spread
/// while they are few for its size, built up while too few are ready, and raised as partisans otherwise, the share
/// weighed by how strongly the occupier's side holds the area's fronts. The original's <c>AI_ManageUndergrounds</c>.
/// See <c>docs/mechanics/undergrounds.md</c>, section 10.
/// </summary>
public static class CUndergroundAi
{
	/// <summary>A score's ceiling, and what a score is taken from.</summary>
	private const long Whole = 1000;

	/// <summary>The occupier's side passes a front over below this, in thousandths of the other's power.</summary>
	private const long Outweighs = 1100;

	/// <summary>A front's score is a tenth of the difference: times this, over a thousand.</summary>
	private const long ScoreScale = 100;

	/// <summary>A front in an area the country holds counts half again: times this, over a thousand.</summary>
	private const long OwnArea = 1500;

	/// <summary>The share of an area's provinces the undergrounds spread to, in thousandths: from this ...</summary>
	private const long SpreadBase = 20;

	/// <summary>... to twice it, by how weak the occupier's hold is.</summary>
	private const long SpreadRange = 20;

	/// <summary>An area counts at most this many provinces for the share.</summary>
	private const int AreaCap = 200;

	/// <summary>The fixed point the share is compared in, fifteen binary places.</summary>
	private const int Shift = 15;

	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			if (country.Tag.Value == CRebelFactions.Rebels || state.Played.Contains(country.Tag))
			{
				continue;
			}
			Run(state, country);
		}
	}

	/// <summary>One country's undergrounds, area by area, each posting an action only where it changes.</summary>
	public static void Run(CGameState state, CCountry country)
	{
		List<CProvince> undergrounds = CUndergrounds.Of(state, country.Tag);
		if (undergrounds.Count == 0)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		// Ours: the original keys its map by the area's address, an order nothing in the game holds; the areas go here in
		// the order their first underground comes in, which changes nothing but the order the commands are posted in.
		var areas = new List<CSupplyArea>();
		var byArea = new Dictionary<CSupplyArea, List<CProvince>>();
		foreach (CProvince province in undergrounds)
		{
			if (network.AreaOf(province.Id) is not { } area)
			{
				continue;
			}
			if (!byArea.TryGetValue(area, out List<CProvince>? list))
			{
				list = new List<CProvince>();
				byArea[area] = list;
				areas.Add(area);
			}
			list.Add(province);
		}
		foreach (CSupplyArea area in areas)
		{
			if (area.Provinces.Count > 0)
			{
				Decide(state, country, area, byArea[area]);
			}
		}
	}

	/// <summary>Spread, build up or raise partisans, for the undergrounds of one area.</summary>
	private static void Decide(CGameState state, CCountry country, CSupplyArea area, List<CProvince> undergrounds)
	{
		long ready = 0;
		foreach (CProvince province in undergrounds)
		{
			if (CUndergrounds.MayRaisePartisans(state, country.Tag, province))
			{
				ready++;
			}
		}
		Set(state, undergrounds, Action(undergrounds.Count, area.Provinces.Count, ready, Score(state, country, area)));
	}

	/// <summary>
	/// The action for an area's <paramref name="count"/> undergrounds, <paramref name="ready"/> of them able to raise
	/// partisans, in an area of <paramref name="areaProvinces"/> provinces, by the area's score: spread while they are fewer
	/// than 2% to 4% of the area, then build up while too few are ready, and raise partisans.
	/// </summary>
	public static int Action(long count, int areaProvinces, long ready, long score)
	{
		long t = Whole - score;
		long x = SpreadBase + SpreadRange * t / Whole;
		long have = (count << Shift) / Math.Min(areaProvinces, AreaCap);
		long want = (x << Shift) / Whole;
		if (have < want)
		{
			return CUndergrounds.Spread;
		}
		return ready < count * t / Whole ? CUndergrounds.BuildUp : CUndergrounds.RaisePartisans;
	}

	private static void Set(CGameState state, List<CProvince> undergrounds, int action)
	{
		foreach (CProvince province in undergrounds)
		{
			if (province.UndergroundAction != action)
			{
				state.Outbox.Add(new CChangeUndergroundMissionCommand(province.Id, action));
			}
		}
	}

	/// <summary>
	/// How strongly the occupier's side holds the area's land fronts, 0 to 1000: on each front, the occupier of its first
	/// occupied province against the owner across the line, each side's units by their power, a front counting only where
	/// the occupier's side outweighs the other by a tenth.
	/// </summary>
	public static long Score(CGameState state, CCountry country, CSupplyArea area)
	{
		long score = 0;
		if (!CAreaHostility.TouchesHostileArea(state, area))
		{
			return score;
		}
		foreach (List<int> front in CAreaFronts.Of(state, area))
		{
			CProvince? occupied = null;
			foreach (int id in front)
			{
				CProvince province = state.Province(id);
				if (province.Controller != province.Owner)
				{
					occupied = province;
					break;
				}
			}
			if (occupied?.Controller is not { } c || state.Country(c) is null)
			{
				continue;
			}
			CCountryTag? l = null;
			foreach (CProvinceEdge edge in occupied.Edges)
			{
				CProvince next = state.Province(edge.To);
				if (next.Owner is { } owner && next.Controller == owner && state.Country(owner) is { } holder
					&& CCountryLists.IsEnemy(state, holder, c))
				{
					l = owner;
					break;
				}
			}
			// The original's: a front with nobody across the line ends the walk of every front after it.
			if (l is not { } lTag)
			{
				break;
			}
			if (CCountryLists.IsEnemy(state, state.Country(lTag)!, country.Tag))
			{
				continue;
			}
			long sumC = 0;
			long sumL = 0;
			foreach (int id in front)
			{
				foreach (CUnit unit in state.UnitsIn(id))
				{
					// A lent unit counts for its lender, as the original's.
					if (state.Country(unit.FiguresOwner) is not { } side)
					{
						continue;
					}
					if (CCountryLists.IsFriend(state, side, c, true))
					{
						sumC += CUnitPower.Of(unit);
					}
					else if (CCountryLists.IsFriend(state, side, lTag, true))
					{
						sumL += CUnitPower.Of(unit);
					}
				}
			}
			if (sumL > 0 && sumC * Whole / sumL < Outweighs)
			{
				continue;
			}
			long s = (sumC - sumL) * ScoreScale / Whole;
			if (area.Controller == country.Tag)
			{
				s = s * OwnArea / Whole;
			}
			score += s;
		}
		return Math.Min(score, Whole);
	}
}
