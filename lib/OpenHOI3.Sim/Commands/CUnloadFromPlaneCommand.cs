using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country putting one of its land units off the air unit it is aboard, as the original's unload-from-plane command
/// does: refused unless the unit is aboard, stands on land and its air unit is at its base, and otherwise it gets off
/// where it is, bringing nothing, and looks for a battle there. See <c>docs/mechanics/transport.md</c>, section 13.3.
/// </summary>
public sealed class CUnloadFromPlaneCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;

	public CUnloadFromPlaneCommand(CCountryTag country, int unit)
	{
		_country = country;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is { Plane: { } plane } unit
			&& unit.Location > 0 && !state.Province(unit.Location).IsSea && CAirMissions.IsAtOwnBase(plane);
	}

	public override void Execute(CGameState state)
	{
		CAirTransport.Release(state, Find(state.Country(_country)!)!, parachuted: false);
	}

	private CUnit? Find(CCountry country)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == _unit)
			{
				return unit;
			}
		}
		return null;
	}
}
