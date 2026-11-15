using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// What carrying supplies out to its provinces adds to what a country's units asked for: in its day, right after its
/// units and the units waiting to be placed have drawn, each province it controls that has a unit in it - or, with none,
/// a naval base and nothing of supplies passed through it today - adds its hops to its depot times what a load loses
/// crossing it. So the supply slider's need carries the network's carriage as well as the units' own use
/// (<c>docs/mechanics/supply.md</c>, section 11; <c>reversing/findings/FINDINGS-laterstart.md</c>, section 18).
/// </summary>
public sealed class CSupplyCarriage
{
	private readonly CGameState _state;
	private readonly CSupplyNetwork _network;
	private readonly CBuilding? _navalBase;
	private readonly Dictionary<CCountryTag, List<CProvince>> _controlled = new();

	/// <summary>Made once for the units' day: the network, and each country's provinces, which the day does not change.</summary>
	public CSupplyCarriage(CGameState state)
	{
		_state = state;
		_network = CSupplyNetworks.Refresh(state);
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(CSupplyNetworks.NavalBase, StringComparison.OrdinalIgnoreCase))
			{
				_navalBase = building;
			}
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is { } holder)
			{
				if (!_controlled.TryGetValue(holder, out List<CProvince>? list))
				{
					list = new List<CProvince>();
					_controlled[holder] = list;
				}
				list.Add(province);
			}
		}
	}

	/// <summary>Adds the country's carriage to what its units asked for, as its provinces stand now.</summary>
	public void AddTo(CCountry country)
	{
		if (!_controlled.TryGetValue(country.Tag, out List<CProvince>? provinces))
		{
			return;
		}
		long added = 0;
		foreach (CProvince province in provinces)
		{
			if (Counts(_state, province, _navalBase))
			{
				// The hops times a thousand, times the loss in thousandths, over a thousand: the hops times the loss.
				added += _network.HopsOf(province.Id) * CSupplyFigures.Loss(_state, province).Raw;
			}
		}
		CSupplyPass.Add(country.UnitDemand, EGoods.Supplies, added);
	}

	/// <summary>
	/// Whether a province adds its carriage: a unit in it, whoever's; else a naval base built and no supplies passed
	/// through it today - the original's test as it stands, not what it may have meant.
	/// </summary>
	public static bool Counts(CGameState state, CProvince province, CBuilding? navalBase)
	{
		if (state.UnitsIn(province.Id).Count > 0)
		{
			return true;
		}
		return navalBase is not null && province.BuildingMaxLevel(navalBase).Raw > 0
			&& province.SupplyThroughput[EGoods.Supplies].Raw == 0;
	}
}
