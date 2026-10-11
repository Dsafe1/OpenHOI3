using System;
using System.Text;
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
/// What a province's tooltip says in the supply map mode, put together as the original puts
/// it: where it is supplied from and how far that is, what its infrastructure lets through,
/// what lies there, what was asked of it and what passed through it, what its harbour can
/// land - and on the sea, which of the player's convoys go by and what each ships. See
/// <c>docs/mechanics/supplymap.md</c>, section 3.
/// </summary>
public static class CSupplyMapText
{
	private const int Places = 2;

	/// <summary>Above this much of what the player knows of a province, where it is supplied from is told.</summary>
	private const int KnownSome = 5;

	/// <summary>Above this much, its figures are told.</summary>
	private const int KnownWell = 8;

	private const long LeastInfrastructure = 200;

	/// <summary>An infrastructure figure is a level from 0 to 10: ten times the share.</summary>
	private const long LevelsPerWhole = 10;

	private static readonly string Green = CColouredText.In(CColouredText.Green);
	private static readonly string Yellow = CColouredText.In(CColouredText.Yellow);
	private static readonly string Red = CColouredText.In(CColouredText.Red);
	private static readonly string White = CColouredText.In('W');
	private static readonly string Plain = CColouredText.In('!');

	/// <summary>
	/// The supply mode's part of a province's tooltip, each line starting with a newline as
	/// the original appends it; empty where it says nothing.
	/// </summary>
	public static string Text(Func<string, string> localise, CGameState state, CSupplyNetwork network,
		CProvince province, CCountryTag? player)
	{
		var text = new StringBuilder();
		if (province.IsSea)
		{
			if (state.Country(player) is { } owner)
			{
				foreach (CConvoy convoy in owner.Convoys)
				{
					if (!convoy.Path.Contains(province.Id))
					{
						continue;
					}
					text.Append('\n').Append(localise("LOGTT_8")).Append(state.Province(convoy.Start).Name)
						.Append(localise("LOGTT_9")).Append(state.Province(convoy.End).Name).Append(localise("LOGTT_10"));
					foreach (EGoods goods in Enum.GetValues<EGoods>())
					{
						if (convoy.Daily[goods] > CFixedPoint.Zero)
						{
							text.Append("\n   ").Append(CTextTemplate.Fill(localise("CONV_SHIP_IRO"),
								("NAME", localise(CAirCombatText.GoodsKeys[(int)goods])),
								("VALUE", CNumberText.Format(convoy.Daily[goods], 1))));
						}
					}
				}
			}
			return text.ToString();
		}

		if (CSupplyFigures.MostInfrastructure(state, province) < LeastInfrastructure)
		{
			return "\n" + localise("TOOLOWINFRA");
		}

		int known = CSupplyMapColours.Intelligence(province, player);
		CFixedPoint capacity = CSupplyFigures.Capacity(state, province);
		if (known > KnownSome)
		{
			text.Append('\n').Append(InfrastructureLine(localise, state, province, capacity)).Append('\n');
			int depot = network.DepotOf(province.Id);
			text.Append('\n').Append(localise("LOGTT_1")).Append(Yellow).Append(state.Province(depot).Name).Append(White);
			text.Append(" (").Append(network.DistanceOf(province.Id)).Append(' ').Append(localise("LOGTT_2")).Append(')');
		}
		if (known > KnownWell)
		{
			CGoodsPool drawn = province.SupplyDrawn;
			CGoodsPool through = province.SupplyThroughput;
			text.Append('\n')
				.Append(localise("LOGTT_3")).Append(Colour(province.Pool[EGoods.Supplies] < province.SupplyNeed[EGoods.Supplies], Red, Green))
				.Append(Figure(province.Pool[EGoods.Supplies])).Append(White)
				.Append(localise("LOGTT_4")).Append(Colour(province.Pool[EGoods.Fuel] < province.SupplyNeed[EGoods.Fuel], Red, Green))
				.Append(Figure(province.Pool[EGoods.Fuel])).Append(White);
			text.Append('\n')
				.Append(localise("LOGTT_5")).Append(Colour(drawn[EGoods.Supplies] < through[EGoods.Supplies], Red, Yellow))
				.Append(Figure(drawn[EGoods.Supplies])).Append(White)
				.Append(localise("LOGTT_6")).Append(Colour(drawn[EGoods.Fuel] < through[EGoods.Fuel], Red, Yellow))
				.Append(Figure(drawn[EGoods.Fuel])).Append(White);
			text.Append('\n')
				.Append(localise("LOGTT_7")).Append(Colour(through[EGoods.Supplies] < capacity, Yellow, Red))
				.Append(Figure(through[EGoods.Supplies])).Append(White)
				.Append(localise("LOGTT_6")).Append(Colour(through[EGoods.Fuel] < capacity, Yellow, Red))
				.Append(Figure(through[EGoods.Fuel])).Append(White);
			if (CConvoyRules.IsHarbour(state, province))
			{
				string landed = Figure(new CFixedPoint(CConvoyRules.PortCapacity(state, province)));
				text.Append('\n').Append(CTextTemplate.Fill(localise("PORT_SHIPS"), ("SIZE", landed)));
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// What the province's infrastructure lets through: its level against the most it can
	/// have, in red where it is below that and green where it is not, and what passes in a
	/// day - or, beside a capital, that it passes without limit.
	/// </summary>
	private static string InfrastructureLine(Func<string, string> localise, CGameState state, CProvince province,
		CFixedPoint capacity)
	{
		if (CSupplyFigures.IsBesideCapital(state, province))
		{
			return CTextTemplate.Fill(localise("SUPPLY_INFRA_CAPITAL_EFFECT_INFO"), ("THRU", localise("VALUE_MAX")));
		}
		var level = new CFixedPoint(CSupplyFigures.EffectiveInfrastructure(province) * LevelsPerWhole);
		var most = new CFixedPoint(CSupplyFigures.MostInfrastructure(state, province) * LevelsPerWhole);
		string infra = Colour(level < most, Red, Green) + Figure(level) + Plain;
		return CTextTemplate.Fill(localise("SUPPLY_INFRA_EFFECT_INFO"),
			("INFRA", infra), ("MAX", Figure(most)), ("THRU", CNumberText.Trimmed(capacity)));
	}

	private static string Colour(bool first, string ifTrue, string ifFalse) => first ? ifTrue : ifFalse;

	private static string Figure(CFixedPoint value) => CNumberText.Format(value, Places);
}
