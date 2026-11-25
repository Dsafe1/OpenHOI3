using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// Works out what each country produces at home in a day: the resources of the
/// provinces joined to its capital, scaled by its laws and its technology, plus a small
/// amount every country gets regardless. See <c>docs/mechanics/resources.md</c>,
/// section 3. Money and supplies are home production too, and come of the country's
/// shares of IC: <see cref="CProduction"/>.
///
/// The rule was measured from a savegame of the original, and a province's yield read out of it since 2026-11-15.
/// </summary>
public static class CResources
{
	private const string GlobalResources = "global_resources";
	private const string LocalResources = "local_resources";
	private const string GlobalMoney = "global_money";

	/// <summary>The define that turns IC into money, and the section of the defines it is in.</summary>
	public const string EconomySection = "economy";
	public const string IcToMoney = "IC_TO_MONEY";

	/// <summary>The four goods a province yields, and the technology effect that scales each.</summary>
	private static readonly (EGoods Goods, string TechnologyKey)[] Produced =
	{
		(EGoods.Metal, "metal_production"),
		(EGoods.Energy, "energy_production"),
		(EGoods.RareMaterials, "rares_production"),
		(EGoods.CrudeOil, ""),
	};

	/// <summary>
	/// What a point of IC needs in a day: 1 metal, 2 energy and half a rare material.
	/// Every country also produces exactly this much for each point of IC that
	/// <c>base_values</c> gives it, whatever it holds, and no modifier touches that.
	/// </summary>
	public static readonly (EGoods Goods, CFixedPoint PerIc)[] NeededPerIc =
	{
		(EGoods.Metal, CFixedPoint.One),
		(EGoods.Energy, CFixedPoint.FromInt(2)),
		(EGoods.RareMaterials, CFixedPoint.Parse("0.5")),
	};

	/// <summary>What a harbour holds for its ship: everything but supplies and fuel.</summary>
	private static readonly EGoods[] Shipped =
	{
		EGoods.Money, EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	public static void UpdateHomeProduction(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			UpdateHomeProduction(state, country);
		}
	}

	/// <summary>
	/// A country's day's production collected: what it made at home cleared and made again from its provinces. Only a
	/// country with a day of its own has it - one owning a province, or a government in exile - as the original's
	/// <c>CCountry::CollectDailyProduction</c> runs only for those [exe]. One that does not exist keeps its pool as it
	/// stands, and the money and supplies its shares make are added to it day after day (resources.md, section 3).
	/// </summary>
	public static void UpdateHomeProduction(CGameState state, CCountry country)
	{
		if (!HasOwnDay(state, country))
		{
			return;
		}
		CGoodsPool home = country.HomeProduced;
		home.Clear();

		List<int> joined = ProvincesJoinedToCapital(state, country);
		foreach (int id in joined)
		{
			AddYield(home, country, state.Provinces[id], state.Rules.Has(Rules.EFix.YieldModifiers));
		}

		// And land that is not joined to the capital but draws on a depot that is: its
		// yield comes home along the supply network as if it were.
		if (joined.Count > 0)
		{
			Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
			Supply.CSupplyArea? homeArea = network.AreaOf(country.ActingCapital);
			// The provinces it holds, by the tag's letters as Holds matches them, in the map's order (CHeldProvinces.Walk).
			foreach (CProvince province in Countries.CHeldProvinces.Walk(state, country.Tag, ignoringCase: true))
			{
				if (Holds(country, province) && network.AreaOf(province.Id) != homeArea
					&& IsHome(network, country, province))
				{
					AddYield(home, country, province, state.Rules.Has(Rules.EFix.YieldModifiers));
				}
			}
		}

		// What base_values' IC needs is made by every country that has a day of its own - one owning a province, or a
		// government in exile - whatever it holds: an exile seated in its host's capital makes it into its own pool
		// (war.md, section 14).
		CFixedPoint baseIc = state.StaticModifier(CIndustrialCapacity.BaseValues, CIndustrialCapacity.BaseValuesIc);
		foreach ((EGoods goods, CFixedPoint perIc) in NeededPerIc)
		{
			home[goods] += baseIc * perIc;
		}
	}

