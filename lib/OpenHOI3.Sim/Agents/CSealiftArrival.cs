using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What the sealift makes of a land unit stepping into a province, in the hour it steps: its own agent, told at once, has
/// its areas looked at again in the next hour where the unit now stands in its target's area; and a unit with its way
/// done, not aboard, standing in the plan's embark port boards the first transport of the agent's lying there that can
/// take it - not at the sealift's next daily pass. The original's <c>CAIUnit</c> slot 71, called from
/// <c>CUnit::AdvanceMovement</c>, and the boarding it calls. See <c>docs/mechanics/moving.md</c>, section 15.
/// </summary>
public static class CSealiftArrival
{
	/// <summary>Called by the march after a land unit has entered a province.</summary>
	public static void OnEntered(CGameState state, CUnit unit)
	{
		if (unit.SealiftTarget <= 0 || unit.IsRemoved || CAgentUnits.NearestAgent(unit) is not { } agent)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? here = network.AreaOf(unit.Location);
		if (here is not null && ReferenceEquals(network.AreaOf(unit.SealiftTarget), here))
		{
			agent.ReassignAreasNow = true;
			return;
		}
		if (unit.IsMoving || unit.Transport is not null || agent.Unit is not { } headquarters
			|| headquarters.Plan.Embark <= 0 || unit.Location != headquarters.Plan.Embark)
		{
			return;
		}
		Board(state, agent, unit);
	}

	/// <summary>
	/// A unit standing in the embark port, with the plan's debark set too, put aboard the first fleet there - by the
	/// province's order of units - that is the agent's own, can carry it and is one of its transports; whether one took it.
	/// </summary>
	public static bool Board(CGameState state, CAgent agent, CUnit unit)
	{
		if (unit.Transport is not null || agent.Unit is not { } headquarters || headquarters.Plan.Embark < 1
			|| headquarters.Plan.Debark < 1 || unit.Location != headquarters.Plan.Embark)
		{
			return false;
		}
		foreach (CUnit fleet in state.UnitsIn(unit.Location))
		{
			if (fleet.Kind != EUnitKind.Navy || !ReferenceEquals(CAgentUnits.NearestAgent(fleet), agent)
				|| !CTransport.CanCarry(fleet, unit) || !agent.TransportFleets.Contains(fleet))
			{
				continue;
			}
			if (state.Country(unit.Owner) is { } country && state.AiTrace is { } trace && trace.Country == country.Tag)
			{
				trace.Write($"    board {unit.Name}#{unit.Id} onto {fleet.Name} at {unit.Location} on arrival");
			}
			state.Outbox.Add(new CLoadUnitCommand(unit.Owner, unit.Id, fleet.Id));
			if (state.Country(unit.Owner) is { } owner)
			{
				CSealiftSailing.Run(state, owner, agent, fleet, new[] { unit });
			}
			return true;
		}
		return false;
	}
}
