using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// The two things a country turns one good into another by, each day: energy into crude
/// oil, and crude oil into fuel. See <c>docs/mechanics/ic.md</c>, section 4.
///
/// Both belong to a country that exists. A country that holds nothing still runs its
/// shares of IC, but it converts nothing.
/// </summary>
public static class CConversions
{
	/// <summary>The crude oil a unit of energy makes before any technology: a tenth. Built into the original.</summary>
	private static readonly CFixedPoint BaseOilRate = CFixedPoint.Parse("0.1");

	/// <summary>The most crude oil a country makes from energy in a day, per IC - and never less than one unit in all.</summary>
	private static readonly CFixedPoint MostOilPerIc = CFixedPoint.Parse("0.05");

	/// <summary>A country holding this much fuel refines no more.</summary>
	private static readonly CFixedPoint MostFuel = CFixedPoint.FromInt(99000);

	/// <summary>How many days of the crude oil coming in count as plenty of fuel.</summary>
	private const int DaysOfFuel = 365;

	private const string TechnologyOilRate = "energy_to_oil_conversion";
	private const string TechnologyRefining = "refinery_efficiency";
	private const string FuelConversion = "fuel_conversion";

	/// <summary>The crude oil one unit of energy makes for this country.</summary>
	public static CFixedPoint OilRate(CCountry country)
	{
		return BaseOilRate + country.Technology[TechnologyOilRate];
	}

	/// <summary>
	/// The energy a country would turn into crude oil today, given what its stockpile
	/// holds. Three things limit it: how much oil a day its industry can make, the point
	/// at which it would hold only twice as much energy as crude oil, and what is left
	/// once industry has had what it needs.
	/// </summary>
	public static CFixedPoint EnergyToConvert(CCountry country, CGoodsPool stockpile)
	{
		long rate = OilRate(country).Raw;
		long spare = stockpile[EGoods.Energy].Raw - country.TotalIc * 2 * CFixedPoint.Scale;
		if (rate <= 0 || spare <= 0)
		{
			return CFixedPoint.Zero;
		}

		// Yesterday's usage and today's home production are the flows there are so far.
		long energyNet = country.HomeProduced[EGoods.Energy].Raw - country.Usage[EGoods.Energy].Raw;
		long crudeNet = country.HomeProduced[EGoods.CrudeOil].Raw;
		long wanted = (stockpile[EGoods.Energy].Raw + energyNet - 2 * (stockpile[EGoods.CrudeOil].Raw + crudeNet))
			* CFixedPoint.Scale / (CFixedPoint.Scale + rate * 2);
		if (wanted <= 0)
		{
			return CFixedPoint.Zero;
		}

		long mostOil = Math.Max(country.TotalIc * MostOilPerIc.Raw, CFixedPoint.Scale);
		long energyPerOil = Math.Max(CFixedPoint.Scale * CFixedPoint.Scale / rate, CFixedPoint.Scale);
		long energy = Math.Min(mostOil * energyPerOil / CFixedPoint.Scale, wanted);
		return new CFixedPoint(Math.Max(0, Math.Min(energy, spare)));
	}

	public static void EnergyIntoCrudeOil(CCountry country, CGoodsPool stockpile)
	{
		CFixedPoint energy = EnergyToConvert(country, stockpile);
		CFixedPoint oil = energy * OilRate(country);
		if (energy <= CFixedPoint.Zero || oil <= CFixedPoint.Zero)
		{
			return;
		}

		stockpile[EGoods.Energy] -= energy;
		stockpile[EGoods.CrudeOil] += oil;
		country.ConversionUsed[EGoods.Energy] += energy;
		country.ConversionMade[EGoods.CrudeOil] += oil;
	}

	/// <summary>
	/// The crude oil a country would refine today: half a unit for each whole IC, or all
	/// it has. Nothing once it holds fuel enough - 99,000, or both more than a year of
	/// the crude oil coming in and more than twice its crude oil.
	/// </summary>
	public static CFixedPoint CrudeOilToRefine(CCountry country, CGoodsPool stockpile)
	{
		CFixedPoint fuel = stockpile[EGoods.Fuel];
		CFixedPoint crudeOil = stockpile[EGoods.CrudeOil];
		if (fuel >= MostFuel)
		{
			return CFixedPoint.Zero;
		}

		CFixedPoint comingIn = country.HomeProduced[EGoods.CrudeOil] + country.ConversionMade[EGoods.CrudeOil];
		if (fuel > comingIn * DaysOfFuel && fuel / 2 > crudeOil)
		{
			return CFixedPoint.Zero;
		}

		CFixedPoint amount = CFixedPoint.FromInt(country.TotalIc / 2);
		if (amount > crudeOil)
		{
			amount = crudeOil;
		}
		return amount > CFixedPoint.Zero ? amount : CFixedPoint.Zero;
	}

	/// <summary>Refines the day's crude oil, and the fuel is all in the pool given.</summary>
	public static void CrudeOilIntoFuel(CCountry country, CGoodsPool stockpile)
	{
		stockpile[EGoods.Fuel] += Refine(country, stockpile);
	}

	/// <summary>
	/// Refines the day's crude oil out of the stockpile, and the fuel is made where the
	/// factories are, as supplies are: see <see cref="Supply.CSupplyMaking"/>.
	/// </summary>
	public static void CrudeOilIntoFuel(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		CFixedPoint fuel = Refine(country, stockpile);
		if (fuel > CFixedPoint.Zero)
		{
			Supply.CSupplyMaking.ShareOut(state, country, stockpile, EGoods.Fuel, fuel);
		}
	}

	/// <summary>Takes the day's crude oil out of the stockpile and answers the fuel it makes.</summary>
	private static CFixedPoint Refine(CCountry country, CGoodsPool stockpile)
	{
		CFixedPoint crudeOil = CrudeOilToRefine(country, stockpile);
		if (crudeOil <= CFixedPoint.Zero)
		{
			return CFixedPoint.Zero;
		}

		// The technology and the modifier multiply each other here; each step drops what
		// is left below a thousandth.
		CFixedPoint fuel = crudeOil * (CFixedPoint.One + country.Technology[TechnologyRefining]);
		fuel *= CFixedPoint.One + country.Modifier[FuelConversion];

		stockpile[EGoods.CrudeOil] -= crudeOil;
		country.ConversionUsed[EGoods.CrudeOil] += crudeOil;
		country.ConversionMade[EGoods.Fuel] += fuel;
		return fuel;
	}
}
