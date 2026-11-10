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
		var garrison = new List<CUnit>();
		foreach (CUnit unit in agent.Committed)
		{
			if (unit.IsGarrison)
			{
				garrison.Add(unit);
			}
		}
		var standing = new Dictionary<int, int>();
		CPositionCount.Count(state, country, garrison, targets, province => have[province] = have.GetValueOrDefault(province) + 1,
			province => standing[province] = standing.GetValueOrDefault(province) + 1, divisionsOnly: false);
		int each = garrison.Count / targets.Count;
		int extra = garrison.Count % targets.Count;
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
		bool moved = false;
		foreach (int id in targets)
		{
			bool threatened = agent.Threatened.Contains(id);
			CSupplyArea? area = network.AreaOf(id);
			bool focus = area is not null && agent.FocusAreas.Contains(area);
			CProvince target = state.Province(id);
			bool depot = network.DepotOf(id) == id;
			long targetScore = score.GetValueOrDefault(id);
			while (have.GetValueOrDefault(id) < want[id])
			{
				CUnit? best = null;
				float least = 1e18f;
				foreach (CUnit unit in garrison)
				{
					int bound = CAgentUnits.DestinationOrPlace(unit);
					if (Combat.CCombats.IsHoldingGround(unit) || unit.IsRetreating || unit.Location <= 0
						|| !CUnitMayLeave.Allows(state, country, unit, null, strict: true, notDefending: false))
					{
						continue;
					}
					// Where it is bound decides, not where it stands (FINDINGS-liveread.md, section 13).
					if ((!focus && !ReferenceEquals(network.AreaOf(bound), area)) || bound == id
						|| (state.Province(bound).Controller != country.Tag && CLineFill.IsEnemyHeld(state, country, bound))
						|| agent.Threatened.Contains(bound))
					{
						continue;
					}
					// A position of the list is not stripped to fill another no fuller than it would be left.
					long boundScore = 0;
					if (!threatened && want.ContainsKey(bound) && (boundScore = score.GetValueOrDefault(bound)) > 0)
					{
						int spare = targetScore > boundScore || depot ? 0 : 1;
						if (have.GetValueOrDefault(id) + (depot ? 0 : 1) >= standing.GetValueOrDefault(bound) - spare)
						{
							continue;
						}
					}
					// Single precision, as the original keeps the distance; a tie is the first unit's.
					float squared = CMapDistance.Squared(state.Province(unit.Location), target, state.MapWidth);
					if (boundScore > targetScore && CArmyRole.Of(unit) == CArmyRole.Garrison)
					{
						squared = (float)(squared * 2.0);
					}
					if (squared < least && CAiMoves.CanReach(state, country, unit, id))
					{
						least = squared;
						best = unit;
					}
				}
				if (best is null)
				{
					break;
				}
				int previous = CAgentUnits.Destination(best);
				if (previous > 0)
				{
					if (want.ContainsKey(previous))
					{
						have[previous] = have.GetValueOrDefault(previous) - 1;
					}
				}
				else if (want.ContainsKey(best.Location))
				{
					have[best.Location] = have.GetValueOrDefault(best.Location) - 1;
					standing[best.Location] = standing.GetValueOrDefault(best.Location) - 1;
				}
				// The original counts it sent whether or not the order takes.
				CAiMoves.Move(state, country, best, id, safe: true);
				moved = true;
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
