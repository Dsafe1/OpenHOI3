using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What the strategy's lists are made of and share: its arithmetic in thousandths, a country's strength, its border share,
/// the two tests of its capital and its reach. See <c>docs/mechanics/aistrategy.md</c>, sections 6 and 7.
/// </summary>
public static class CStrategyFigures
{
	/// <summary>Where a list is scaled to, and what every scaled list tops at.</summary>
	public const int Top = 400;

	/// <summary>Thousandths made whole as the original does it: a single-precision number, rounded half to even.</summary>
	public static int Rounded(long thousandths)
	{
		return (int)MathF.Round((float)(thousandths / 1000.0), MidpointRounding.ToEven);
	}

	/// <summary>
	/// A single-precision number made thousandths as the original does: a half-thousandth added, times a thousand, each
	/// step single precision again, and floored.
	/// </summary>
	public static long Thousandths(float value)
	{
		float lifted = (float)(value + 0.0005);
		return (long)MathF.Floor((float)(lifted * 1000.0));
	}

	/// <summary>The original's fixed-point division: thousandths of one over the other, truncated, and -1 over nought.</summary>
	public static long Ratio(long over, long under)
	{
		return under == 0 ? -1 : over * 1000 / under;
	}

	/// <summary>The original's fixed-point product: truncated to thousandths.</summary>
	public static long Times(long one, long other)
	{
		return one * other / 1000;
	}

	/// <summary>
	/// The brigades of the country's land units, every unit at every level counting its own - the original's
	/// <c>GetTotalNumOfRegiments</c> over the country's unit list.
	/// </summary>
	public static int Regiments(CCountry country)
	{
		int regiments = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			if (CMovementRules.IsLandUnit(unit))
			{
				regiments += unit.SubUnits.Count;
			}
		}
		return regiments;
	}

	/// <summary>Its brigades plus one, in thousandths: the strength the antagonism and friendship lists weigh.</summary>
	public static long Strength(CCountry country)
	{
		return (Regiments(country) + 1L) * 1000;
	}

	/// <summary>
	/// The share of the country's border that is each other country's, thousandths: of the edges out of the provinces it
	/// owns into provinces somebody else both owns and holds, those into each country's. Made from the map as it stands.
	/// </summary>
	public static Dictionary<CCountryTag, long> BorderShares(CGameState state, CCountry country)
	{
		var shares = new Dictionary<CCountryTag, long>();
		long total = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != country.Tag)
			{
				continue;
			}
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.Owner is { } other && beside.Controller == other && other != country.Tag)
				{
					shares[other] = shares.GetValueOrDefault(other) + 1000;
					total += 1000;
				}
			}
		}
		if (total > 0)
		{
			foreach (CCountryTag other in new List<CCountryTag>(shares.Keys))
			{
				shares[other] = Ratio(shares[other], total);
			}
		}
		return shares;
	}

	/// <summary>
	/// Whether the area of the supply network the country's acting capital is in touches an area the other holds; false
	/// where the capital is in none.
	/// </summary>
	public static bool CapitalAreaTouches(CGameState state, CCountry country, CCountryTag other)
	{
		if (CSupplyNetworks.Refresh(state).AreaOf(country.ActingCapital) is not { } area)
		{
			return false;
		}
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Controller == other)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether the country's own capital - not the acting one - lies beside a province the other owns, over anything but an impassable border.</summary>
	public static bool CapitalBorders(CGameState state, CCountry country, CCountryTag other)
	{
		if (country.Capital <= 0 || country.Capital >= state.Provinces.Count)
		{
			return false;
		}
		foreach (CProvinceEdge edge in state.Province(country.Capital).Edges)
		{
			if (edge.Kind != EProvinceEdgeKind.Impassable && state.Province(edge.To).Owner == other)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>How far the country reaches, thousandths of the map's kilometres: 28 for each province it owns, at most 1200.</summary>
	public static long Reach(int owned)
	{
		return Math.Min(owned * 28L, 1200L) * 1000;
	}

	/// <summary>The original's <c>GetDiplomaticDistance</c>: between the two acting capitals as the map measures it, thousandths.</summary>
	public static long Distance(CGameState state, CCountry one, CCountry other)
	{
		return CMapDistance.Between(state.Province(one.ActingCapital), state.Province(other.ActingCapital), state.MapWidth) * 1000;
	}

	/// <summary>
	/// A country a list is made over: not the rebels, not itself, owning a province. (The original also passes over the id
	/// nought, which no country has: its ids are a country's place plus one.)
	/// </summary>
	public static bool IsCandidate(CGameState state, CCountry country, CCountry other)
	{
		return other.Tag.Value != "REB" && other != country && COwnedCounts.Of(state).Count(other.Tag) > 0;
	}

	/// <summary>Whether the two are in one faction - one real faction, as no-faction is no faction.</summary>
	public static bool OneFaction(CCountry one, CCountry other)
	{
		return one.Faction.Length > 0 && one.Faction == other.Faction;
	}

	/// <summary>The corner of the triangle whose faction it is; none for no faction.</summary>
	public static Diplomacy.CIdeologyGroup? GroupOfFaction(CGameState state, string faction)
	{
		if (faction.Length == 0)
		{
			return null;
		}
		foreach (Diplomacy.CIdeologyGroup group in state.IdeologyGroups)
		{
			if (group.Faction == faction)
			{
				return group;
			}
		}
		return null;
	}

	/// <summary>The group of the country's ruling party's ideology.</summary>
	public static Diplomacy.CIdeologyGroup? RulingGroup(CGameState state, CCountry country)
	{
		return state.Ideology(country.RulingIdeology)?.Group;
	}

	/// <summary>
	/// Whether the other is one of the country's historical friends. No file of the base game or BlackICE gives a country
	/// any, and the history's <c>historical_friends</c> is not read, so nobody is (aistrategy.md, section 9.2).
	/// </summary>
	public static bool IsHistoricalFriend(CCountry country, CCountryTag other)
	{
		return false;
	}

	/// <summary>Whether any province of the strategy's <c>conquer_prov</c> is owned by the other.</summary>
	public static bool OwnsAConquest(CGameState state, CAiStrategy strategy, CCountryTag other)
	{
		foreach ((int province, int _) in strategy.ConquerProvinces.Entries)
		{
			if (state.Province(province).Owner == other)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A country-keyed add, as the original's: a country never lists itself.</summary>
	public static void Add(CCountry country, CStrategyList<CCountryTag> list, CCountryTag other, int weight, int cap)
	{
		if (other != country.Tag)
		{
			list.Add(other, weight, cap);
		}
	}

	/// <summary>The cap of <c>befriend</c>: 7, or 14 for a diplomacy personality.</summary>
	public static int FriendCap(CAiStrategy strategy) => strategy.Personality == EAiPersonality.Diplomacy ? 14 : 7;

	/// <summary>The cap of <c>protect</c>: 6, or 12 for a diplomacy personality - not befriend's.</summary>
	public static int ProtectCap(CAiStrategy strategy) => strategy.Personality == EAiPersonality.Diplomacy ? 12 : 6;

	public const int AntagonizeCap = 7;
	public const int RivalCap = 4;
	public const int VassalCap = 10;
}
