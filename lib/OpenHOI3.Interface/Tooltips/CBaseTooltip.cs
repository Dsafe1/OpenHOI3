using System;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The tooltip over a port's or an airfield's picture on the map: the units of the first unit there, as its counter would
/// show them, and how many are based there against what the base can repair. See <c>docs/mechanics/tooltips.md</c>, section 7.2.
/// </summary>
public static class CBaseTooltip
{
	private const string InfoKey = "UNITBASEPROVINCE_INFO";

	public static string Build(Func<string, string> localise, CGameState state, int provinceId, EMapBuilding kind, CCountryTag? player)
	{
		var units = CMapBuildings.UnitsOf(state, provinceId, kind);
		string first = units.Count > 0 ? CUnitCounterTooltip.Build(localise, state, units[0], player) : "";
		CProvince province = state.Province(provinceId);
		string amount = units.Sum(unit => unit.SubUnits.Count).ToString(CultureInfo.InvariantCulture);
		if (kind == EMapBuilding.Port)
		{
			// With a fleet there the fleet's words stand in the place of the port's own.
			return (units.Count > 0 ? first : localise("PORT") + "\n") + "\n"
				+ Info(localise, amount, new CFixedPoint(CConvoyRules.PortCapacity(state, province)));
		}
		CBuilding? airBase = state.Buildings.FirstOrDefault(building => building.Key == CMovementRules.AirBase);
		return (units.Count > 0 ? first + "\n" : "")
			+ Info(localise, amount, airBase is null ? CFixedPoint.One : province.BuildingLevel(airBase));
	}

	private static string Info(Func<string, string> localise, string amount, CFixedPoint daily)
	{
		return CTextTemplate.Fill(localise(InfoKey), ("AMOUNT", amount), ("DAILY", CNumberText.Format(daily, 2)));
	}
}