	/// <summary>
	/// Whether what a province yields reaches its holder's stockpile by land: it is in
	/// the block of land the capital stands on, or it draws on a depot that is. Nothing
	/// is home for a country whose capital is in no such block.
	/// </summary>
	public static bool IsHome(Supply.CSupplyNetwork network, CCountry country, CProvince province)
	{
		if (network.AreaOf(country.ActingCapital) is not { } home)
		{
			return false;
		}
		if (network.AreaOf(province.Id) == home)
		{
			return true;
		}
		int depot = network.DepotOf(province.Id);
		return depot > 0 && network.AreaOf(depot) == home;
	}

	/// <summary>
	/// What land cut off from its holder's capital yielded today: it is put down in the
	/// province, and then everything the province holds of money and the four resources
	/// is moved to the harbour the block's ship loads in - where the block has one.
	///
	/// Everything: a province's pool is also the stockpile of a country whose capital
	/// it is, so the goods of a country that does not exist go to the harbour with the
	/// day's yield and are shipped to whoever holds its capital. The original does just
	/// that. See <c>docs/mechanics/convoys.md</c>, section 8.
	/// </summary>
	public static void GatherCutOff(CGameState state)
	{
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
		List<CProvince>[] held = LandByHolder(state);
		foreach (CCountry holder in state.Countries)
		{
			GatherCutOff(state, network, holder, held[holder.Index]);
		}
	}

