using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The two colours of every province in the air map mode, land and sea alike. See
/// <c>docs/mechanics/airmap.md</c>, section 3:
///
///     grey          every province to begin with
///     green         where one of the player's air intercepts reaches - both colours,
///                   the stripes only where they are still grey
///     red stripes   where one of its air superiority missions reaches - the ground
///                   too where it is still grey
///
/// Each is the original's packed byte over 255, with its alpha of a half.
/// </summary>
public static class CAirMapColours
{
	private const float Half = 0x80 / 255f;
	private const float Low = 0x58 / 255f;

	public static readonly CColor Grey = new(Half, Half, Half, Half);
	public static readonly CColor Green = new(Low, 1f, Low, Half);
	public static readonly CColor Red = new(1f, Low, Low, Half);

	/// <summary>
	/// The provinces the player's fighter missions colour, with their two colours. A province
	/// not in it is grey.
	/// </summary>
	public static Dictionary<int, (CColor Ground, CColor Stripes)> Build(CGameState state, CCountryTag? player)
	{
		var coloured = new Dictionary<int, (CColor Ground, CColor Stripes)>();
		if (state.Country(player) is not { } country)
		{
			return coloured;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			bool intercept = unit.Order == EUnitOrder.AirIntercept;
			if (!intercept && unit.Order != EUnitOrder.AirSuperiority)
			{
				continue;
			}
			foreach (int id in unit.Mission.Provinces)
			{
				CProvince province = state.Province(id);
				if (!CUnitOrders.IsInRange(state, unit, unit.Order, province))
				{
					continue;
				}
				(CColor ground, CColor stripes) = coloured.TryGetValue(id, out var was) ? was : (Grey, Grey);
				if (intercept)
				{
					ground = Green;
					stripes = stripes == Grey ? Green : stripes;
				}
				else
				{
					stripes = Red;
					ground = ground == Grey ? Red : ground;
				}
				coloured[id] = (ground, stripes);
			}
		}
		return coloured;
	}

	/// <summary>
	/// What the player's fighter missions are now - which unit, which order, which provinces -
	/// as one figure, so that the map is coloured again when an order of the two kinds starts
	/// or goes. Ours: the original colours it at those moments; this sees them from outside.
	/// </summary>
	public static long Figure(CGameState state, CCountryTag? player)
	{
		long figure = 17;
		if (state.Country(player) is not { } country)
		{
			return figure;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Order != EUnitOrder.AirIntercept && unit.Order != EUnitOrder.AirSuperiority)
			{
				continue;
			}
			figure = figure * 31 + unit.Id;
			figure = figure * 31 + (int)unit.Order;
			foreach (int id in unit.Mission.Provinces)
			{
				figure = figure * 31 + id;
			}
		}
		return figure;
	}
}
