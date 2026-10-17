using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How much of an agent's exposed front its units hold: of its area's provinces beside the enemy's land in an area of more
/// than five provinces, the share where one of its units with no subordinates stands, over the fewer of those provinces
/// and its divisions. The original's <c>CAIUnit_GetFrontCoverage</c>. See <c>docs/mechanics/agents.md</c>, section 8.4.
/// </summary>
public static class CFrontCoverage
{
	private const long Whole = 1000;

	private const int LargeArea = 5;

	public static void Update(CGameState state, CCountry country, CAgent agent)
	{
		agent.Coverage = Of(state, country, agent);
	}

	private static long Of(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is null || agent.Area.Count == 0)
		{
			return 0;
		}
		int divisions = 0;
		foreach (CUnit unit in agent.Land)
		{
			if (unit.Kind == EUnitKind.Division && !unit.IsRemoved)
			{
				divisions++;
			}
		}
		if (divisions == 0)
		{
			return 0;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int exposed = 0;
		int covered = 0;
		foreach (int id in agent.Area)
		{
			if (!IsExposed(state, network, country, state.Province(id)))
			{
				continue;
			}
			exposed++;
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (unit.Owner == country.Tag && CTheatres.IsLand(unit) && unit.Children.Count == 0)
				{
					covered++;
					break;
				}
			}
		}
		if (exposed == 0)
		{
			return Whole;
		}
		return covered * Whole / System.Math.Min(exposed, divisions);
	}

	private static bool IsExposed(CGameState state, CSupplyNetwork network, CCountry country, CProvince province)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (beside.IsSea || beside.Controller is not { } holder || !CCountryLists.IsEnemy(state, country, holder))
			{
				continue;
			}
			if (network.AreaOf(edge.To) is { } area && area.Provinces.Count > LargeArea)
			{
				return true;
			}
		}
		return false;
	}
}
