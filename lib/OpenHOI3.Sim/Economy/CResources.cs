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

		long lawFactor = CFixedPoint.Scale + country.Modifier[GlobalResources].Raw;
		List<int> joined = ProvincesJoinedToCapital(state, country);
		foreach (int id in joined)
		{
			CProvince province = state.Provinces[id];
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
				home[goods] += new CFixedPoint(scaled);
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

		var seen = new HashSet<int> { capital.Id };
		var frontier = new Queue<int>();
		frontier.Enqueue(capital.Id);
		while (frontier.Count > 0)
		{
			int id = frontier.Dequeue();
			joined.Add(id);
			foreach (int neighbour in state.Provinces[id].Neighbours)
			{
				if (seen.Add(neighbour) && Holds(country, state.Provinces[neighbour]))
				{
					frontier.Enqueue(neighbour);
				}
			}
		}
		joined.Sort();                                       // so the order of the sum does not depend on the walk
		return joined;
	}

	private static bool Holds(CCountry country, CProvince province)
	{
		return !province.IsSea && province.Controller is { } controller
			&& controller.Value.Equals(country.Tag.Value, StringComparison.OrdinalIgnoreCase);
	}
}
