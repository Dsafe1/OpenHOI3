using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country putting one of its land units aboard one of its air units, as the original's load-on-plane command does:
/// refused where either is gone or the air unit may not take the unit, and otherwise puts it aboard where it stands and
/// drops its order. See <c>docs/mechanics/transport.md</c>, section 13.3.
/// </summary>
public sealed class CLoadOnPlaneCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly int _plane;

	public CLoadOnPlaneCommand(CCountryTag country, int unit, int plane)
	{
		_country = country;
		_unit = unit;
		_plane = plane;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country, _unit) is { } unit
			&& Find(country, _plane) is { } plane && CAirTransport.MayLoad(state, plane, unit);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country, _unit)!;
		if (CMovementRules.IsLandUnit(unit) && unit.Plane is null)
		{
			CAirTransport.Board(Find(country, _plane)!, unit);
			// Ours: it goes where the air unit goes and nowhere else (transport.md, Ours).
			CMovement.Cancel(unit);
		}
		CUnitOrders.Clear(unit);
	}

	private static CUnit? Find(CCountry country, int id)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == id)
			{
				return unit;
			}
		}
		return null;
	}
}
