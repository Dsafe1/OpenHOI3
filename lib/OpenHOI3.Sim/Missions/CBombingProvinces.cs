using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// Which province of its list a bombing mission flies at next. Each mission has its own
/// way, and three of them are odd: ground attack goes round a list of two or more in
/// order and weighs nothing; interdiction takes the last province of its list that its
/// country can see into; a naval strike goes round like ground attack. They are built so.
/// See <c>docs/mechanics/bombing.md</c>, section 4.
/// </summary>
public static class CBombingProvinces
{
	/// <summary>The score of the province a unit is already flying at.</summary>
	private const long Current = -10000;

	/// <summary>The score of a province the unit's country cannot see into.</summary>
	private const long Unseen = -20000;

	/// <summary>The score of a province enough wings are bound for already.</summary>
	private const long Crowded = -10000000;

	/// <summary>A province scoring this or under is not taken for the best.</summary>
	private const long Least = -1000;

	/// <summary>With this unit's own, more wings than this are not sent at one province.</summary>
	private const int MostWingsAtOne = 3;

	/// <summary>The province to fly at next, or 0 for none.</summary>
	public static int Pick(CGameState state, CUnit unit)
	{
		return unit.Order switch
		{
			EUnitOrder.GroundAttack => PickForGroundAttack(state, unit),
			EUnitOrder.Interdiction => PickForInterdiction(state, unit),
			EUnitOrder.NavalStrike => PickForNavalStrike(state, unit),
			EUnitOrder.StrategicBombing => Highest(state, unit, StrategicWorth, isCrowdedPassedOver: true),
			EUnitOrder.LogisticalStrike => Highest(state, unit, LogisticalWorth),
			EUnitOrder.RunwayCratering => Highest(state, unit, RunwayWorth),
			EUnitOrder.InstallationStrike => Highest(state, unit, InstallationWorth),
			EUnitOrder.PortStrike => Highest(state, unit, PortWorth),
			_ => 0,
		};
	}

	/// <summary>
	/// The province after the one the unit is flying at, in the mission's list: the first
	/// where it is flying at none, the first again after the last, and the one it has
	/// where that is not on the list or the list is empty.
	/// </summary>
	public static int Next(CMission mission)
	{
		int current = mission.Destination;
		if (mission.Provinces.Count == 0)
		{
			return current;
		}
		if (current == 0)
		{
			return mission.Provinces[0];
		}
		int at = mission.Provinces.IndexOf(current);
		if (at < 0)
		{
			return current;
		}
		return at == mission.Provinces.Count - 1 ? mission.Provinces[0] : mission.Provinces[at + 1];
	}

	private static int PickForGroundAttack(CGameState state, CUnit unit)
	{
		CMission mission = unit.Mission;
		int next = Next(mission);
		int best = 0;
		long most = 0;
		foreach (int id in mission.Provinces)
		{
			CProvince province = state.Province(id);
			if (!CBombingOrders.MayList(state, unit, unit.Order, province))
			{
				continue;
			}
			long score = LandScore(state, unit, province, countsWings: true);
			if (score > most && score > Least)
			{
				most = score;
				best = id;
			}
		}
		if (unit.NoBombingHours > 0)
		{
			return 0;
		}
		return best == 0 || next != mission.Destination ? next : best;
	}

	private static int PickForInterdiction(CGameState state, CUnit unit)
	{
		CMission mission = unit.Mission;
		int next = Next(mission);
		int best = LastSeen(state, unit, province => LandScore(state, unit, province, countsWings: false));
		return next != mission.Destination && unit.NoBombingHours > 0 ? next : best;
	}

	private static int PickForNavalStrike(CGameState state, CUnit unit)
	{
		CMission mission = unit.Mission;
		int next = Next(mission);
		int best = LastSeen(state, unit, province => SeaScore(state, unit, province));
		return next == mission.Destination || unit.NoBombingHours > 0 ? best : next;
	}

	/// <summary>
	/// The "best" of interdiction and of the naval strike: a province is taken where it
	/// scores above the least or above the best so far - so the last listed province
	/// that scores above the least is the one that stands.
	/// </summary>
	private static int LastSeen(CGameState state, CUnit unit, System.Func<CProvince, long> score)
	{
		int best = 0;
		long most = 0;
		foreach (int id in unit.Mission.Provinces)
		{
			CProvince province = state.Province(id);
			if (!CBombingOrders.MayList(state, unit, unit.Order, province))
			{
				continue;
			}
			long worth = score(province);
			if (worth > Least || worth > most)
			{
				most = worth;
				best = id;
			}
		}
		return best;
	}

	/// <summary>
	/// What a province is worth to a mission against land units: the strength of the
	/// enemy's land units in it, summed unit by unit, the running sum cut to a hundredth
	/// each time a unit is not what the mission is after - one holding ground for ground
	/// attack, one on the move for interdiction.
	/// </summary>
	public static long LandScore(CGameState state, CUnit unit, CProvince province, bool countsWings)
	{
		if (province.Id == unit.Mission.Destination)
		{
			return Current;
		}
		if (countsWings && IsCrowded(state, unit, province))
		{
			return Crowded;
		}
		if (CIntelligence.Level(state, province, unit.Owner) < CIntelligence.LeastToPick)
		{
			return Unseen;
		}
		long sum = 0;
		foreach (CUnit other in CBombing.Standing(state, province))
		{
			if (!CMovementRules.IsLandUnit(other) || !CMovementRules.IsEnemy(state, other.Owner, unit.Owner))
			{
				continue;
			}
			sum += CUnitTotals.Strength(other).Raw;
			bool isWanted = countsWings ? CBombing.IsHoldingGround(other) : other.IsMoving;
			if (!isWanted)
			{
				sum = sum * CFixedPoint.Scale / 100000;
			}
		}
		return sum;
	}

