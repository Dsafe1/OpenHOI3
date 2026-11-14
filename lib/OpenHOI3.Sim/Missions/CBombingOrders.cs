using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// What tells the eight bombing missions apart: which frame an hour of each runs by,
/// what each may be given for, which provinces it lists, what it goes for and what it
/// bombs. The original asks an order each of these as one question; here they are one
/// method each. See <c>docs/mechanics/bombing.md</c>, section 1.
/// </summary>
public static class CBombingOrders
{
	public const string StrategicAttack = "strategic_attack";

	// The modifiers a building is known by: what one level of it gives its province.
	public const string Infrastructure = "infrastructure";
	public const string Industry = "ic";
	public const string NavalBaseLevel = "naval_capacity";
	public const string AirBaseLevel = "air_capacity";
	public const string RadarLevel = "radar_level";
	public const string AntiAir = "local_anti_air";

	public static bool IsBombingMission(EUnitOrder order)
	{
		return IsStrike(order) || IsBombing(order);
	}

	/// <summary>The missions that keep the province they fly at from one sortie to the next - the raid on convoys from the air among them.</summary>
	public static bool IsStrike(EUnitOrder order)
	{
		return order is EUnitOrder.GroundAttack or EUnitOrder.Interdiction or EUnitOrder.NavalStrike or EUnitOrder.AirConvoyRaid;
	}

	/// <summary>
	/// Raids convoys: bombs nothing and starts no bombing, but in the raid's own battle fires its sea attack at every ship
	/// of the convoy's, which it needs no roll to find (<c>navalmissions.md</c>, section 8.1).
	/// </summary>
	public static bool RaidsConvoys(EUnitOrder order)
	{
		return order == EUnitOrder.AirConvoyRaid;
	}

	/// <summary>The missions that forget their province once a bombing has it, and pick afresh at their base.</summary>
	public static bool IsBombing(EUnitOrder order)
	{
		return order is EUnitOrder.StrategicBombing or EUnitOrder.LogisticalStrike or EUnitOrder.RunwayCratering
			or EUnitOrder.InstallationStrike or EUnitOrder.PortStrike;
	}

	/// <summary>Bombs the ground: a land province an enemy holds is bombed for itself, no unit needed.</summary>
	public static bool BombsGround(EUnitOrder order)
	{
		return IsBombing(order) || order == EUnitOrder.NavalStrike;
	}

	/// <summary>Goes for ships, and fires its sea attack - carrier protection too (<c>transport.md</c>, section 13.2).</summary>
	public static bool GoesForShips(EUnitOrder order)
	{
		return order is EUnitOrder.NavalStrike or EUnitOrder.PortStrike or EUnitOrder.CarrierProtection;
	}

	/// <summary>Goes for land units, and fires its soft and hard attack by how soft the target is.</summary>
	public static bool GoesForLandUnits(EUnitOrder order)
	{
		return order is EUnitOrder.GroundAttack or EUnitOrder.Interdiction;
	}

	/// <summary>Goes for aircraft at their base, and otherwise fires its hard attack.</summary>
	public static bool GoesForBasedAircraft(EUnitOrder order)
	{
		return order is EUnitOrder.RunwayCratering or EUnitOrder.InstallationStrike or EUnitOrder.NavalStrike;
	}

	/// <summary>Looks first for land units that are fighting.</summary>
	public static bool PrefersFighting(EUnitOrder order)
	{
		return order == EUnitOrder.GroundAttack;
	}

	/// <summary>
	/// Whether a mission may be given for a province: ground attack and interdiction for
	/// land an enemy holds; the four strategic kinds for that, by a unit with a strategic
	/// attack; a naval strike and the raid on convoys for water; a port strike for land.
	/// </summary>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, EUnitOrder order, CProvince province)
	{
		if (unit.Kind != EUnitKind.Air || !IsBombingMission(order))
		{
			return false;
		}
		switch (order)
		{
			case EUnitOrder.GroundAttack:
			case EUnitOrder.Interdiction:
				return !province.IsSea && CMovementRules.IsEnemy(state, province.Controller, unit.Owner);
			case EUnitOrder.NavalStrike:
			case EUnitOrder.AirConvoyRaid:
				return province.IsSea;
			case EUnitOrder.PortStrike:
				return !province.IsSea;
			default:
				return !province.IsSea && CMovementRules.IsEnemy(state, province.Controller, unit.Owner)
					&& CUnitTotals.Sum(unit, StrategicAttack) > CFixedPoint.Zero;
		}
	}

