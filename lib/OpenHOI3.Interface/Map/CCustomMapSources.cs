using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The sources a custom map mode may shade by, kind by kind in the order their lists show them. The buildings are the
/// game's own; the resources and the supply figures are the engine's. See <c>docs/mechanics/custommapmodes.md</c>,
/// section 3.
/// </summary>
public static class CCustomMapSources
{
	/// <summary>What every text key of the feature begins with.</summary>
	public const string TextPrefix = "OPENHOI_MAPMODE_";

	private const long Scale = CFixedPoint.Scale;

	/// <summary>A building's level above which, and a resource's amount above which, a figure is taken for no figure.</summary>
	private const long MostLevel = 100 * Scale;
	private const long MostAmount = 1000 * Scale;

	/// <summary>The most days a stock is said to last, and the most a line is said to be loaded.</summary>
	private const long MostDays = 365 * Scale;
	private const long MostLoad = 10 * Scale;

	private static readonly long[] DayBands = { 0, 1000, 2000, 3000, 5000, 7000, 10000, 15000, 21000, 30000 };
	private static readonly long[] DistanceBands = { 0, 1, 2, 4, 7, 12, 20, 35, 60, CSupplyNetwork.Unreached };
	private static readonly long[] DetourBands = { 0, 1, 2, 3, 5, 8, 12, 18, 26, 40 };
	private static readonly long[] LoadBands = { 0, 100, 200, 300, 400, 500, 600, 700, 850, 1000 };
	private static readonly long[] TaxBands = { 0, 25, 40, 50, 60, 75, 100, 125, 150, 190 };

	/// <summary>The sources of a kind, in their list's order.</summary>
	public static List<CCustomMapSource> Of(CGameState state, ECustomMapKind kind)
	{
		return kind switch
		{
			ECustomMapKind.Buildings => Buildings(state),
			ECustomMapKind.Resources => Resources(),
			_ => Supply(),
		};
	}

