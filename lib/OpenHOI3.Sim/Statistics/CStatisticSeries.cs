using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Statistics;

/// <summary>
/// The series the statistics record keeps of every country, the engine's own fixed list, and a country's day of them.
/// Their names are those BlackICE's collector wrote its files under. A value is sampled in thousandths and kept less
/// finely, by its series' step (<see cref="Step"/>). See
/// <c>docs/mechanics/statisticsrecord.md</c>, section 2.
/// </summary>
public sealed class CStatisticSeries
{
	private static readonly (string Name, int Index)[] IcShares =
	{
		("LendLease", 0), ("Consumer", 1), ("Production", 2), ("Supply", 3), ("Reinforce", 4), ("Upgrade", 5),
	};

	private static readonly (string Name, EGoods Goods)[] Goods =
	{
		("energy", EGoods.Energy), ("metal", EGoods.Metal), ("rares", EGoods.RareMaterials), ("oil", EGoods.CrudeOil),
		("supplies", EGoods.Supplies), ("fuel", EGoods.Fuel), ("money", EGoods.Money),
	};

	private const long Thousand = CFixedPoint.Scale;

	private readonly List<string> _names = new();
	private readonly IReadOnlyList<CIdeology> _ideologies;
	private readonly List<string> _groups = new();

	public CStatisticSeries(CGameState state)
	{
		// The null ideology, number 0, has no name and is nobody's party: it has no series.
		var named = new List<CIdeology>();
		foreach (CIdeology ideology in state.Ideologies)
		{
			if (ideology.Index > 0 && ideology.Name.Length > 0)
			{
				named.Add(ideology);
			}
		}
		_ideologies = named;
		_names.AddRange(new[]
		{
			"ls_TotalLeadership", "ls_Percent_Research", "ls_Percent_Espionage", "ls_Percent_Diplomacy", "ls_Percent_NCO",
			"intel_FreeSpies", "intel_DomesticSpies", "intel_TotalSpiesAbroad", "diplo_Neutrality", "diplo_EffectiveNeutrality",
			"prod__IcEff", "prod__TotalIc",
		});
		foreach ((string name, _) in IcShares)
		{
			_names.Add("prod_" + name + "_%");
			_names.Add("prod_" + name + "_IC");
		}
		foreach (CIdeology ideology in _ideologies)
		{
			_names.Add("pol_Popularity_Single_" + ideology.Name);
			if (ideology.Group is { } group && !_groups.Contains(group.Name))
			{
				_groups.Add(group.Name);
			}
		}
		foreach (string group in _groups)
		{
			_names.Add("pol_Popularity_Group_" + group);
		}
		_names.AddRange(new[]
		{
			"other_ProvincesControlled", "other_ProvincesOwned", "other_u_LandCountTotal", "other_u_AirCountTotal",
			"other_u_NavalCountTotal", "other_u_TotalDivisions", "other_ManpowerTotal",
		});
		foreach ((string name, _) in Goods)
		{
			_names.Add("res_pool_" + name);
			_names.Add("res_balance_" + name);
			_names.Add("res_tradeFor_" + name);
			_names.Add("res_tradeAway_" + name);
		}
	}

	/// <summary>What a stockpile, a balance and a trade are kept to: whole units.</summary>
	public const long GoodsStep = 1000;

	/// <summary>What every other series is kept to: hundredths.</summary>
	public const long OtherStep = 10;

	/// <summary>
	/// How many thousandths one step of a series' kept value is: the record holds the sampled value over this, rounded
	/// to the nearest, which is what makes a year of every country small enough to keep (statisticsrecord.md, section 3).
	/// </summary>
	public long Step(int index) => _names[index].StartsWith("res_", StringComparison.Ordinal) ? GoodsStep : OtherStep;

	/// <summary>A sampled value as the record keeps it: over the series' step, rounded to the nearest.</summary>
	public long Kept(int index, long thousandths)
	{
		long step = Step(index);
		return (thousandths >= 0 ? thousandths + step / 2 : thousandths - step / 2) / step;
	}

