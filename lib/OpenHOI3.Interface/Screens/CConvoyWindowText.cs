using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the convoy windows say and allow: the production screen's list, <c>convoy_settings</c>, <c>convoy_creation_window</c> and <c>convoy_details</c>. See
/// <c>docs/mechanics/convoywindows.md</c>.
/// </summary>
public static class CConvoyWindowText
{
	public const string SettingsName = "convoy_settings";
	public const string CreationName = "convoy_creation_window";
	public const string DetailsName = "convoy_details";
	public const string RowName = "convoy_entry";
	public const string PortRowName = "convoy_province_entry";

	/// <summary>Ctrl's step on the two counts.</summary>
	public const int Bundle = 10;

	private const string SameAreaCutoff = "SUPPLY_SAME_AREA_DIST_CUTOFF";
	private const string MilitarySection = "military";

	/// <summary>The six goods a window names, each with its box and its figure's box.</summary>
	public static readonly IReadOnlyList<(EGoods Goods, string Box, string Value)> Goods = new[]
	{
		(EGoods.Fuel, "ship_fuel", "fuel_value"), (EGoods.Supplies, "ship_supplies", "supplies_value"), (EGoods.CrudeOil, "ship_oil", "oil_value"),
		(EGoods.RareMaterials, "ship_raremat", "raremat_value"), (EGoods.Metal, "ship_metal", "metal_value"), (EGoods.Energy, "ship_energy", "energy_value"),
	};

	/// <summary>The icons of a row, by the good each shows.</summary>
	public static readonly IReadOnlyList<(EGoods Goods, string Icon)> RowIcons = new[]
	{
		(EGoods.Supplies, "icon_supplies_mini"), (EGoods.Fuel, "icon_fuel_mini"), (EGoods.CrudeOil, "icon_oil_mini"),
		(EGoods.Metal, "icon_metal_mini"), (EGoods.Energy, "icon_energy_mini"), (EGoods.RareMaterials, "icon_raremat_mini"),
	};

	/// <summary>The list: the country's own convoys in the order they were made, then the first lend-lease convoy of each country it gives to.</summary>
	public static List<CConvoy> Rows(CGameState state, CCountry country)
	{
		var rows = new List<CConvoy>(country.Convoys);
		foreach (CCountry other in state.Countries.Where(other => other != country))
		{
			if (other.Convoys.FirstOrDefault(convoy => convoy.IsLendLease && convoy.LendLeaseFrom == country.Tag) is { } convoy)
			{
				rows.Add(convoy);
			}
		}
		return rows;
	}

	private static bool IsHealthy(CGameState state, CSupplyNetwork network, CConvoy convoy) =>
		convoy.Path.Count >= 2 && convoy.Transports != 0 && CConvoyRules.IsRouteUsable(state, network, convoy);

	/// <summary>Red where it cannot sail; otherwise green at full strength, and yellow under it.</summary>
	public static string Colour(CGameState state, CConvoy convoy)
	{
		if (!IsHealthy(state, CSupplyNetworks.Refresh(state), convoy))
		{
			return "§R";
		}
		return CConvoyRules.Efficiency(state, convoy) >= CFixedPoint.Scale ? "§G" : "§Y";
	}

	public static string RowTitle(CGameState state, CConvoy convoy) => Colour(state, convoy) + state.Province(convoy.Start).Name + " - " + state.Province(convoy.End).Name;

