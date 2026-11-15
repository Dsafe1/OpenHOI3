using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// What one province is to the supplies that cross it: how well it is built, how much
/// it can pass in a day, what a load loses on the way through, and how long the way
/// is. See <c>docs/mechanics/supply.md</c>, sections 6 to 8.
/// </summary>
public static class CSupplyFigures
{
	public const string Military = "military";
	public const string WeatherSection = "weather";
	public const string ThroughputImpact = "INFRA_THROUGHPUT_IMPACT";
	public const string OwnedThroughputBonus = "OWNED_AND_CONTROLLED_THROUGHPUT_CAP_BONUS";
	public const string SupplyTax = "SUPPLY_TAX";
	public const string PartisanTax = "PARTISAN_EFFECT_ON_SUPPLY_TAX";
	public const string MudTax = "MUDDYNESSSUPPLYTAXMODIFIER";

	public const string Infrastructure = "infrastructure";
	public const string LocalInfrastructure = "local_infrastructure";
	public const string GlobalInfrastructure = "global_infrastructure";
	public const string PartisanSupport = "local_partisan_support";
	public const string Throughput = "supply_throughput";
	public const string TransferCost = "supply_transfer_cost";

	/// <summary>Infrastructure is never counted as less than this, nor as more than whole.</summary>
	private const long LeastInfrastructure = 10;

	/// <summary>What a capital and the provinces beside it can pass: as good as no limit.</summary>
	public static readonly CFixedPoint Unlimited = new(99999000);

	/// <summary>
	/// The infrastructure a province has the use of, in thousandths of a whole: what is
	/// built, raised or lowered by what applies to the province and to its holder, and
	/// held between a hundredth and one.
	/// </summary>
	public static long EffectiveInfrastructure(CProvince province)
	{
		long built = province.Modifier[Infrastructure].Raw
			* (CFixedPoint.Scale + province.Modifier[LocalInfrastructure].Raw + province.Modifier[GlobalInfrastructure].Raw)
			/ CFixedPoint.Scale;
		return Math.Clamp(built, LeastInfrastructure, CFixedPoint.Scale);
	}

	/// <summary>
	/// The most infrastructure a province can have, in thousandths of a whole: ten
	/// levels is 1000. With no damage yet it is what is built.
	/// </summary>
	public static long MostInfrastructure(CGameState state, CProvince province)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.ModifierKey.Equals(Infrastructure, StringComparison.OrdinalIgnoreCase))
			{
				// What was built, damaged or not: the original keeps this figure by the maximum.
				return province.BuildingMaxLevel(building).Raw * building.ModifierValue.Raw / CFixedPoint.Scale;
			}
		}
		return 0;
	}

	/// <summary>
	/// How big a province counts as, 1 to 4, by its bounding box: the width and the
	/// height together under 30 pixels, under 60, under 90, or more.
	/// </summary>
	public static int SizeClass(CProvince province)
	{
		return province.Span < 30 ? 1 : province.Span < 60 ? 2 : province.Span < 90 ? 3 : 4;
	}

	/// <summary>
	/// How much further from the depot a province is than the one a load comes into it
	/// from: its size, once over good roads, twice over poor and three times over bad.
	/// </summary>
	public static int Step(CProvince province)
	{
		long lack = 4000 - EffectiveInfrastructure(province) * 4000 / CFixedPoint.Scale;
		if (lack < CFixedPoint.Scale)
		{
			lack = CFixedPoint.Scale;
		}
		return (int)(lack / CFixedPoint.Scale) * SizeClass(province);
	}

	/// <summary>
	/// Whether a province is its holder's capital or touches it. Such a province passes
	/// supplies without limit.
	/// </summary>
	public static bool IsBesideCapital(CGameState state, CProvince province)
	{
		if (state.Country(province.Controller) is not { ActingCapital: > 0 } holder)
		{
			return false;
		}
		if (holder.ActingCapital == province.Id)
		{
			return true;
		}
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.To == holder.ActingCapital)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>How much a province can pass on in a day.</summary>
	public static CFixedPoint Capacity(CGameState state, CProvince province)
	{
		if (IsBesideCapital(state, province))
		{
			return Unlimited;
		}

		// The level of its infrastructure, squared, and never less than one.
		long level = 10 * CFixedPoint.Scale * EffectiveInfrastructure(province) / CFixedPoint.Scale;
		long worth = level < CFixedPoint.Scale ? CFixedPoint.Scale : level * level / CFixedPoint.Scale;
		worth = state.Defines[Military, ThroughputImpact].Raw * worth / CFixedPoint.Scale;
		if (province.Owner == province.Controller)
		{
			worth = state.Defines[Military, OwnedThroughputBonus].Raw * worth / CFixedPoint.Scale;
		}
		long better = CFixedPoint.Scale;
		if (state.Country(province.Controller) is { } holder)
		{
			better += holder.Modifier[Throughput].Raw + holder.Technology[Throughput].Raw;
		}
		return new CFixedPoint(better * worth / CFixedPoint.Scale);
	}

	/// <summary>What a load of supplies loses crossing a province.</summary>
	public static CFixedPoint Loss(CGameState state, CProvince province)
	{
		long tax = state.Defines[Military, SupplyTax].Raw;
		if (province.Owner == province.Controller)
		{
			tax = tax * CFixedPoint.Scale / (2 * CFixedPoint.Scale);
		}

		long times = CFixedPoint.Scale;
		if (province.Weather.Frozen == CFixedPoint.Zero)
		{
			times += province.Weather.Muddyness.Raw * state.Defines[WeatherSection, MudTax].Raw / CFixedPoint.Scale;
		}
		// Held by a country at war with its owner, partisans take their share, less what the suppression in and around the
		// province takes off it - the figure its last day made, held between nothing and one (supply.md, section 8).
		if (CMovementRules.IsEnemy(state, province.Controller, province.Owner))
		{
			long partisans = state.Defines[Military, PartisanTax].Raw * province.Modifier[PartisanSupport].Raw / CFixedPoint.Scale;
			long suppression = Math.Clamp(province.Suppression.Raw, 0, CFixedPoint.Scale);
			times += partisans * (CFixedPoint.Scale - suppression) / CFixedPoint.Scale;
		}
		tax = tax * times / CFixedPoint.Scale;

		if (state.Country(province.Controller) is { } holder)
		{
			tax += holder.Technology[TransferCost].Raw;
		}
		return new CFixedPoint(Math.Max(tax, 0));
	}
}