	/// <summary>The series' names, in the order a country's day holds them.</summary>
	public IReadOnlyList<string> Names => _names;

	public int Count => _names.Count;

	/// <summary>The place of a series by its name, whatever its case, or -1.</summary>
	public int IndexOf(string name) => _names.FindIndex(known => known.Equals(name, StringComparison.OrdinalIgnoreCase));

	/// <summary>A country's figures today, a value a series in thousandths.</summary>
	/// <param name="controlled">How many provinces each country controls, counted once for the day.</param>
	public void Sample(CGameState state, CCountry country, IReadOnlyDictionary<CCountryTag, int> controlled, long[] into)
	{
		int at = 0;
		void Add(long value) => into[at++] = value;

		// Leadership: the total and its four shares in percent, by the original's order of the shares.
		Add(country.TotalLeadership.Raw);
		foreach (int share in new[] { 3, 2, 1, 0 })
		{
			Add(country.LeadershipShares[share].ToThousandths().Raw * 100);
		}
		Add(country.FreeSpies.Raw);
		long home = 0;
		long abroad = 0;
		for (int other = 0; other < country.Espionage.Length; other++)
		{
			if (country.Espionage[other] is not { } presence)
			{
				continue;
			}
			if (other == country.Index)
			{
				home = presence.Level;
			}
			else
			{
				abroad += presence.Level;
			}
		}
		Add(home);
		Add(abroad);
		Add(country.Neutrality.Raw);
		Add(country.EffectiveNeutrality.Raw);

		// Industry: its efficiency in percent, its IC, and each share in percent and in IC.
		Add((country.Modifier[CBuildCosts.IndustrialEfficiency].Raw + country.Technology[CBuildCosts.TechnologyEfficiency].Raw) * 100);
		Add(country.TotalIc * Thousand);
		foreach ((_, int index) in IcShares)
		{
			long share = country.ProductionShares[index].ToThousandths().Raw;
			Add(share * 100);
			Add(share * country.TotalIc);
		}

		// Politics: each ideology's popularity, and each group's.
		long[] popularity = country.Popularity.Values;
		var byGroup = new long[_groups.Count];
		foreach (CIdeology ideology in _ideologies)
		{
			long value = ideology.Index >= 0 && ideology.Index < popularity.Length ? popularity[ideology.Index] : 0;
			Add(value);
			if (ideology.Group is { } group)
			{
				byGroup[_groups.IndexOf(group.Name)] += value;
			}
		}
		foreach (long value in byGroup)
		{
			Add(value);
		}

		Add(controlled.GetValueOrDefault(country.Tag) * Thousand);
		Add(CCountryFacts.Of(state).OwnedCount(country.Tag) * Thousand);
		long brigades = 0;
		long wings = 0;
		long ships = 0;
		long divisions = 0;
		foreach (CUnit unit in country.AllUnits())
		{
			switch (unit.Kind)
			{
				case EUnitKind.Navy:
					ships += unit.SubUnits.Count;
					break;
				case EUnitKind.Air:
					wings += unit.SubUnits.Count;
					break;
				default:
					brigades += unit.SubUnits.Count;
					divisions += unit.Kind == EUnitKind.Division ? 1 : 0;
					break;
			}
		}
		Add(brigades * Thousand);
		Add(wings * Thousand);
		Add(ships * Thousand);
		Add(divisions * Thousand);
		Add(country.Manpower.Raw);

		CGoodsPool? pool = state.Stockpile(country);
		foreach ((_, EGoods goods) in Goods)
		{
			Add(pool?[goods].Raw ?? 0);
			Add(CDailyLedger.Net(country, goods));
			Add(country.TradedForSansAlliedSupply[goods].Raw);
			Add(country.TradedAwaySansAlliedSupply[goods].Raw);
		}
	}
}
