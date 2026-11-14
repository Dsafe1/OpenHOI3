using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How the AI moves a unit: a halt where it is sent to where it stands while it moves, nothing where it is going there
/// already, a strategic redeployment for a long way asked for safely, a march otherwise; and the stand-down. The
/// original's <c>CEU3AI::MoveUnit</c>, <c>CEU3AI_ShouldRedeployStrategically</c> and <c>CUnit::AIStandDown</c>. See
/// <c>docs/mechanics/moving.md</c>, section 10.
/// </summary>
public static class CAiMoves
{
	private const long Whole = 1000;

	/// <summary>A long way is this many hours at the unit's top speed, a quarter of it, at war; at peace three times as many.</summary>
	private const long AtWar = 200;

	private const long AtPeace = 600;

	/// <summary>
	/// Sends the unit; whether a command was posted. Ours: nothing is posted, and the answer is no, where the unit has no
	/// way there (<c>docs/mechanics/moving.md</c>, section 19).
	/// </summary>
	public static bool Move(CGameState state, CCountry country, CUnit unit, int province, bool safe)
	{
		if (province <= 0 || unit.Location <= 0 || unit.IsRetreating)
		{
			return false;
		}
		// Nothing where the click's test refuses the province, asked before the stand-down: so a unit marching into a battle
		// is neither halted nor sent anywhere (FINDINGS-liveread.md, section 88).
		if (!CMovementRules.MayMoveTo(state, country, unit, state.Province(province)))
		{
			return false;
		}
		if (province == unit.Location)
		{
			if (CAgentUnits.Destination(unit) > 0)
			{
				StandDown(state, country, unit, state.AiTrace is null ? "" : "sent where it stands by " + new System.Diagnostics.StackFrame(1).GetMethod()?.Name);
				return true;
			}
			return false;
		}
		if (CAgentUnits.Destination(unit) == province && !unit.IsFighting)
		{
			return false;
		}
		ERouteSafety safety = safe ? ERouteSafety.VerySafe : ERouteSafety.Plain;
		if (!CanReach(state, country, unit, province, safety))
		{
			return false;
		}
		unit.AiBound = province;
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"    move {unit.Name}#{unit.Id} {unit.Location} -> {province} (way to {(unit.IsMoving ? unit.Path[^1] : 0)}, ai {unit.AiBound}, order {unit.Order}){(unit.IsFighting ? " (fighting)" : "")}{(System.Linq.Enumerable.Any(unit.Combats, combat => combat.Defender.Units.Contains(unit)) ? " (defending)" : "")} by {new System.Diagnostics.StackFrame(1).GetMethod()?.Name}");
		}
		if (safe && ShouldRedeploy(state, country, unit, province))
		{
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, unit.Id, EUnitOrder.StrategicRedeployment, province));
		}
		else
		{
			state.Outbox.Add(new CMoveUnitCommand(country.Tag, unit.Id, province, landSafety: safety, ai: true));
		}
		return true;
	}

	/// <summary>
	/// The original's <c>CMapProvince::CanUnitReach</c>: yes where the unit stands there already; no for a unit falling back
	/// or with no brigade; otherwise whether the unit has a way there - the safe way, as the garrisons ask it; the line asks
	/// <see cref="ReachesByLand"/>, as the original does (<c>docs/mechanics/moving.md</c>, section 10).
	/// </summary>
	public static bool CanReach(CGameState state, CCountry country, CUnit unit, int province,
		ERouteSafety safety = ERouteSafety.Safe)
	{
		if (unit.Location == province)
		{
			return true;
		}
		if (unit.IsRetreating || unit.SubUnits.Count == 0 || unit.Location <= 0)
		{
			return false;
		}
		return CRouteFinder.Find(state, country, unit, unit.Location, province, landSafety: safety) is not null;
	}

	/// <summary>
	/// The original's <c>CMapProvince::CanUnitReach</c> for a land unit, which looks for no way: not into the battle of an
	/// enemy standing there while it fights, nor into an enemy's province; then yes in one area, or beside its province by
	/// land, and otherwise, where it stands where it may not or has a neighbour it may enter, yes to an area beside its own
	/// or joined to it by areas no enemy holds. The record of a failed order, which refuses a province for two days, is not
	/// built (<c>docs/mechanics/moving.md</c>, section 10).
	/// </summary>
	public static bool ReachesByLand(CGameState state, CCountry country, CUnit unit, int province)
	{
		if (unit.Location == province)
		{
			return true;
		}
		if (unit.IsRetreating || unit.SubUnits.Count == 0 || unit.Location <= 0)
		{
			return false;
		}
		CProvince target = state.Province(province);
		if (CMovementRules.IsEnemyIn(state, unit.Owner, target.Controller, province))
		{
			return false;
		}
		if (unit.IsFighting)
		{
			foreach (CUnit other in state.Positions.Of(province))
			{
				if (CMovementRules.IsLandUnit(other) && !other.IsRetreating && CMovementRules.IsEnemyIn(state, unit.Owner, other.Owner, province))
				{
					return false;
				}
			}
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? from = network.AreaOf(unit.Location);
		CSupplyArea? to = network.AreaOf(province);
		if (from is not null && ReferenceEquals(from, to))
		{
			return true;
		}
		CProvince here = state.Province(unit.Location);
		bool way = !CMovementRules.MayEnter(state, country, here);
		foreach (CProvinceEdge edge in here.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable || state.Province(edge.To).IsSea)
			{
				continue;
			}
			if (edge.To == province)
			{
				return true;
			}
			way |= CMovementRules.MayEnter(state, country, state.Province(edge.To));
		}
		if (!way)
		{
			return false;
		}
		return (from is not null && to is not null && from.Neighbours.Contains(to)) || CAreaPaths.Joined(state, country, unit.Location, province);
	}

	/// <summary>
	/// Not to a neighbour, and at least a quarter of its top speed times 200 hours at war, 600 at peace - whatever order the
	/// unit holds, so one already redeploying is given a new redeployment and keeps its pace (FINDINGS-liveread.md, section 75).
	/// </summary>
	public static bool ShouldRedeploy(CGameState state, CCountry country, CUnit unit, int province)
	{
		if (CAreaScores.Borders(state, unit.Location, province))
		{
			return false;
		}
		long speed = CMovementSpeed.SlowestBrigade(unit).Raw;
		long hours = state.IsAtWar(country.Tag) ? AtWar : AtPeace;
		long distance = CMapDistance.Between(state.Province(unit.Location), state.Province(province), state.MapWidth);
		return distance * Whole >= speed / 4 * hours;
	}

	/// <summary>No order, no way, out of the battles it attacks in, where it stands.</summary>
	/// <param name="why">What called it off, for a tool's trace.</param>
	public static void StandDown(CGameState state, CCountry country, CUnit unit, string why)
	{
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"    halt {unit.Name}#{unit.Id} at {unit.Location}: {why}");
		}
		unit.AiBound = 0;
		state.Outbox.Add(new CCancelMovementCommand(country.Tag, unit.Id));
	}
}
