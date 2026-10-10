using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the supply map mode says of a province, in the original's own words: where it
/// is supplied from and how far that is, what lies there, what was asked of it and what
/// passed through it, and what its harbour can land - and on the sea, which of the
/// player's convoys go by. See <c>docs/mechanics/supplymap.md</c>, section 3.
/// </summary>
public static class CSupplyMapText
{
	private const int Places = 2;

	/// <summary>Below this much of what the player knows of a province its figures are not told.</summary>
	private const int KnownWell = 9;

	private const long LeastInfrastructure = 200;

	public static List<string> Lines(Func<string, string> localise, CGameState state, CSupplyNetwork network,
		CProvince province, CCountryTag? player)
	{
		var lines = new List<string>();
		if (province.IsSea)
		{
			if (state.Country(player) is { } owner)
			{
				foreach (CConvoy convoy in owner.Convoys)
				{
					if (convoy.Path.Contains(province.Id))
					{
						lines.Add(localise("LOGTT_8") + state.Province(convoy.Start).Name + localise("LOGTT_9")
							+ state.Province(convoy.End).Name + localise("LOGTT_10"));
					}
				}
			}
			return lines;
		}

		if (CSupplyFigures.MostInfrastructure(state, province) < LeastInfrastructure)
		{
			lines.Add(localise("TOOLOWINFRA"));
			return lines;
		}

		int depot = network.DepotOf(province.Id);
		if (depot != 0)
		{
			lines.Add(localise("LOGTT_1") + state.Province(depot).Name
				+ " (" + network.DistanceOf(province.Id) + " " + localise("LOGTT_2") + ")");
		}
		if (CSupplyMapColours.Intelligence(province, player) < KnownWell)
		{
			return lines;
		}

		lines.Add(localise("LOGTT_3") + Short(province.Pool, province.SupplyNeed, EGoods.Supplies)
			+ localise("LOGTT_4") + Short(province.Pool, province.SupplyNeed, EGoods.Fuel));
		lines.Add(localise("LOGTT_5") + CNumberText.Format(province.SupplyDrawn[EGoods.Supplies], Places)
			+ localise("LOGTT_6") + CNumberText.Format(province.SupplyDrawn[EGoods.Fuel], Places));
		lines.Add(localise("LOGTT_7") + CNumberText.Format(province.SupplyThroughput[EGoods.Supplies], Places)
			+ localise("LOGTT_6") + CNumberText.Format(province.SupplyThroughput[EGoods.Fuel], Places));
		if (CConvoyRules.IsHarbour(state, province))
		{
			string landed = CNumberText.Format(new CFixedPoint(CConvoyRules.PortCapacity(state, province)), Places);
			lines.Add(localise("PORT_SHIPS").Replace("$SIZE$", landed, StringComparison.Ordinal));
		}
		return lines;
	}

	/// <summary>What lies there of a good, in red where it is less than the units ask for in a day.</summary>
	private static string Short(CGoodsPool pool, CGoodsPool need, EGoods goods)
	{
		string figure = CNumberText.Format(pool[goods], Places);
		return pool[goods] < need[goods]
			? CColouredText.In(CColouredText.Red) + figure + CColouredText.In('W')
			: figure;
	}
}
