using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What a province's hold value asks of the world and not of the province, worked out once for a run of provinces
/// (<see cref="CProvinceHoldValue.Context"/>): whether the country is at peace, whether its theatre's needs are full, and
/// how many ships every country bases in each province, at sea or not (FINDINGS-liveread.md, section 15) - counted at the
/// first port asked about, where walking every country's units for each port was a fifth of the unit walks under
/// BlackICE. A run changes nothing, so none goes stale.
/// </summary>
public sealed class CHoldContext
{
	private readonly CGameState _state;
	private Dictionary<int, int>? _ships;

	public CHoldContext(CGameState state, bool atPeace, bool full)
	{
		_state = state;
		AtPeace = atPeace;
		Full = full;
	}

	public bool AtPeace { get; }

	public bool Full { get; }

	/// <summary>The ships of every country's fleets whose base is the province.</summary>
	public int ShipsBasedAt(int province)
	{
		if (_ships is null)
		{
			_ships = new Dictionary<int, int>();
			foreach (CCountry country in _state.Countries)
			{
				foreach (CUnit unit in country.AllUnits())
				{
					if (unit.Kind == EUnitKind.Navy)
					{
						_ships[unit.Base] = _ships.GetValueOrDefault(unit.Base) + unit.SubUnits.Count;
					}
				}
			}
		}
		return _ships.GetValueOrDefault(province);
	}
}
