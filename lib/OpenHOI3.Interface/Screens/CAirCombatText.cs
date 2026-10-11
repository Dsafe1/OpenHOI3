using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the battle window shows of a battle in the air and of a bombing: each side's
/// line over its list, the rows of the list - wings, and under bombs the province's
/// buildings, stockpiles and production as well - and each row's name, picture and
/// bars. See <c>docs/mechanics/combatview.md</c>, section 11.
/// </summary>
public static class CAirCombatText
{
	public const string PlanesKey = "PLANES_LABEL";

	/// <summary>What a sub unit's picture in a row is called: this and its type.</summary>
	public const string SubUnitSprite = "GFX_counter_";

	/// <summary>What a building's picture in a row is called: this and the building's key.</summary>
	public const string BuildingSprite = "icon_build_";

	/// <summary>A row's bar of what lies in the province is full at this much, in thousandths.</summary>
	private const long FullStockpile = 200000;

	private const long Scale = CFixedPoint.Scale;

	/// <summary>The keys of the goods' names, in the goods' own order.</summary>
	public static readonly string[] GoodsKeys =
	{
		"SUPPLIES_IRO", "FUEL_IRO", "MONEY_IRO", "CRUDE_OIL_IRO", "METAL_IRO", "ENERGY_IRO", "RAREMAT_IRO",
	};

	/// <summary>Whether a battle has the air side of the window: one in the air, or a bombing.</summary>
	public static bool HasAirSide(CCombat combat)
	{
		return combat.Kind == ECombatKind.Air || CBombing.IsBombing(combat.Kind);
	}

	/// <summary>
	/// The line over a side's list: how many sub units it has, in yellow, and the word
	/// for wings. The bombed side of a bombing has none.
	/// </summary>
	public static string PlanesLabel(Func<string, string> localise, CCombatSide side)
	{
		if (CBombing.IsBombing(side.Combat.Kind) && !side.IsAttacker)
		{
			return "";
		}
		int wings = 0;
		foreach (CUnit unit in side.Units)
		{
			wings += unit.SubUnits.Count;
		}
		return "§Y" + wings.ToString(System.Globalization.CultureInfo.InvariantCulture) + "§W "
			+ localise(PlanesKey);
	}

	/// <summary>
	/// The rows of a side's list, in their order. Every side has a row for each sub unit
	/// of each of its units, unit by unit as they joined. The bombed side of a bombing
	/// has rows before those, by what the first bomber's mission bombs: each such
	/// building the province has any of, and then good by good what lies there and what
	/// is made there.
	/// </summary>
	public static List<CCombatRow> Rows(CGameState state, CCombatSide side)
	{
		var rows = new List<CCombatRow>();
		CCombat combat = side.Combat;
		if (CBombing.IsBombing(combat.Kind) && !side.IsAttacker && combat.Attacker.Units.Count > 0)
		{
			EUnitOrder order = combat.Attacker.Units[0].Order;
			CProvince province = state.Province(combat.Province);
			foreach (CBuilding building in state.Buildings)
			{
				if (CBombingOrders.BombsBuilding(order, building) && province.BuildingMaxLevel(building).Raw > 0)
				{
					rows.Add(new CCombatRow(ECombatRowKind.Building, Building: building));
				}
			}
			for (int index = 0; index < CGoodsPool.Count; index++)
			{
				var goods = (EGoods)index;
				if (CBombingOrders.BombsStockpile(order, goods))
				{
					rows.Add(new CCombatRow(ECombatRowKind.GoodHeld, Goods: goods));
				}
				if (CBombingOrders.BombsProduction(order, goods) && province.MaxProducing[goods].Raw > 0)
				{
					rows.Add(new CCombatRow(ECombatRowKind.GoodMade, Goods: goods));
				}
			}
		}
		foreach (CUnit unit in side.Units)
		{
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				rows.Add(new CCombatRow(ECombatRowKind.SubUnit, SubUnit: subUnit));
			}
		}
		return rows;
	}

	/// <summary>
	/// Whether a side keeps the battle's window up with so many rows in its list: a
	/// side of an air battle and the bombers of a bombing while they have a row, the
	/// bombed side of a bombing always - its list is not asked.
	/// </summary>
	public static bool KeepsWindowUp(CCombatSide side, int rows)
	{
		return rows > 0 || (CBombing.IsBombing(side.Combat.Kind) && !side.IsAttacker);
	}

	/// <summary>A row's name: the sub unit's, the building's, or the good's.</summary>
	public static string RowName(Func<string, string> localise, CCombatRow row)
	{
		switch (row.Kind)
		{
			case ECombatRowKind.SubUnit:
				return row.SubUnit is { } subUnit ? (subUnit.Name.Length != 0 ? subUnit.Name : subUnit.Type.Key) : "";
			case ECombatRowKind.Building:
				return row.Building is { } building ? localise(building.Key) : "";
			default:
				return localise(GoodsKeys[(int)row.Goods]);
		}
	}

	/// <summary>
	/// The sprite of a row's picture, used where there is one of that name; a good's row
	/// has none and keeps the picture the file gives it.
	/// </summary>
	public static string RowSprite(CCombatRow row)
	{
		return row.Kind switch
		{
			ECombatRowKind.SubUnit when row.SubUnit is { } subUnit => SubUnitSprite + subUnit.Type.Key,
			ECombatRowKind.Building when row.Building is { } building => BuildingSprite + building.Key,
			_ => "",
		};
	}

	/// <summary>The frame of a good's picture, counted from one: the goods in their own order.</summary>
	public static int GoodsFrame(CCombatRow row)
	{
		return (int)row.Goods + 1;
	}

	/// <summary>Whether a row has a bar of organisation. Only a sub unit's has.</summary>
	public static bool HasOrganisation(CCombatRow row)
	{
		return row.Kind == ECombatRowKind.SubUnit;
	}

	/// <summary>
	/// A row's upper bar, in whole hundredths: a sub unit's strength of its full
	/// strength, what works of a building of what was built, what lies in the province
	/// of a good of two hundred, and what the province makes of a good of the most it
	/// can make.
	/// </summary>
	public static int StrengthBar(CGameState state, CCombat combat, CCombatRow row)
	{
		CProvince province = state.Province(combat.Province);
		long share;
		switch (row.Kind)
		{
			case ECombatRowKind.SubUnit:
				return row.SubUnit is { } subUnit ? CCombatViewText.ShipStrengthBar(state, subUnit) : 0;
			case ECombatRowKind.Building:
				share = row.Building is { } building
					? Share(province.BuildingLevel(building).Raw, province.BuildingMaxLevel(building).Raw)
					: 0;
				break;
			case ECombatRowKind.GoodHeld:
				share = province.Pool[row.Goods].Raw * Scale / FullStockpile;
				break;
			default:
				share = Share(province.CurrentProducing[row.Goods].Raw, province.MaxProducing[row.Goods].Raw);
				break;
		}
		return (int)(share * 100000 / Scale / Scale);
	}

	/// <summary>A sub unit's organisation bar, in whole hundredths of the most its type has.</summary>
	public static int OrganisationBar(CCombatRow row)
	{
		return row.SubUnit is { } subUnit ? CCombatViewText.ShipOrganisationBar(subUnit) : 0;
	}

	/// <summary>One figure as thousandths of another; of nothing, less than nothing.</summary>
	private static long Share(long part, long whole)
	{
		return whole == 0 ? -1 : part * Scale / whole;
	}
}
