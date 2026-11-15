using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What the agents ask of a unit's place in the order of battle: the agent it answers to, and a unit put under another.
/// See <c>docs/mechanics/agents.md</c>, section 4.3.
/// </summary>
public static class CAgentUnits
{
	/// <summary>The agent of the unit or of the nearest unit above it that has one.</summary>
	public static CAgent? NearestAgent(CUnit unit)
	{
		for (CUnit? at = unit; at is not null; at = at.Parent)
		{
			if (at.Invasion is not null)
			{
				return null;
			}
			if (at.Agent is { } agent)
			{
				return agent;
			}
		}
		return null;
	}

	/// <summary>
	/// The unit put last under the other - the same owner, the other ranking above it - with no cap on how many it holds.
	/// Its own agent, if it has one, is let go, and the agents above where it was and where it goes sort their
	/// subordinates again.
	/// </summary>
	public static bool Attach(CGameState state, CUnit unit, CUnit parent)
	{
		if (unit.Owner != parent.Owner || parent.Kind >= unit.Kind || state.Country(unit.Owner) is not { } country
			|| ReferenceEquals(unit.Parent, parent))
		{
			return false;
		}
		if (unit.Parent is { } old && NearestAgent(old) is { } oldAgent)
		{
			oldAgent.ReplanSubordinates = true;
		}
		if (unit.Agent is { } own)
		{
			CAgents.Unbind(state, own);
		}
		CTheatres.Attach(country, unit, parent);
		if (NearestAgent(parent) is { } newAgent)
		{
			newAgent.ReplanSubordinates = true;
			// An air unit put under it asks the agent to regroup its air groups (FINDINGS-liveread.md, section 56).
			if (unit.Kind == EUnitKind.Air)
			{
				newAgent.RegroupAirPending = true;
			}
		}
		return true;
	}

	/// <summary>The unit to the top of its country's order of battle, keeping what is under it; the agent above it sorts its subordinates again.</summary>
	public static void Detach(CGameState state, CUnit unit)
	{
		if (unit.Parent is not { } parent || state.Country(unit.Owner) is not { } country)
		{
			return;
		}
		if (NearestAgent(parent) is { } agent)
		{
			agent.ReplanSubordinates = true;
		}
		parent.Children.Remove(unit);
		unit.Parent = null;
		country.Units.Add(unit);
	}

	/// <summary>The unit's original level: a theatre 0, an army group 1, an army 2, a corps 3, a division 4, else 5 or 6.</summary>
	public static int Level(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Theatre => 0,
			EUnitKind.ArmyGroup => 1,
			EUnitKind.Army => 2,
			EUnitKind.Corps => 3,
			EUnitKind.Division => 4,
			EUnitKind.Navy => 5,
			_ => 6,
		};
	}

	/// <summary>A land unit whose first brigade is a garrison brigade.</summary>
	public static bool IsGarrison(CUnit unit)
	{
		return unit.SubUnits.Count > 0 && unit.SubUnits[0].Type.Key == "garrison_brigade";
	}

	/// <summary>How many units are above it in the order of battle.</summary>
	public static int Ancestors(CUnit unit)
	{
		int count = 0;
		for (CUnit? at = unit.Parent; at is not null; at = at.Parent)
		{
			count++;
		}
		return count;
	}

	/// <summary>Where a unit is going, or else where it is, by <see cref="Destination"/>.</summary>
	public static int DestinationOrPlace(CUnit unit)
	{
		int destination = Destination(unit);
		return destination > 0 ? destination : unit.Location;
	}

	/// <summary>Where a unit is going, or else where it is, by <see cref="MovementDestination"/>.</summary>
	public static int MovementDestinationOrPlace(CUnit unit)
	{
		int destination = MovementDestination(unit);
		return destination > 0 ? destination : unit.Location;
	}

	/// <summary>
	/// Where it is going, or 0, by the original's <c>CUnit_GetPendingDestinationProvince</c>: what the AI sent it to in this
	/// pass, else its way's end - and with neither, the province of an order still under way, a support attack's among them.
	/// A stand-down posts its "no order" as a command, so a division the AI has just halted on a strategic redeployment still
	/// answers that redeployment's province for the rest of the pass (FINDINGS-liveread.md, section 101). The attack asks this
	/// one, so a division waiting on a support attack counts among a province's attackers (FINDINGS-aiconsumer.md), and so does
	/// the line's look at where each committed division is heading (FINDINGS-moving.md, A.4b).
	/// </summary>
	public static int Destination(CUnit unit)
	{
		int destination = unit.AiBound >= 0 ? unit.AiBound : unit.Path.Count > 0 ? unit.Path[^1] : 0;
		if (destination == 0 && unit.Order is not (EUnitOrder.None or EUnitOrder.Move) && unit.OrderProvince > 0)
		{
			return unit.OrderProvince;
		}
		return destination;
	}

	/// <summary>
	/// Where it is going, or 0, by the original's <c>CUnit::GetMovementDestinationProvince</c>: nowhere under a support
	/// attack, whatever its way or what the AI sent it to; else as <see cref="Destination"/> (FINDINGS-liveread.md, section
	/// 101). The pull and the count of a line's positions ask this one, as do the headquarters' places, the reserve's detach,
	/// a unit's anchor for the sealift, the sealift's embark tally and an invasion's march to its port.
	/// </summary>
	public static int MovementDestination(CUnit unit)
	{
		return unit.Order == EUnitOrder.SupportAttack ? 0 : Destination(unit);
	}

	/// <summary>The country's agent working the theatre, or null.</summary>
	public static CAgent? AgentFor(CCountry country, int theatre)
	{
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Theatre == theatre)
			{
				return agent;
			}
		}
		return null;
	}
}