	/// <summary>The land provinces each country holds, in the map's order, by the country's index.</summary>
	public static List<CProvince>[] LandByHolder(CGameState state)
	{
		var held = new List<CProvince>[state.Countries.Count];
		for (int index = 0; index < held.Length; index++)
		{
			held[index] = new List<CProvince>();
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id != 0 && !province.IsSea && state.Country(province.Controller) is { } holder)
			{
				held[holder.Index].Add(province);
			}
		}
		return held;
	}

	/// <summary>One holder's cut-off land gathered, in the holder's own day, over the land it holds in the map's order.</summary>
	public static void GatherCutOff(CGameState state, Supply.CSupplyNetwork network, CCountry holder, List<CProvince> held)
	{
		foreach (CProvince province in held)
		{
			if (IsHome(network, holder, province))
			{
				continue;
			}
			AddYield(province.Pool, holder, province, state.Rules.Has(Rules.EFix.YieldModifiers));

			if (network.AreaOf(province.Id) is not { } area)
			{
				continue;
			}
			int port = Convoys.CResourceConvoys.LoadingPort(network, holder, area);
			if (port == 0 || port == province.Id)
			{
				continue;
			}
			CGoodsPool waiting = state.Province(port).Pool;
			foreach (EGoods goods in Shipped)
			{
				waiting[goods] += province.Pool[goods];
				province.Pool[goods] = CFixedPoint.Zero;
			}
		}
	}

	/// <summary>
	/// Adds what a province yields in a day to a pool, good by good, as its holder gets
	/// it: scaled by the province's own modifier, the country's laws and its technology.
	/// </summary>
	private static void AddYield(CGoodsPool into, CCountry country, CProvince province, bool mended)
	{
		foreach ((EGoods goods, string _) in Produced)
		{
			if (province.CurrentProducing[goods].Raw != 0)
			{
				into[goods] += Yield(country, province, goods, mended);
			}
		}
	}

	/// <summary>
	/// What a province yields of a good in a day as its holder gets it, as the original's
	/// <c>CMapProvince::ResourceYield</c> works it [exe]: what it yields now, times one plus the holder's technology for
	/// the good, truncated; then times one plus the <b>sum</b> of four modifiers - the province's of the good
	/// (<c>local_metal</c>), the holder's of the good (<c>global_metal</c>), the province's <c>local_resources</c> and
	/// the holder's <c>global_resources</c> - that sum never below nothing, truncated. The modifiers add, so a province
	/// at <c>non_core</c>'s and a policy's -0.5 each still yields the laws' share (resources.md, section 3). Also the
	/// province view's figure (<c>provinceview.md</c>, section 2).
	/// </summary>
	/// <param name="mended">An enhanced game's fix: fuel by the holder's <c>global_fuel</c> and money by its <c>global_money</c>.</param>
	public static CFixedPoint Yield(CCountry country, CProvince province, EGoods goods, bool mended = false)
	{
		long yield = province.CurrentProducing[goods].Raw;
		if (TechnologyKey(goods) is { Length: > 0 } technologyKey)
		{
			yield = yield * (CFixedPoint.Scale + country.Technology[technologyKey].Raw) / CFixedPoint.Scale;
		}
		long factor = CFixedPoint.Scale + province.Modifier[LocalKey(goods)].Raw + country.Modifier[GlobalKey(goods, mended)].Raw
			+ province.Modifier[LocalResources].Raw + country.Modifier[GlobalResources].Raw;
		return new CFixedPoint(yield * Math.Max(0, factor) / CFixedPoint.Scale);
	}

	/// <summary>The province's modifier of one good, as the original's table of them names it.</summary>
	private static string LocalKey(EGoods goods) => goods switch
	{
		EGoods.Supplies => "local_supplies",
		EGoods.Fuel => "local_fuel",
		EGoods.Money => "local_money",
		EGoods.CrudeOil => "local_crude_oil",
		EGoods.Metal => "local_metal",
		EGoods.Energy => "local_energy",
		_ => "local_rare_materials",
	};

	/// <summary>
	/// The holder's modifier of one good, as the original's table of them names it - which has fuel's and money's the
	/// wrong way round (docs/enhanced.md); no province yields either.
	/// </summary>
	private static string GlobalKey(EGoods goods, bool mended) => goods switch
	{
		EGoods.Supplies => "global_supplies",
		EGoods.Fuel => mended ? "global_fuel" : "global_money",
		EGoods.Money => mended ? "global_money" : "global_fuel",
		EGoods.CrudeOil => "global_crude_oil",
		EGoods.Metal => "global_metal",
		EGoods.Energy => "global_energy",
		_ => "global_rare_materials",
	};

	/// <summary>The technology effect that scales a good a province yields, or nothing.</summary>
	public static string TechnologyKey(EGoods goods)
	{
		foreach ((EGoods produced, string key) in Produced)
		{
			if (produced == goods)
			{
				return key;
			}
		}
		return "";
	}

	/// <summary>
	/// The money a country makes in a day: its base IC, at the rate the defines give,
	/// scaled by its laws and ministers. Not its total IC, and nothing to do with how much
	/// of it goes to consumer goods. See <c>docs/mechanics/stockpile.md</c>, section 4.
	/// </summary>
	public static CFixedPoint MoneyPerDay(CGameState state, CCountry country)
	{
		long money = country.BaseIc * CFixedPoint.Scale;
		money = money * state.Defines[EconomySection, IcToMoney].Raw / CFixedPoint.Scale;
		money = money * (CFixedPoint.Scale + country.Modifier[GlobalMoney].Raw) / CFixedPoint.Scale;
		return new CFixedPoint(money);
	}

	/// <summary>
	/// Whether the country has a day of its own, in which its production is collected, its IC worked out again and its
	/// conversions run: it owns a province, or it is a government in exile (war.md, section 14).
	/// </summary>
	public static bool HasOwnDay(CGameState state, CCountry country) =>
		country.InExile || CCountryLists.OwnedCount(state, country.Tag) > 0;

	/// <summary>Whether the country holds the province its government sits in, its acting capital.</summary>
	public static bool HoldsCapital(CGameState state, CCountry country)
	{
		CProvince capital = state.Province(country.ActingCapital);
		return capital.Id != 0 && Holds(country, capital);
	}

	/// <summary>
	/// The provinces whose production reaches the capital on its own: the acting capital,
	/// and every land province the country controls that can be walked to from it through
	/// land it controls. Anything else has to be shipped, and is not home production.
	/// </summary>
	public static List<int> ProvincesJoinedToCapital(CGameState state, CCountry country)
	{
		var joined = new List<int>();
		CProvince capital = state.Province(country.ActingCapital);
		if (!HoldsCapital(state, country))
		{
			return joined;
		}

		joined = Supply.CSupplyNetworks.AreaAround(state, capital.Id);
		joined.Sort();                                       // so the order of the sum does not depend on the walk
		return joined;
	}

	private static bool Holds(CCountry country, CProvince province)
	{
		return !province.IsSea && province.Controller is { } controller
			&& controller.Value.Equals(country.Tag.Value, StringComparison.OrdinalIgnoreCase);
	}
}
