using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A blockaded port and a country's share of its home ports cut off, the original's <c>UpdateProvinceBlockaded</c> and
/// <c>UpdateBlockadeLevel</c>. A port is worked out only as its controller is set - a fleet sailing in or out tells it
/// nothing - and a country's share each month, on entering a game and with any of its ports. Neither is saved. See
/// <c>docs/mechanics/modifiers.md</c>, section 3.
/// </summary>
public static class CBlockade
{
	/// <summary>
	/// The province's port worked out again: where it has an owner, a port and a naval base built, blockaded while some
	/// enemy unit of its controller's with a brigade or a ship stands in the port's sea province, fighting no battle and
	/// not falling back - and then its owner's share. Anything else clears it, and leaves the share alone.
	/// </summary>
	public static void UpdateProvince(CGameState state, CProvince province)
	{
		if (province.Owner is not { } owner || province.PortSea == 0
			|| CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) <= CFixedPoint.Zero)
		{
			province.Blockaded = false;
			return;
		}
		long brigades = 0;
		foreach (CUnit unit in state.UnitsIn(province.PortSea))
		{
			if (Blockades(state, province, unit))
			{
				brigades += unit.SubUnits.Count;
			}
		}
		province.Blockaded = brigades >= 1;
		if (state.Country(owner) is { } country)
		{
			UpdateLevel(state, country);
		}
	}

	/// <summary>The original's <c>UnitBlockadesProvince</c>: not fighting, not falling back, an enemy of the port's holder, and a brigade or a ship to it.</summary>
	public static bool Blockades(CGameState state, CProvince port, CUnit unit)
	{
		return !unit.IsFighting && !unit.IsRetreating
			&& CMovementRules.IsEnemyIn(state, unit.Owner, port.Controller, port.Id)
			&& unit.SubUnits.Count >= 1;
	}

	/// <summary>Every country's share, as the month and a game's entry work it out.</summary>
	public static void UpdateLevels(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			UpdateLevel(state, country);
		}
	}

	/// <summary>
	/// The share of its home ports that are cut off: of the provinces it owns in its acting capital's area or on its
	/// continent with a port and a naval base built, those blockaded or held by somebody else, in thousandths of them all,
	/// truncated; nought with none.
	/// </summary>
	public static void UpdateLevel(CGameState state, CCountry country)
	{
		CProvince capital = state.Province(country.ActingCapital);
		HashSet<int>? area = null;
		long ports = 0;
		long cut = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || province.Owner != country.Tag)
			{
				continue;
			}
			area ??= new HashSet<int>(Supply.CSupplyNetworks.AreaAround(state, capital.Id));
			bool home = area.Contains(province.Id)
				|| (capital.Id != 0 && capital.Continent.Equals(province.Continent, StringComparison.OrdinalIgnoreCase));
			if (!home || province.PortSea == 0
				|| CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) <= CFixedPoint.Zero)
			{
				continue;
			}
			ports++;
			if (province.Blockaded || province.Controller != province.Owner)
			{
				cut++;
			}
		}
		country.Blockade = new CFixedPoint(ports == 0 ? 0 : cut * 1000 / ports);
	}
}
