using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The sealift's sailing step: whether a transport carrying troops at the plan's embark port sails them to debark. Of the
/// agent's land units bound for debark and standing in embark's area: all aboard, it sails; one not aboard that the
/// transports have room for, it sails only where it may stand in debark and the nearest such one is more than 50 hours off;
/// none that fits, it sails where it may stand in debark. It sails under a transport order, or an invasion where debark is
/// enemy-held. The original asks it as a unit boards - <c>CAIUnit</c> slot 69, which also clears the unit's held-back and
/// garrison marks - and in the sealift's pass, and the order it gives runs in the boarding's hour; here the AI asks it as it
/// posts the load, counting the units boarding as aboard, so that the order goes out right behind the load and runs in the
/// same drain (ours: a command posted by another command waits a drain here; <c>reversing/findings/FINDINGS-liveread.md</c>,
/// sections 63 and 89). The original's <c>CAIUnit_SailTransportOrWait</c>. See <c>docs/mechanics/moving.md</c>, section 15.
/// </summary>
public static class CSealiftSailing
{
	/// <summary>Further off than this many hours, a unit still to come is not waited for.</summary>
	private const double FarHours = 50.0;

	/// <summary>Called as a land unit has boarded a fleet: where the fleet has an agent above it, the unit's marks are cleared.</summary>
	public static void OnBoarded(CGameState state, CUnit unit)
	{
		if (unit.Transport is not { } fleet || CAgentUnits.NearestAgent(fleet) is null)
		{
			return;
		}
		unit.IsHeldBack = false;
		unit.IsGarrison = false;
	}

	/// <summary>
	/// Sails the fleet where it should, the units in <paramref name="boarding"/> counted as aboard it already; whether it was
	/// ordered.
	/// </summary>
	public static bool Run(CGameState state, CCountry country, CAgent agent, CUnit fleet,
		System.Collections.Generic.ICollection<CUnit>? boarding = null)
	{
		int aboard = fleet.Passengers.Count + (boarding?.Count ?? 0);
		if (aboard == 0 || agent.Unit is not { } headquarters || headquarters.Plan.Embark <= 0
			|| headquarters.Plan.Debark <= 0)
		{
			return false;
		}
		int embark = headquarters.Plan.Embark;
		int debark = headquarters.Plan.Debark;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? embarkArea = network.AreaOf(embark);
		// The room: this fleet's carrying less what it carries, and the agent's other transports' (ours: their room too).
		long own = CTransport.Capacity(fleet) - CTransport.Load(fleet);
		foreach (CUnit unit in boarding ?? (System.Collections.Generic.ICollection<CUnit>)System.Array.Empty<CUnit>())
		{
			own -= CTransport.Weight(unit);
		}
		long room = own;
		foreach (CUnit other in agent.TransportFleets)
		{
			if (!ReferenceEquals(other, fleet) && !other.IsRemoved)
			{
				room += CTransport.Capacity(other) - CTransport.Load(other);
			}
		}
		bool waiting = false;
		bool fits = false;
		double least = double.MaxValue;
		foreach (CUnit unit in agent.Land)
		{
			if (unit.IsRemoved || unit.SealiftTarget != debark || unit.Transport is not null || boarding?.Contains(unit) == true
				|| !ReferenceEquals(network.AreaOf(CAgentUnits.DestinationOrPlace(unit)), embarkArea))
			{
				continue;
			}
			waiting = true;
			long weight = CTransport.Weight(unit);
			if (weight > room)
			{
				continue;
			}
			fits = true;
			if (weight <= own)
			{
				least = System.Math.Min(least, Hours(state, unit, embark));
			}
		}
		bool mayStand = CMovementRules.MayStandIn(state, country, fleet, state.Province(debark));
		bool sail = !waiting || (fits ? mayStand && least > FarHours : mayStand);
		if (!sail)
		{
			return false;
		}
		EUnitOrder order = CCountryLists.IsEnemy(state, country, network.AreaOf(debark)?.Controller ?? default)
			? EUnitOrder.Invasion
			: EUnitOrder.Transport;
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"    sail {fleet.Name}#{fleet.Id} from {fleet.Location} to {debark} ({order}), {aboard} aboard");
		}
		state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, order, debark, ai: true));
		return true;
	}

	/// <summary>
	/// The hours a unit needs to reach embark: the map's distance over its own top speed, or over the strategic
	/// redeployment's speed where it is redeploying (ours: what the original scales that speed by is not read).
	/// </summary>
	private static double Hours(CGameState state, CUnit unit, int embark)
	{
		double distance = CMapDistance.Between(state.Province(unit.Location), state.Province(embark), state.MapWidth);
		double speed = unit.Order == EUnitOrder.StrategicRedeployment
			? state.Defines[Economy.CManpower.MilitarySection, CMovementSpeed.StratRedeployBaseSpeed].Raw / 1000.0
			: CMovementSpeed.SlowestBrigade(unit).Raw / 1000.0;
		return speed > 0 ? distance / speed : double.MaxValue;
	}
}
