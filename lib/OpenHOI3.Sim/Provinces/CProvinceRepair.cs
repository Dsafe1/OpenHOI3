using System;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// What comes back to a land province each day: its mines and wells towards what they
/// could yield, and its damaged buildings towards what was built - both faster on good
/// roads, and faster for a holder with the technology. See
/// <c>docs/mechanics/bombing.md</c>, section 11.
/// </summary>
public static class CProvinceRepair
{
	public const string EconomySection = "economy";
	public const string RepairSpeed = "BUILDING_REPAIR_SPEED";
	public const string RepairRate = "repair_rate";

	public const string Infrastructure = "infrastructure";
	public const string LocalInfrastructure = "local_infrastructure";
	public const string GlobalInfrastructure = "global_infrastructure";

	/// <summary>The least and the most the roads count for, in thousandths.</summary>
	private const long LeastRoads = 10;
	private const long MostRoads = 1000;

	/// <summary>What a day brings back whatever the roads, in thousandths.</summary>
	private const long BaseRate = 100;

	/// <summary>Every province's repair alone, each made again at once where a building crossed a whole level.</summary>
	public static void RunDaily(CGameState state)
	{
		long speed = Speed(state);
		foreach (CProvince province in state.Provinces)
		{
			if (Repair(state, province, speed))
			{
				Modifiers.CProvinceModifiers.RebuildOne(state, province);
			}
		}
	}

	public static long Speed(CGameState state)
	{
		return state.Defines.Has(EconomySection, RepairSpeed) ? state.Defines[EconomySection, RepairSpeed].Raw : 0;
	}

	/// <summary>
	/// One land province's repair. Answers whether a building was carried across a whole level, which makes its modifier
	/// again in its day; any other mend waits for the hour's look (<c>docs/mechanics/modifiers.md</c>, section 3).
	/// </summary>
	public static bool Repair(CGameState state, CProvince province, long speed)
	{
		if (province.Id == 0 || province.IsSea)
		{
			return false;
		}
		long rate = Rate(state, province);
		for (int index = 0; index < CGoodsPool.Count; index++)
		{
			var goods = (EGoods)index;
			long made = province.CurrentProducing[goods].Raw;
			long most = province.MaxProducing[goods].Raw;
			if (made != most)
			{
				province.CurrentProducing[goods] = new CFixedPoint(Math.Min(most, made + rate));
			}
		}

		long mend = speed * rate / CFixedPoint.Scale;
		bool wholeLevel = false;
		foreach (CBuilding building in state.Buildings)
		{
			CFixedPoint current = province.BuildingLevel(building);
			CFixedPoint most = province.BuildingMaxLevel(building);
			if (building.IsRepaired && current != most)
			{
				province.SetBuildingCurrentLevel(building, new CFixedPoint(current.Raw + mend));
				wholeLevel |= province.BuildingLevel(building).Raw / CFixedPoint.Scale != current.Raw / CFixedPoint.Scale;
			}
		}
		return wholeLevel;
	}

	/// <summary>
	/// What a day brings back to a province, in thousandths: a tenth and its roads - the
	/// infrastructure it has, bettered by what is said of roads there and in its
	/// country - times one and its holder's <c>repair_rate</c>.
	/// </summary>
	public static long Rate(CGameState state, CProvince province)
	{
		long roads = province.Modifier[Infrastructure].Raw
			* (CFixedPoint.Scale + province.Modifier[LocalInfrastructure].Raw + province.Modifier[GlobalInfrastructure].Raw)
			/ CFixedPoint.Scale;
		roads = Math.Clamp(roads, LeastRoads, MostRoads);
		long technology = state.Country(province.Controller) is { } holder ? holder.Technology[RepairRate].Raw : 0;
		return (BaseRate + roads) * (CFixedPoint.Scale + technology) / CFixedPoint.Scale;
	}
}
