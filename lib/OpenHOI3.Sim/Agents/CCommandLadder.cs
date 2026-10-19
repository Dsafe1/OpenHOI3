using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The ladder of commands under a theatre's headquarters: a command of the level above for every four of a level and
/// one more, made a level a day, and every division, corps and army kept under or moved to the nearest command one level
/// up with room for five. Slot 85, the original's <c>OrganiseCommands</c>, <c>CreateHigherCommands</c> and
/// <c>AssignToNearestCommand</c>. See <c>docs/mechanics/agents.md</c>, section 4.
/// </summary>
public static class CCommandLadder
{
	/// <summary>Units of a level a command of the next is wanted for, beyond the first.</summary>
	private const int PerCommand = 4;

	/// <summary>The most subordinates a command keeps of the level below, before the one being placed.</summary>
	private const int Room = 4;

	/// <summary>The current commander's score is this share of its distance, in thousandths.</summary>
	private const long Kept = 850;

	/// <summary>A score at or above this places nothing, in thousandths of a distance.</summary>
	private const long Unplaced = 1_000_000_000;

	private const long NotOver = -1000;

	/// <summary>Slot 85: commands made, then garrisons and fleets put under the headquarters, then the three levels filled.</summary>
	public static void Organise(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } headquarters || CAgents.TheatreOf(state, country, agent) is null)
		{
			return;
		}
		bool reorg = country.UnitAi.RunsUnits || headquarters.Plan.AllowHqReorg;
		if (reorg)
		{
			Make(state, country, agent);
		}
		var armyGroups = new List<CUnit>();
		var armies = new List<CUnit>();
		var corps = new List<CUnit>();
		var garrisons = new List<CUnit>();
		var divisions = new List<CUnit>();
		foreach (CUnit unit in agent.Land)
		{
			unit.Promised = 0;
		}
		foreach (CUnit unit in agent.Land)
		{
			if (ReferenceEquals(unit, headquarters) || unit.IsRemoved || unit.Transport is not null)
			{
				continue;
			}
			switch (unit.Kind)
			{
				case EUnitKind.ArmyGroup:
					armyGroups.Add(unit);
					break;
				case EUnitKind.Army:
					armies.Add(unit);
					break;
				case EUnitKind.Corps:
					corps.Add(unit);
					break;
				default:
					(CAgentUnits.IsGarrison(unit) ? garrisons : divisions).Add(unit);
					break;
			}
		}
		if (reorg)
		{
			foreach (CUnit garrison in garrisons)
			{
				if (ReferenceEquals(garrison.Parent, headquarters))
				{
					continue;
				}
				if (garrison.Parent is { } old)
				{
					old.Promised--;
				}
				headquarters.Promised++;
				state.Outbox.Add(new CAttachUnitCommand(country.Tag, garrison.Id, headquarters.Id));
			}
			foreach (CUnit fleet in agent.Fleets)
			{
				if (!ReferenceEquals(fleet.Parent, headquarters))
				{
					state.Outbox.Add(new CAttachUnitCommand(country.Tag, fleet.Id, headquarters.Id));
				}
			}
		}
		Assign(state, country, divisions, corps, reorg);
		Assign(state, country, corps, armies, reorg);
		Assign(state, country, armies, armyGroups, reorg);
	}

	/// <summary>Section 4.1: commands of the level above for the divisions, the corps and the armies, borrowed or made.</summary>
	private static void Make(CGameState state, CCountry country, CAgent agent)
	{
		if (!state.SubUnitTypes.TryGetValue(CTheatreHeadquarters.HqBrigade, out CSubUnitType? hq)
			|| country.Manpower.Raw < hq.Stats[CSubUnitStats.BuildCostManpower].Raw)
		{
			return;
		}
		CUnit headquarters = agent.Unit!;
		var counts = new int[5];
		foreach (CUnit unit in agent.Land)
		{
			if (!CAgentUnits.IsGarrison(unit))
			{
				counts[CAgentUnits.Level(unit)]++;
			}
		}
		for (int level = 4; level >= 2; level--)
		{
			int need = counts[level] / PerCommand + 1;
			var chosen = new HashSet<CUnit>();
			while (counts[level - 1] < need)
			{
				if (country.UnitAi.RunsUnits && Borrow(state, country, agent, headquarters, level - 1, counts, need))
				{
					break;
				}
				if (Shallowest(state, agent, level, chosen) is not { } over)
				{
					break;
				}
				chosen.Add(over);
				state.Outbox.Add(new CCreateHigherCommand(state, country.Tag, over.Id));
				counts[level - 1]++;
			}
		}
	}

	/// <summary>
	/// Spare commands of the level wanted from the country's other agents, each attached under this headquarters; whether
	/// the need is met, asked between agents. The units taken are counted afresh each time it is asked.
	/// </summary>
	private static bool Borrow(CGameState state, CCountry country, CAgent agent, CUnit headquarters, int level, int[] counts,
		int need)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var taken = new HashSet<CUnit>();
		foreach (CAgent other in country.UnitAi.Agents)
		{
			if (ReferenceEquals(other, agent))
			{
				continue;
			}
			foreach (CUnit unit in other.NonDivisions)
			{
				if (CAgentUnits.Level(unit) != level || unit.Children.Count > 0 || unit.Promised > 0 || unit.IsRemoved
					|| unit.Location <= 0 || network.AreaOf(unit.Location) is null || taken.Contains(unit)
					|| !CAreaPaths.Joined(state, country, unit.Location, headquarters.Location))
				{
					continue;
				}
				taken.Add(unit);
				counts[level]++;
				state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, headquarters.Id));
			}
			if (counts[level] >= need)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Of the agent's land units of the level, on land and not garrison-led, the one with fewest above it.</summary>
	private static CUnit? Shallowest(CGameState state, CAgent agent, int level, HashSet<CUnit> chosen)
	{
		CUnit? best = null;
		int fewest = int.MaxValue;
		foreach (CUnit unit in agent.Land)
		{
			if (CAgentUnits.Level(unit) != level || CAgentUnits.IsGarrison(unit) || chosen.Contains(unit) || unit.IsRemoved
				|| unit.Location <= 0 || state.Province(unit.Location).IsSea)
			{
				continue;
			}
			int above = CAgentUnits.Ancestors(unit);
			if (above < fewest)
			{
				fewest = above;
				best = unit;
			}
		}
		return best;
	}

	/// <summary>Section 4.2: each unit kept under its commander, or moved to the nearest command of the level above with room.</summary>
	private static void Assign(CGameState state, CCountry country, List<CUnit> units, List<CUnit> commands, bool reorg)
	{
		foreach (CUnit unit in units)
		{
			CUnit? parent = unit.Parent;
			long over = parent is null ? NotOver : Over(state, unit, parent);
			if (parent is not null && CAgentUnits.Level(parent) == CAgentUnits.Level(unit) - 1)
			{
				if (!reorg || IsBlocked(unit))
				{
					continue;
				}
				if ((IsBlocked(parent) || over <= 0) && parent.Promised + ChildrenOf(parent, unit.Kind) <= Room)
				{
					continue;
				}
			}
			else if (parent is not null && !reorg)
			{
				continue;
			}
			CUnit? winner = null;
			long least = Unplaced;
			foreach (CUnit command in commands)
			{
				if (command.IsRemoved)
				{
					continue;
				}
				int n = command.Promised + 1 + ChildrenOf(command, unit.Kind);
				bool mayTake = n <= Room || (n == Room + 1 && (over > 0 || ReferenceEquals(command, parent)));
				if (!mayTake || unit.Location <= 0 || command.Location <= 0)
				{
					continue;
				}
				long score = CMapDistance.Between(state.Province(unit.Location), state.Province(command.Location),
					state.MapWidth) * CFixedPoint.Scale;
				if (command.Parent is { } above)
				{
					long commandOver = Over(state, command, above);
					if (commandOver > 0)
					{
						score += commandOver;
					}
				}
				if (ReferenceEquals(command, parent))
				{
					score = score * Kept / CFixedPoint.Scale;
				}
				if (score < least)
				{
					least = score;
					winner = command;
				}
			}
			if (winner is null || ReferenceEquals(winner, parent))
			{
				continue;
			}
			if (parent is not null)
			{
				parent.Promised--;
			}
			winner.Promised++;
			state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, winner.Id));
		}
	}

	/// <summary>How far a unit is beyond its commander's full reach, in thousandths of a distance; negative within it.</summary>
	private static long Over(CGameState state, CUnit unit, CUnit commander)
	{
		int from = CAgentUnits.DestinationOrPlace(unit);
		int to = CAgentUnits.DestinationOrPlace(commander);
		if (from <= 0 || to <= 0)
		{
			return NotOver;
		}
		long distance = CMapDistance.Between(state.Province(from), state.Province(to), state.MapWidth);
		return (distance - CCommandReach.Radius(state, commander)) * CFixedPoint.Scale;
	}

	private static int ChildrenOf(CUnit command, EUnitKind kind)
	{
		int count = 0;
		foreach (CUnit child in command.Children)
		{
			if (child.Kind == kind && !child.IsRemoved)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// The original's <c>UnitHasBlockingOrder</c>: a unit on the move, or one under an order that keeps it busy - ours,
	/// any order, the original asking each kind but support attack.
	/// </summary>
	public static bool IsBlocked(CUnit unit)
	{
		return unit.IsMoving || (unit.Order is not (EUnitOrder.None or EUnitOrder.Move or EUnitOrder.SupportAttack
			or EUnitOrder.CarrierProtection));
	}
}