	/// <summary>Whether a province may be on a mission's list, and be picked from it.</summary>
	public static bool MayList(CGameState state, CUnit unit, EUnitOrder order, CProvince province)
	{
		bool isPeacefulLand = !province.IsSea && !CMovementRules.IsEnemy(state, province.Controller, unit.Owner);
		switch (order)
		{
			case EUnitOrder.GroundAttack:
				return !province.IsSea;
			// The raid on convoys from the air lists any province at all.
			case EUnitOrder.AirConvoyRaid:
				return true;
			case EUnitOrder.NavalStrike:
				return province.IsSea && province.SeaIndex >= 0;       // water that is no lake
			case EUnitOrder.PortStrike:
				return CMovementRules.IsEnemy(state, province.Controller, unit.Owner);
			case EUnitOrder.LogisticalStrike:
				if (isPeacefulLand)
				{
					return false;
				}
				return province.Pool[EGoods.Supplies] != CFixedPoint.Zero || province.Pool[EGoods.Fuel] != CFixedPoint.Zero
					|| MaxLevel(state, province, Infrastructure) > CFixedPoint.Zero;
			case EUnitOrder.RunwayCratering:
				return !isPeacefulLand && MaxLevel(state, province, AirBaseLevel) > CFixedPoint.Zero;
			default:
				return !isPeacefulLand;
		}
	}

	/// <summary>
	/// Whether a unit under a mission bombs in a province. Six missions only in the one
	/// they are flying at; ground attack also wherever its country sees an enemy land
	/// unit holding ground, and interdiction wherever it sees one on the move.
	/// </summary>
	public static bool ShouldBomb(CGameState state, CUnit unit, CProvince province)
	{
		EUnitOrder order = unit.Order;
		// Carrier protection bombs where its carrier is, and nowhere else.
		if (order == EUnitOrder.CarrierProtection)
		{
			return province.Id == unit.OrderProvince;
		}
		// The raid on convoys from the air bombs nowhere: its raid is its own hour's.
		if (!IsBombingMission(order) || RaidsConvoys(order))
		{
			return false;
		}
		bool isStrike = province.Id == unit.Mission.Destination;
		if (!GoesForLandUnits(order))
		{
			return isStrike;
		}
		if (order == EUnitOrder.GroundAttack && isStrike)
		{
			return true;
		}
		if (CIntelligence.Level(state, province, unit.Owner) < CIntelligence.LeastToPick
			|| CBombing.Standing(state, province).Count == 0)
		{
			return false;
		}
		if (isStrike)
		{
			return true;
		}
		foreach (CUnit other in CBombing.Standing(state, province))
		{
			if (!CMovementRules.IsLandUnit(other) || !CMovementRules.IsEnemy(state, other.Owner, unit.Owner))
			{
				continue;
			}
			if (order == EUnitOrder.GroundAttack ? CBombing.IsHoldingGround(other) : other.IsMoving)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether a mission's bombs fall on a kind of building: its own, and always the guns.</summary>
	public static bool BombsBuilding(EUnitOrder order, CBuilding building)
	{
		string key = building.ModifierKey;
		string? own = order switch
		{
			EUnitOrder.StrategicBombing => Industry,
			EUnitOrder.LogisticalStrike => Infrastructure,
			EUnitOrder.RunwayCratering => AirBaseLevel,
			EUnitOrder.InstallationStrike => RadarLevel,
			EUnitOrder.PortStrike => NavalBaseLevel,
			_ => null,
		};
		return own is not null && (Is(key, own) || Is(key, AntiAir));
	}

	/// <summary>Whether a mission's bombs fall on what lies in the province of a good: a logistical strike's, on supplies and fuel.</summary>
	public static bool BombsStockpile(EUnitOrder order, EGoods goods)
	{
		return order == EUnitOrder.LogisticalStrike && goods is EGoods.Supplies or EGoods.Fuel;
	}

	/// <summary>Whether a mission's bombs fall on the day's production of a good: a strategic bombing's, on the four resources.</summary>
	public static bool BombsProduction(EUnitOrder order, EGoods goods)
	{
		return order == EUnitOrder.StrategicBombing
			&& goods is EGoods.CrudeOil or EGoods.Metal or EGoods.Energy or EGoods.RareMaterials;
	}

	/// <summary>The building a modifier belongs to, or null.</summary>
	public static CBuilding? BuildingOf(CGameState state, string modifierKey)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (Is(building.ModifierKey, modifierKey))
			{
				return building;
			}
		}
		return null;
	}

	/// <summary>How much of the building of a modifier has been built in a province, damaged or not.</summary>
	public static CFixedPoint MaxLevel(CGameState state, CProvince province, string modifierKey)
	{
		return BuildingOf(state, modifierKey) is { } building ? province.BuildingMaxLevel(building) : CFixedPoint.Zero;
	}

	private static bool Is(string a, string b)
	{
		return a.Equals(b, System.StringComparison.OrdinalIgnoreCase);
	}
}
