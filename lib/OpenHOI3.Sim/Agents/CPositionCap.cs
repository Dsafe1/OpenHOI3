using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The most divisions the AI puts on one position: a stack, from <c>BASE_STACKING_PENALTY</c>, less its fort; beside
/// small enemy areas, no more than the enemy's units there. The original's <c>CAIUnit_GetPositionCap</c>. See
/// <c>docs/mechanics/moving.md</c>, section 7.3.
/// </summary>
public static class CPositionCap
{
	private const long Whole = 1000;

	private const int DefaultStack = 20;

	private const int MostFort = 6;

	private const int SmallArea = 6;

	public const string BaseStackingPenalty = "BASE_STACKING_PENALTY";

	/// <summary>round(0.5 / -<c>BASE_STACKING_PENALTY</c>): 5 at the base game's -0.1; 20 with no penalty.</summary>
	public static int Stack(CGameState state)
	{
		long penalty = -state.Defines[Economy.CManpower.MilitarySection, BaseStackingPenalty].Raw;
		if (penalty <= 0)
		{
			return DefaultStack;
		}
		return (int)((Whole * Whole / 2 + penalty / 2) / penalty / Whole);
	}

	public static int Of(CGameState state, CCountry country, CAgent agent, CProvince province, int stack)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool bordersEnemy = false;
		bool allSmall = true;
		bool coastal = false;
		int enemies = 0;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea || beside.Controller is not { } holder || !CCountryLists.IsEnemy(state, country, holder))
			{
				continue;
			}
			bordersEnemy = true;
			if (network.AreaOf(edge.To) is { } area)
			{
				allSmall &= area.Provinces.Count < SmallArea;
				coastal |= !area.IsLandlocked;
			}
			foreach (CUnit unit in state.Positions.Of(edge.To))
			{
				if (CTheatres.IsLand(unit) && unit.Owner == holder)
				{
					enemies++;
				}
			}
		}
		if (!bordersEnemy && !agent.Area.Contains(province.Id))
		{
			return 0;
		}
		long fort = province.Modifier[CCombatModifiers.FortLevel].Raw / Whole;
		int cap = System.Math.Max(stack - (int)System.Math.Min(MostFort, fort * 2 / 3), 1);
		if (bordersEnemy && allSmall)
		{
			int most = System.Math.Max(enemies, 1) * (coastal ? 2 : 1);
			cap = System.Math.Min(cap, most);
		}
		return cap;
	}
}