	/// <summary>Its ships over those it would have; the giver of a lend-lease convoy sees how well it runs.</summary>
	public static string RowCount(CGameState state, CConvoy convoy, CCountryTag viewer)
	{
		string colour = Colour(state, convoy);
		if (convoy.IsLendLease && convoy.LendLeaseFrom == viewer)
		{
			return colour + CNumberText.Format(new CFixedPoint(CConvoyRules.Efficiency(state, convoy) * 100), 1) + "%§!";
		}
		return colour + convoy.Transports.ToString(CultureInfo.InvariantCulture) + "/" + CConvoyRules.DesiredTransports(state, convoy).ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>A lend-lease convoy's line, "IC to" the receiver where the viewer gives and "IC from" the giver otherwise; null for any other.</summary>
	public static string? LendLeaseLine(Func<string, string> localise, CGameState state, CConvoy convoy, CCountryTag viewer)
	{
		if (!convoy.IsLendLease || convoy.LendLeaseFrom is not { } giver)
		{
			return null;
		}
		return giver == viewer
			? CTextTemplate.Fill(localise("LEND_LEASE_CONVOYED_TO"), ("COUNTRY", "@" + convoy.Owner.Value))
			: CTextTemplate.Fill(localise("LEND_LEASE_CONVOYED_FROM"), ("COUNTRY", "@" + giver.Value));
	}

	/// <summary>Only a lend-lease convoy of somebody else's may not be opened.</summary>
	public static bool MayOpen(CConvoy convoy, CCountryTag viewer) => !convoy.IsLendLease || convoy.Owner == viewer;

	/// <summary>What a row says when the mouse is on it.</summary>
	public static CTooltip Tooltip(Func<string, string> localise, CGameState state, CConvoy convoy, CCountryTag viewer)
	{
		long efficiency = CConvoyRules.Efficiency(state, convoy);
		if (convoy.IsLendLease && convoy.LendLeaseFrom is { } giver)
		{
			string name = state.Country(giver)?.Name ?? giver.Value;
			string receiver = state.Country(convoy.Owner)?.Name ?? convoy.Owner.Value;
			var lines = new StringBuilder(CTextTemplate.Fill(localise("LEND_LEASE_CONVOY_DESC"), ("FROM", name), ("TO", receiver)));
			lines.Append('\n').Append(CTextTemplate.Fill(localise("LL_CONVOY_EFF_DESC"), ("PERC", CNumberText.Format(new CFixedPoint(efficiency * 100), 2))));
			return new CTooltip
			{
				Text = lines.ToString(),
				Delayed = convoy.Owner != viewer ? CTextTemplate.Fill(localise("LEND_LEASE_CONVOY_OWNER_DESC"), ("COUNTRY", receiver)) : "",
			};
		}
		var ships = new List<string>();
		foreach (EGoods goods in Enum.GetValues<EGoods>())
		{
			if (convoy.Daily[goods].Raw > 0)
			{
				ships.Add(CTextTemplate.Fill(localise("CONV_SHIP_IRO"), ("VALUE", CNumberText.Format(convoy.Daily[goods], 1)), ("NAME", localise(CTradeWindowText.GoodsName(goods)))));
			}
		}
		string carried = ships.Count > 0 ? string.Join(" ", ships) : localise("NO_CONVOY_SHIP");
		return new CTooltip { Text = carried + " " + CTextTemplate.Fill(localise("CONV_EFF_IRO"), ("EFF", CNumberText.Format(new CFixedPoint(efficiency * 100), 1))) };
	}

	private static IEnumerable<CProvince> Ports(CGameState state, CCountry country) =>
		state.Provinces.Where(province => province.Controller == country.Tag && CConvoyRules.IsHarbour(state, province));

	private static bool InCapitalArea(CSupplyNetwork network, CCountry country, CProvince province) =>
		ReferenceEquals(network.AreaOf(province.Id), network.AreaOf(country.ActingCapital));

	private static bool AreaHasConvoyStarting(CGameState state, CSupplyNetwork network, CCountry country, CProvince province)
	{
		CSupplyArea? area = network.AreaOf(province.Id);
		return country.Convoys.Any(convoy => ReferenceEquals(network.AreaOf(convoy.Start), area));
	}

	private static bool HasAnyConvoy(CGameState state, CProvince province) =>
		state.Countries.Any(country => country.Convoys.Any(convoy => convoy.Path.Contains(province.Id)));

	/// <summary>Whether the two create buttons may be pressed: a port that could start one, and the automatic flag for that kind off.</summary>
	public static (bool Resource, bool Supply) MayCreate(CGameState state, CCountry country)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool resource = false;
		bool supply = false;
		foreach (CProvince port in Ports(state, country))
		{
			resource |= !InCapitalArea(network, country, port) && !AreaHasConvoyStarting(state, network, country, port);
			supply |= !HasAnyConvoy(state, port);
		}
		return (resource && !country.AutoResourceConvoys, supply && !country.AutoSupplyConvoys);
	}

	/// <summary>
	/// The ports a convoy may be given at this step. For a supply convoy the origin is in the capital's block and the destination out of it, or within it but far from its depot; for a resource convoy the other
	/// way about, and a block with one starting already offers no origin. Neither offers the origin chosen; a supply convoy, no port where a supply convoy already ends.
	/// </summary>
	public static List<CProvince> Candidates(CGameState state, CCountry country, bool supplies, bool pickingOrigin, int origin)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		long cutoff = state.Defines[MilitarySection, SameAreaCutoff].Raw / CFixedPoint.Scale;
		var ports = new List<CProvince>();
		foreach (CProvince port in Ports(state, country).Where(port => port.Id != origin))
		{
			bool capital = InCapitalArea(network, country, port);
			bool passes;
			if (supplies)
			{
				passes = pickingOrigin ? capital : !capital || network.HopsOf(port.Id) > cutoff;
				passes &= !EndsSupplyConvoy(state, port);
			}
			else
			{
				passes = pickingOrigin ? !capital && !AreaHasConvoyStarting(state, network, country, port) : capital;
			}
			if (passes)
			{
				ports.Add(port);
			}
		}
		return ports;
	}

	private static bool EndsSupplyConvoy(CGameState state, CProvince port) =>
		state.Countries.Any(country => country.Convoys.Any(convoy => convoy.End == port.Id && !convoy.IsLendLease && !convoy.IsTrade
			&& (convoy.Carries[(int)EGoods.Supplies] || convoy.Carries[(int)EGoods.Fuel])));

	public static string Running(Func<string, string> localise, CGameState state, CCountry country)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int active = country.Convoys.Count(convoy => IsHealthy(state, network, convoy));
		return CTextTemplate.Fill(localise("CONVOYS_RUNNING"), ("ACTIVE", active.ToString(CultureInfo.InvariantCulture)), ("TOTAL", country.Convoys.Count.ToString(CultureInfo.InvariantCulture)));
	}

	public static string Available(Func<string, string> localise, CCountry country) =>
		CTextTemplate.Fill(localise("CONVOYS_AVAIL"), ("TRA", country.Transports.ToString(CultureInfo.InvariantCulture)), ("ESC", country.Escorts.ToString(CultureInfo.InvariantCulture)));

	public static string TransportsValue(CGameState state, CConvoy convoy) =>
		convoy.Transports.ToString(CultureInfo.InvariantCulture) + "/" + CConvoyRules.DesiredTransports(state, convoy).ToString(CultureInfo.InvariantCulture);

	/// <summary>How many ships a press adds, held by what is wanted and what is free; nought does nothing.</summary>
	public static int TransportsPlus(int step, int desired, int transports, int free) => Math.Min(step, Math.Min(desired - transports, free));

	public static int EscortsPlus(int step, int free) => Math.Min(step, free);

	/// <summary>How many a press takes off, as a negative: no more than there are.</summary>
	public static int Minus(int step, int held) => Math.Max(-step, -held);

	/// <summary>A convoy's goods may be changed, and it may be cancelled, unless it is lend-lease or trade.</summary>
	public static bool MayChange(CConvoy convoy) => !convoy.IsLendLease && !convoy.IsTrade;
}
