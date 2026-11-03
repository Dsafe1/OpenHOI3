using System.Collections.Generic;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's garrisons shared over its garrison targets, a threatened one wanting more, and the nearest free garrison
/// sent to each that is short. The original's <c>CAIUnit_FillGarrisons</c>. See <c>docs/mechanics/moving.md</c>, section
/// 7.4.
/// </summary>
public static class CGarrisonFill
{
	public static bool Fill(CGameState state, CCountry country, CAgent agent, int garrisons, List<int> targets)
	{
		if (targets.Count == 0)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int stack = CPositionCap.Stack(state);
		var want = new Dictionary<int, int>();
		var have = new Dictionary<int, int>();
		var score = new Dictionary<int, long>();
		foreach (CHoldProvince hold in agent.HoldProvinces)
		{
			score.TryAdd(hold.Province, hold.Score);
		}
		int each = garrisons / targets.Count;
		int extra = garrisons % targets.Count;
		for (int index = 0; index < targets.Count; index++)
		{
			int id = targets[index];
			int wanted = each + (index < extra ? 1 : 0);
			if (agent.Threatened.Contains(id))
			{
				wanted += Incoming(state, country, id);
			}
			want[id] = System.Math.Min(wanted, stack);
		}
		var garrison = new List<CUnit>();
		foreach (CUnit unit in agent.Committed)
		{
			if (!unit.IsGarrison)
			{
				continue;
			}
			garrison.Add(unit);
			int bound = CAgentUnits.DestinationOrPlace(unit);
			if (want.ContainsKey(bound))
			{
				have[bound] = have.GetValueOrDefault(bound) + 1;
			}
		}
		bool moved = false;
		foreach (int id in targets)
		{
			bool threatened = agent.Threatened.Contains(id);
			CSupplyArea? area = network.AreaOf(id);
			bool focus = area is not null && agent.FocusAreas.Contains(area);
			CProvince target = state.Province(id);
			while (have.GetValueOrDefault(id) < want[id])
			{
				CUnit? best = null;
				long least = long.MaxValue;
				foreach (CUnit unit in garrison)
				{
					int bound = CAgentUnits.DestinationOrPlace(unit);
					if (bound == id || Combat.CCombats.IsHoldingGround(unit) || unit.IsRetreating || unit.Location <= 0
						|| agent.Threatened.Contains(unit.Location))
					{
						continue;
					}
					if (!focus && !ReferenceEquals(network.AreaOf(unit.Location), area))
					{
						continue;
					}
					if (!threatened && want.TryGetValue(bound, out int theirs) && have.GetValueOrDefault(bound) <= theirs)
					{
						continue;
					}
					if (!CUnitMayLeave.Allows(state, country, unit, null, strict: true, notDefending: false))
					{
						continue;
					}
					long squared = CMapDistance.Squared(state.Province(unit.Location), target, state.MapWidth);
					if (CArmyRole.Of(unit) == CArmyRole.Garrison && score.GetValueOrDefault(bound) > score.GetValueOrDefault(id))
					{
						squared *= 2;
					}
					if (squared < least)
					{
						least = squared;
						best = unit;
					}
				}
				int previous = best is null ? 0 : CAgentUnits.DestinationOrPlace(best);
				if (best is null || !CAiMoves.Move(state, country, best, id, safe: true))
				{
					break;
				}
				moved = true;
				if (want.ContainsKey(previous))
				{
					have[previous] = have.GetValueOrDefault(previous) - 1;
				}
				have[id] = have.GetValueOrDefault(id) + 1;
			}
		}
		return moved;
	}

	/// <summary>The brigades of hostile land units bound for the province.</summary>
	private static int Incoming(CGameState state, CCountry country, int province)
	{
		int brigades = 0;
		foreach (CCountry enemy in CThreatenedProvinces.Hostile(state, country))
		{
			foreach (CUnit unit in enemy.AllUnits())
			{
				if (CTheatres.IsLand(unit) && unit.Path.Count > 0 && unit.Path[^1] == province)
				{
					brigades += unit.SubUnits.Count;
				}
			}
		}
		return brigades;
	}
}