	/// <summary>The source of a kind by its key, whatever its case, or none.</summary>
	public static CCustomMapSource? Find(CGameState state, ECustomMapKind kind, string key)
	{
		return Of(state, kind).Find(source => source.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Every building the game defines, in its file's order: the level that works in the province - what bombing left of
	/// it, not what was built - cut to whole levels. Nothing to show at none, or above a hundred.
	/// </summary>
	private static List<CCustomMapSource> Buildings(CGameState state)
	{
		var sources = new List<CCustomMapSource>();
		foreach (CBuilding building in state.Buildings)
		{
			CBuilding own = building;
			sources.Add(new CCustomMapSource
			{
				Kind = ECustomMapKind.Buildings,
				Key = building.Key,
				Label = building.Key,
				Scale = ECustomMapScale.Level,
				Unit = ECustomMapUnit.Level,
				Value = (_, province) =>
				{
					long level = province.BuildingLevel(own).Raw;
					return level < Scale || level > MostLevel ? null : level / Scale;
				},
			});
		}
		return sources;
	}

	/// <summary>What a province yields of each of four goods today, and its manpower and leadership.</summary>
	private static List<CCustomMapSource> Resources()
	{
		static CCustomMapSource Resource(string key, Func<CProvince, long> read) => new()
		{
			Kind = ECustomMapKind.Resources,
			Key = key,
			Label = TextPrefix + key.ToUpperInvariant(),
			Scale = ECustomMapScale.Map,
			Value = (_, province) =>
			{
				long amount = read(province);
				return amount <= 0 || amount > MostAmount ? null : amount;
			},
		};
		return new List<CCustomMapSource>
		{
			Resource("energy", province => province.CurrentProducing[EGoods.Energy].Raw),
			Resource("metal", province => province.CurrentProducing[EGoods.Metal].Raw),
			Resource("rare_materials", province => province.CurrentProducing[EGoods.RareMaterials].Raw),
			Resource("crude_oil", province => province.CurrentProducing[EGoods.CrudeOil].Raw),
			Resource("manpower", province => province.Manpower.Raw),
			Resource("leadership", province => province.Leadership.Raw),
		};
	}

	/// <summary>The eleven supply figures, each of them the day's supply pass's own or made of them.</summary>
	private static List<CCustomMapSource> Supply()
	{
		static CCustomMapSource Figure(string key, ECustomMapScale scale, ECustomMapUnit unit, long[]? bands, bool worse,
			Func<CGameState, CProvince, long?> value) => new()
		{
			Kind = ECustomMapKind.Supply,
			Key = key,
			Label = TextPrefix + key.ToUpperInvariant(),
			Explanation = TextPrefix + key.ToUpperInvariant() + "_DESC",
			Scale = scale,
			Unit = unit,
			Bands = bands ?? Array.Empty<long>(),
			HigherIsWorse = worse,
			HiddenUnseen = true,
			Value = value,
		};
		// What lies there over what the units drawing on it need a day, held at a year: nothing where they need nothing.
		static long? Days(CProvince province, EGoods goods)
		{
			long need = province.SupplyNeed[goods].Raw;
			return need <= 0 ? null : Math.Min(Sane(province.Pool[goods].Raw) * Scale / need, MostDays);
		}
		// What was asked of it today over what it can pass on: nothing where nothing was asked, full where it can pass nothing.
		static long? Load(CGameState state, CProvince province, EGoods goods)
		{
			long asked = Sane(province.SupplyDrawn[goods].Raw);
			if (asked <= 0)
			{
				return null;
			}
			long capacity = CSupplyFigures.Capacity(state, province).Raw;
			return capacity <= 0 ? MostLoad : Math.Min(asked * Scale / capacity, MostLoad);
		}
		static long? Positive(long amount) => Sane(amount) <= 0 ? null : amount;
		static CSupplyNetwork Network(CGameState state) => CSupplyNetworks.Refresh(state);

		return new List<CCustomMapSource>
		{
			Figure("days_of_supplies", ECustomMapScale.Bands, ECustomMapUnit.Days, DayBands, false, (_, province) => Days(province, EGoods.Supplies)),
			Figure("days_of_fuel", ECustomMapScale.Bands, ECustomMapUnit.Days, DayBands, false, (_, province) => Days(province, EGoods.Fuel)),
			Figure("depot_distance", ECustomMapScale.Bands, ECustomMapUnit.Cost, DistanceBands, true,
				(state, province) => Network(state).DepotOf(province.Id) == 0 ? null : Network(state).DistanceOf(province.Id)),
			Figure("depot_detour", ECustomMapScale.Bands, ECustomMapUnit.Cost, DetourBands, true, (state, province) =>
			{
				CSupplyNetwork network = Network(state);
				int distance = network.DistanceOf(province.Id);
				return network.DepotOf(province.Id) == 0 || distance >= CSupplyNetwork.Unreached
					? null
					: Math.Max(0, distance - network.HopsOf(province.Id));
			}),
			Figure("supply_load", ECustomMapScale.Bands, ECustomMapUnit.Percent, LoadBands, true, (state, province) => Load(state, province, EGoods.Supplies)),
			Figure("fuel_load", ECustomMapScale.Bands, ECustomMapUnit.Percent, LoadBands, true, (state, province) => Load(state, province, EGoods.Fuel)),
			Figure("supply_tax", ECustomMapScale.Bands, ECustomMapUnit.Amount, TaxBands, true, (state, province) =>
			{
				if (Network(state).DepotOf(province.Id) == 0)
				{
					return null;
				}
				long tax = CSupplyFigures.Loss(state, province).Raw;
				return tax < 0 ? null : tax;
			}),
			Figure("supply_traffic", ECustomMapScale.Map, ECustomMapUnit.Amount, null, false, (_, province) => Positive(province.SupplyThroughput[EGoods.Supplies].Raw)),
			Figure("fuel_traffic", ECustomMapScale.Map, ECustomMapUnit.Amount, null, false, (_, province) => Positive(province.SupplyThroughput[EGoods.Fuel].Raw)),
			Figure("supply_stock", ECustomMapScale.Map, ECustomMapUnit.Amount, null, false, (_, province) => Positive(province.Pool[EGoods.Supplies].Raw)),
			Figure("fuel_stock", ECustomMapScale.Map, ECustomMapUnit.Amount, null, false, (_, province) => Positive(province.Pool[EGoods.Fuel].Raw)),
		};
	}

	/// <summary>An amount below nought or above a million counts as nought.</summary>
	private static long Sane(long amount) => amount < 0 || amount > 1_000_000 * Scale ? 0 : amount;
}
