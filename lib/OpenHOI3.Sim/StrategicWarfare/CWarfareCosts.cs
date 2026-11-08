using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.StrategicWarfare;

/// <summary>
/// What a loss is worth, in IC and men, in 15-place fixed point: a whole unit's price, the share of it a part of a brigade's strength comes to, and what one side of a battle lost.
/// The original's <c>GetTotalBuildCost</c> and <c>StratWarfare_</c> helpers. See <c>docs/mechanics/strategicwarfare.md</c>, section 3.
/// </summary>
public static class CWarfareCosts
{
	private const long RateDivisor = 30000;
	private const long HalfRate = 1L << (CFixed15.Places - 1);
	private const string RepairCost = "repair_cost_multiplier";
	private const string BuildCostIc = CSubUnitStats.BuildCostIc;
	private const string UnitRepair = "unit_repair";
	private const string ReinforcementBonus = "reinforcement_bonus";
	private const string BomberFlag = "is_bomber";
	private const long ShipLost = 1000;

	/// <summary>A number of thousandths as 15-place fixed point, cut toward nought.</summary>
	public static long Fix(long thousandths) => thousandths * CFixed15.Scale / CFixedPoint.Scale;

	private static long Times(long a, long b) => (a * b) >> CFixed15.Places;

	private static long Over(long a, long b) => b == 0 ? 0 : (a << CFixed15.Places) / b;

	/// <summary>The whole price of a type at a country's own levels: its IC, day by day over its days, and its men.</summary>
	public static (long Ic, long Men) Total(CCountry country, CSubUnitType type)
	{
		var order = new CBrigadeOrder(type);
		CBuildQueue.SetModelToCountrys(country, order);
		CSubUnit built = CBuildQueue.AsBuilt(order, false, out long levels);
		long days = CBuildCosts.BuildTime(country, built, levels) * CFixedPoint.Scale;
		long daily = CBuildCosts.BuildCost(country, built, levels);
		return (Fix(daily * days / CFixedPoint.Scale), Fix(CBuildQueue.ManpowerOf(country, built, false).Raw));
	}

	/// <summary>The price of a part of a type's strength, the part in 15-place fixed point: the reinforcement's rate, a half of the strength over 30 and raised by the country's repair and reinforcement.</summary>
	public static (long Ic, long Men) Partial(CCountry country, CSubUnitType type, long amount)
	{
		long full = type.Stats[CSubUnitType.MaxStrength].Raw;
		long rate = Times(Fix(full * CFixedPoint.Scale / RateDivisor), HalfRate);
		rate = Times(rate, CFixed15.Scale + Fix(country.Modifier[UnitRepair].Raw));
		rate = Times(rate, CFixed15.Scale + Fix(country.Modifier[ReinforcementBonus].Raw));
		long ic = 0;
		if (rate > 0)
		{
			long unit = Fix(Math.Max(type.Stats[BuildCostIc].Raw, CFixedPoint.Scale));
			ic = Times(Times(Fix(type.Stats[RepairCost].Raw), unit), Over(amount, rate));
		}
		long men = Times(Over(amount, Fix(full)), Fix(type.Stats[CSubUnitStats.BuildCostManpower].Raw));
		return (ic, men);
	}

	/// <summary>What a brigade's strength lost costs, its owner paying: the whole price where that is all it has, the part's otherwise; the amount in thousandths.</summary>
	public static (long Ic, long Men) LandDamage(CGameState state, CSubUnit brigade, long amount)
	{
		if (amount <= 0 || brigade.Unit is null || state.Country(brigade.Unit.Owner) is not { } owner)
		{
			return (0, 0);
		}
		return amount >= brigade.Strength.Raw ? Total(owner, brigade.Type) : Partial(owner, brigade.Type, Fix(amount));
	}

	public static bool IsBomber(CSubUnitType type) => type.Branch == EUnitBranch.Air && type.Flag(BomberFlag);

	/// <summary>
	/// What one side of a battle lost of the types chosen, priced for a country. In a battle at sea whole ships at their full price and damage as a part; in any other only the
	/// figure kept as destroyed, as a part.
	/// </summary>
	public static long SideLoss(CGameState state, CCombatSide side, CCountryTag tag, Func<CSubUnitType, bool> chosen)
	{
		if (state.Country(tag) is not { } country)
		{
			return 0;
		}
		bool atSea = side.Combat.Kind == ECombatKind.Naval || (CBombing.IsBombing(side.Combat.Kind) && side.IsShips);
		long total = 0;
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			if (!chosen(type))
			{
				continue;
			}
			long destroyed = side.Destroyed.GetValueOrDefault(type.Key);
			if (atSea)
			{
				long whole = Times(Fix(destroyed), Total(country, type).Ic);
				total += whole + Partial(country, type, Fix(side.Damaged.GetValueOrDefault(type.Key))).Ic;
			}
			else
			{
				total += Partial(country, type, Fix(destroyed)).Ic;
			}
		}
		return total;
	}

	/// <summary>How many whole ships' worth a ship's loss writes as destroyed.</summary>
	public const long WholeShip = ShipLost;
}
