using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What radar gives a side fighting in the air or bombing: the highest level of radar
/// station in the province fought over and the provinces beside it, of those a friend of
/// the side holds, times <c>RADAR_COMBAT_IMPACT</c> and bettered by the country's own
/// <c>radar_impact</c>. See <c>docs/mechanics/bombing.md</c>, section 9.
/// </summary>
public static class CRadarCover
{
	public const string RadarLevel = "radar_level";
	public const string CombatImpact = "RADAR_COMBAT_IMPACT";
	public const string RadarImpact = "radar_impact";

	/// <summary>The highest radar level a side has over a province, in thousandths; nothing for a side nobody is on.</summary>
	public static long Level(CGameState state, CProvince province, CCombatSide side)
	{
		if (side.Countries.Count == 0)
		{
			return 0;
		}
		CCountryTag first = side.Countries[0];
		long level = IsFriendly(state, province, first) ? province.Modifier[RadarLevel].Raw : 0;
		foreach (CProvinceEdge edge in province.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (IsFriendly(state, beside, first))
			{
				level = System.Math.Max(level, beside.Modifier[RadarLevel].Raw);
			}
		}
		return level;
	}

	/// <summary>What a level of radar is worth to a country's unit, on its attack and its defence alike.</summary>
	public static long Bonus(CGameState state, CCountry? owner, long level)
	{
		if (level == 0 || !state.Defines.Has(CManpower.MilitarySection, CombatImpact))
		{
			return 0;
		}
		long bonus = state.Defines[CManpower.MilitarySection, CombatImpact].Raw * level / CFixedPoint.Scale;
		return ((owner is null ? 0 : owner.Technology[RadarImpact].Raw) + CFixedPoint.Scale) * bonus / CFixedPoint.Scale;
	}

	private static bool IsFriendly(CGameState state, CProvince province, CCountryTag tag)
	{
		return province.Controller is { } holder && (holder == tag || CIntelligence.IsFriend(state, holder, tag));
	}
}
