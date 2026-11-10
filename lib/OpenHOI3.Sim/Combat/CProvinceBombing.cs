using System.Collections.Generic;
using System;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What a wing's bombs do to the province, each hour of any bombing: whole runs of its
/// strategic attack, never fewer than one, each a die's worth off the buildings, the
/// stockpile and the day's production that its unit's mission names. See
/// <c>docs/mechanics/bombing.md</c>, section 7.
/// </summary>
public static class CProvinceBombing
{
	public const string ResourcesInfraFraction = "BOMB_STRATEGIC_RESOURCES_INFRA_FRACTION";
	public const string Resources = "BOMB_STRATEGIC_RESOURCES";
	public const string Production = "BOMB_STRATEGIC_PRODUCTION";
	public const string Buildings = "BOMB_STRATEGIC_BUILDINGS";

	/// <summary>The days of the province's need its stockpile is set against.</summary>
	private const long DaysOfNeed = 7;

	/// <summary>The most a full stockpile adds to what is destroyed of it, in thousandths.</summary>
	private const long MostFullness = 1200;

	/// <summary>What the roads' most is taken for where there is no such building, in thousandths of a level.</summary>
	private const long RoadsWithoutBuilding = 10000;

	/// <summary>Nothing lying in a province, and nothing it makes, is held above this.</summary>
	private const long MostHeld = 99999000;

	/// <summary>
	/// What the units drawing on a province ask for in a day, worked out the first time
	/// a wing's bombs need it and kept for the hour.
	/// </summary>
	public sealed class CNeed
	{
		private readonly CGameState _state;
		private readonly int _province;
		private bool _isCounted;
		private long _supplies;
		private long _fuel;

		public CNeed(CGameState state, int province)
		{
			_state = state;
			_province = province;
		}

		public long Supplies
		{
			get
			{
				Count();
				return _supplies;
			}
		}

		public long Fuel
		{
			get
			{
				Count();
				return _fuel;
			}
		}

		private void Count()
		{
			if (!_isCounted)
			{
				CSupplyNeed.Of(_state, _province, _state.IsBeingMade, out _supplies, out _fuel);
				_isCounted = true;
			}
		}
	}

	public static void Bomb(CGameState state, CCombat bombing, CCombatSide side, CUnit unit, CSubUnit wing, CNeed need)
	{
		state.CombatTrace?.Invoke($"BRUN wing={wing.Id} draws={state.Random.Draws}");
		CProvince province = state.Province(bombing.Province);
		CDefines defines = state.Defines;
		string military = CManpower.MilitarySection;
		EUnitOrder order = unit.Order;

		long worth = wing.Stats[CBombingOrders.StrategicAttack].Raw
			* (CFixedPoint.Scale + CCommandReach.Effect(state, unit, ETraitEffect.StrategicAttack)) / CFixedPoint.Scale;
		worth = worth * wing.CombatAttack.Raw / CFixedPoint.Scale;
		long runs = Math.Max(worth, CFixedPoint.Scale) / CFixedPoint.Scale;

		long roads = 0;
		long mostRoads = RoadsWithoutBuilding;
		if (CBombingOrders.BuildingOf(state, CBombingOrders.Infrastructure) is { } infra)
		{
			roads = province.BuildingLevel(infra).Raw;
			mostRoads = infra.MaxLevel * (long)CFixedPoint.Scale;
		}
		long lying = (mostRoads - roads) * defines[military, ResourcesInfraFraction].Raw / CFixedPoint.Scale
			* defines[military, Resources].Raw / CFixedPoint.Scale;

		for (; runs > 0; runs--)
		{
			long die = (state.Random.Next(3) + 1) * (long)CFixedPoint.Scale;
			long fromStockpile = lying * die / CFixedPoint.Scale;
			long fromProduction = defines[military, Production].Raw * die / CFixedPoint.Scale;
			long fromBuildings = defines[military, Buildings].Raw * die / CFixedPoint.Scale;

			for (int index = 0; index < CGoodsPool.Count; index++)
			{
				var goods = (EGoods)index;
				if (CBombingOrders.BombsStockpile(order, goods))
				{
					long held = province.Pool[goods].Raw;
					long take = fromStockpile;
					long asked = goods == EGoods.Supplies ? need.Supplies * DaysOfNeed
						: goods == EGoods.Fuel ? need.Fuel * DaysOfNeed : 0;
					if (asked > 0)
					{
						take = fromStockpile * Math.Min(held * CFixedPoint.Scale / asked, MostFullness) / CFixedPoint.Scale;
					}
					take = Math.Min(take, held);
					province.Pool[goods] = new CFixedPoint(held - take);
					side.StockpileDestroyed[goods] = new CFixedPoint(side.StockpileDestroyed[goods].Raw + take);
				}
				if (CBombingOrders.BombsProduction(order, goods))
				{
					long made = province.CurrentProducing[goods].Raw;
					long take = Math.Min(fromProduction, made);
					province.CurrentProducing[goods] = new CFixedPoint(made - take);
					side.ProductionDestroyed[goods] = new CFixedPoint(side.ProductionDestroyed[goods].Raw + take);
				}
			}

			foreach (CBuilding building in state.Buildings)
			{
				if (!CBombingOrders.BombsBuilding(order, building))
				{
					continue;
				}
				long most = province.BuildingMaxLevel(building).Raw;
				long current = province.BuildingLevel(building).Raw;
				long take = building.DamageFactor.Raw * fromBuildings / CFixedPoint.Scale;
				if (most > 0)
				{
					take = current * CFixedPoint.Scale / most * take / CFixedPoint.Scale;
				}
				take = Math.Min(take, current);
				province.SetBuildingCurrentLevel(building, new CFixedPoint(current - take));
				side.BuildingsDestroyed[building.Key] = new CFixedPoint(side.BuildingsDestroyed.GetValueOrDefault(building.Key).Raw + take);
			}
		}

		for (int index = 0; index < CGoodsPool.Count; index++)
		{
			var goods = (EGoods)index;
			province.Pool[goods] = new CFixedPoint(Math.Clamp(province.Pool[goods].Raw, 0, MostHeld));
			province.CurrentProducing[goods] = new CFixedPoint(Math.Clamp(province.CurrentProducing[goods].Raw, 0, MostHeld));
		}
	}
}