	/// <summary>What a sea province is worth to a naval strike: a thousand a ship of the enemy's fleets in it.</summary>
	public static long SeaScore(CGameState state, CUnit unit, CProvince province)
	{
		if (province.Id == unit.Mission.Destination)
		{
			return Current;
		}
		if (IsCrowded(state, unit, province))
		{
			return Crowded;
		}
		if (CIntelligence.Level(state, province, unit.Owner) < CIntelligence.LeastToPick)
		{
			return Unseen;
		}
		return Ships(state, unit, province) * CFixedPoint.Scale;
	}

	/// <summary>
	/// The province that is worth most, the first found among equals, the first that may
	/// be listed standing until a better is found.
	/// </summary>
	private static int Highest(CGameState state, CUnit unit, System.Func<CGameState, CUnit, CProvince, long> worth,
		bool isCrowdedPassedOver = false)
	{
		int best = 0;
		long most = 0;
		foreach (int id in unit.Mission.Provinces)
		{
			CProvince province = state.Province(id);
			if (!CBombingOrders.MayList(state, unit, unit.Order, province))
			{
				continue;
			}
			long score = worth(state, unit, province);
			if (score <= most && best != 0)
			{
				continue;
			}
			if (isCrowdedPassedOver && IsCrowded(state, unit, province))
			{
				continue;
			}
			most = score;
			best = id;
		}
		return best;
	}

	/// <summary>A figure over the province's anti-aircraft guns, where it has any.</summary>
	private static long OverGuns(CProvince province, long figure)
	{
		long guns = province.Modifier[CBombingOrders.AntiAir].Raw;
		return guns == 0 ? figure : figure * CFixedPoint.Scale / guns;
	}

	private static long StrategicWorth(CGameState state, CUnit unit, CProvince province)
	{
		CGoodsPool yield = province.CurrentProducing;
		return OverGuns(province, province.Modifier[CBombingOrders.Industry].Raw + yield[EGoods.CrudeOil].Raw
			+ yield[EGoods.Energy].Raw + yield[EGoods.RareMaterials].Raw + yield[EGoods.Metal].Raw);
	}

	private static long LogisticalWorth(CGameState state, CUnit unit, CProvince province)
	{
		return OverGuns(province, province.Pool[EGoods.Supplies].Raw + province.Modifier[CBombingOrders.Infrastructure].Raw);
	}

	private static long RunwayWorth(CGameState state, CUnit unit, CProvince province)
	{
		long based = 0;
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit other in country.AllUnits())
			{
				if (other.Kind == EUnitKind.Air && !other.IsRemoved && other.Base == province.Id && other.CarriedBy is null)
				{
					based++;
				}
			}
		}
		return OverGuns(province, based * CFixedPoint.Scale);
	}

	private static long InstallationWorth(CGameState state, CUnit unit, CProvince province)
	{
		return OverGuns(province, province.Modifier[CBombingOrders.RadarLevel].Raw);
	}

	private static long PortWorth(CGameState state, CUnit unit, CProvince province)
	{
		if (!CMovementRules.IsEnemy(state, province.Controller, unit.Owner))
		{
			return 0;
		}
		long ships = 0;
		foreach (CUnit fleet in state.UnitsIn(province.Id))
		{
			if (fleet.Kind == EUnitKind.Navy)
			{
				ships += fleet.SubUnits.Count;
			}
		}
		return ships * CFixedPoint.Scale + province.Modifier[CBombingOrders.NavalBaseLevel].Raw;
	}

	private static long Ships(CGameState state, CUnit unit, CProvince province)
	{
		long ships = 0;
		foreach (CUnit fleet in state.UnitsIn(province.Id))
		{
			if (fleet.Kind == EUnitKind.Navy && CMovementRules.IsEnemy(state, fleet.Owner, unit.Owner))
			{
				ships += fleet.SubUnits.Count;
			}
		}
		return ships;
	}

	/// <summary>
	/// Whether the wings already bound for a province - every air unit of a country at
	/// war with its holder whose mission is flying at it - would with this unit's own
	/// come to more than three.
	/// </summary>
	private static bool IsCrowded(CGameState state, CUnit unit, CProvince province)
	{
		int bound = 0;
		foreach (CCountry country in state.Countries)
		{
			if (!CMovementRules.IsEnemy(state, country.Tag, province.Controller))
			{
				continue;
			}
			foreach (CUnit other in country.AllUnits())
			{
				if (other.Kind == EUnitKind.Air && !other.IsRemoved && other.Mission.Destination == province.Id)
				{
					bound += other.SubUnits.Count;
				}
			}
		}
		return bound > 0 && unit.SubUnits.Count + bound > MostWingsAtOne;
	}
}
