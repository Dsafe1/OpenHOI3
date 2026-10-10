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
/// The rule was measured from a savegame of the original rather than read out of it.
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

	public static void UpdateHomeProduction(CGameState state, CCountry country)
	{
		CGoodsPool home = country.HomeProduced;
		home.Clear();

		List<int> joined = ProvincesJoinedToCapital(state, country);
		foreach (int id in joined)
		{
			AddYield(home, country, state.Provinces[id]);
		}

		// And land that is not joined to the capital but draws on a depot that is: its
		// yield comes home along the supply network as if it were.
		if (joined.Count > 0)
		{
			Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
			Supply.CSupplyArea? homeArea = network.AreaOf(country.Capital);
			foreach (CProvince province in state.Provinces)
			{
				if (Holds(country, province) && network.AreaOf(province.Id) != homeArea
					&& IsHome(network, country, province))
				{
					AddYield(home, country, province);
				}
			}
		}

		// A country that does not hold its capital produces nothing at home, not even this.
		if (joined.Count > 0)
		{
			CFixedPoint baseIc = state.StaticModifier(CIndustrialCapacity.BaseValues, CIndustrialCapacity.BaseValuesIc);
			foreach ((EGoods goods, CFixedPoint perIc) in NeededPerIc)
			{
				home[goods] += baseIc * perIc;
			}
		}
	}

	/// <summary>
	/// Whether what a province yields reaches its holder's stockpile by land: it is in
	/// the block of land the capital stands on, or it draws on a depot that is. Nothing
	/// is home for a country whose capital is in no such block.
	/// </summary>
	public static bool IsHome(Supply.CSupplyNetwork network, CCountry country, CProvince province)
	{
		if (network.AreaOf(country.Capital) is not { } home)
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
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || province.IsSea || state.Country(province.Controller) is not { } holder
				|| IsHome(network, holder, province))
			{
				continue;
			}
			AddYield(province.Pool, holder, province);

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
	private static void AddYield(CGoodsPool into, CCountry country, CProvince province)
	{
		long lawFactor = CFixedPoint.Scale + country.Modifier[GlobalResources].Raw;
		foreach ((EGoods goods, string technologyKey) in Produced)
		{
			long yield = province.CurrentProducing[goods].Raw;
			if (yield == 0)
			{
				continue;
			}
			// Province by province, each multiplication truncated in turn: what the
			// province's own modifier leaves of it first, then the country's.
			long localFactor = Math.Max(0, CFixedPoint.Scale + province.Modifier[LocalResources].Raw);
			long scaled = yield * localFactor / CFixedPoint.Scale;
			scaled = scaled * lawFactor / CFixedPoint.Scale;
			if (technologyKey.Length != 0)
			{
				long technologyFactor = CFixedPoint.Scale + country.Technology[technologyKey].Raw;
				scaled = scaled * technologyFactor / CFixedPoint.Scale;
			}
			into[goods] += new CFixedPoint(scaled);
		}
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

	/// <summary>Whether the country controls its own capital, which is what it is to exist here.</summary>
	public static bool HoldsCapital(CGameState state, CCountry country)
	{
		CProvince capital = state.Province(country.Capital);
		return capital.Id != 0 && Holds(country, capital);
	}

	/// <summary>
	/// The provinces whose production reaches the capital on its own: the capital, and
	/// every land province the country controls that can be walked to from it through
	/// land it controls. Anything else has to be shipped, and is not home production.
	/// </summary>
	public static List<int> ProvincesJoinedToCapital(CGameState state, CCountry country)
	{
		var joined = new List<int>();
		CProvince capital = state.Province(country.Capital);
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
