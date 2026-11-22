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

		// The day's flows of each, as the fifteen pools hold them so far: the usage was cleared at the head of the day and
		// nothing of either is used yet, the conversions' own pools were cleared just now, today's home production is in.
		long energyNet = CDailyLedger.Net(country, EGoods.Energy);
		long crudeNet = CDailyLedger.Net(country, EGoods.CrudeOil);
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

	/// <summary>
	/// Whether a country's own setting decides a conversion (<c>docs/mechanics/ic.md</c>, section 4.1): in an enhanced
	/// game, for a country a person plays that has taken the conversion into its own hands. Everywhere else - a
	/// faithful game, a country nobody plays, a conversion left alone - the original's rule decides.
	/// </summary>
	public static bool IsOwn(CGameState state, CCountry country, EConversion conversion)
	{
		return state.Rules.Enhanced && state.Played.Contains(country.Tag) && country.Conversion(conversion).Own;
	}

	/// <summary>The energy one crude oil costs in a country's own conversion, in thousandths: to the nearest, and never under one.</summary>
	private static long OwnEnergyPerOil(long rate)
	{
		return Math.Max((CFixedPoint.Scale * CFixedPoint.Scale + rate / 2) / rate, CFixedPoint.Scale);
	}

	/// <summary>
	/// The most energy a country may turn into crude oil in a day whoever decides: the original's most,
	/// <c>max(0.05 x IC, 1)</c> crude oil, as the energy that costs - nought where the rate is nought or below, when
	/// nothing converts at all.
	/// </summary>
	public static CFixedPoint EnergyCapacity(CCountry country)
	{
		long rate = OilRate(country).Raw;
		if (rate <= 0)
		{
			return CFixedPoint.Zero;
		}
		return new CFixedPoint(Math.Max(country.TotalIc * MostOilPerIc.Raw, CFixedPoint.Scale) * OwnEnergyPerOil(rate) / CFixedPoint.Scale);
	}

	/// <summary>The energy industry needs in a day, which is never converted: twice the IC.</summary>
	public static CFixedPoint EnergyFloor(CCountry country) => CFixedPoint.FromInt(country.TotalIc * 2);

	/// <summary>The most crude oil a country may refine in a day whoever decides: half its IC, in whole units.</summary>
	public static CFixedPoint CrudeOilCapacity(CCountry country) => CFixedPoint.FromInt(country.TotalIc / 2);

	/// <summary>
	/// The energy a country that sets the conversion itself turns into crude oil today: what it chose to spend a day,
	/// cut to its capacity and to what it holds above the higher of industry's need and what it chose to keep; nothing
	/// once it holds the crude oil it chose to stop at, and nothing at a rate of nought or below. The original's "only
	/// towards twice as much energy as crude oil" is not asked of it.
	/// </summary>
	public static CFixedPoint OwnEnergyToConvert(CCountry country, CGoodsPool stockpile)
	{
		CConversionSetting setting = country.EnergyConversion;
		if (setting.Target.Raw > 0 && stockpile[EGoods.CrudeOil] >= setting.Target)
		{
			return CFixedPoint.Zero;
		}
		long floor = Math.Max(EnergyFloor(country).Raw, setting.Keep.Raw);
		long spare = stockpile[EGoods.Energy].Raw - floor;
		if (spare <= 0 || OilRate(country).Raw <= 0)
		{
			return CFixedPoint.Zero;
		}
		long spend = Math.Min(Math.Max(setting.PerDay.Raw, 0), Math.Min(EnergyCapacity(country).Raw, spare));
		return new CFixedPoint(Math.Max(spend, 0));
	}

	/// <summary>
	/// The crude oil a country that sets the conversion itself refines today: what it chose a day, cut to its capacity
	/// and to what it holds above what it chose to keep; nothing at 99,000 fuel, the original's stop, nor once it holds
	/// the fuel it chose to stop at. The original's second stop - a year of the crude oil coming in and twice the crude
	/// oil held - is not asked of it.
	/// </summary>
	public static CFixedPoint OwnCrudeOilToRefine(CCountry country, CGoodsPool stockpile)
	{
		CConversionSetting setting = country.CrudeOilConversion;
		CFixedPoint fuel = stockpile[EGoods.Fuel];
		if (fuel >= MostFuel || (setting.Target.Raw > 0 && fuel >= setting.Target))
		{
			return CFixedPoint.Zero;
		}
		long held = stockpile[EGoods.CrudeOil].Raw - Math.Max(setting.Keep.Raw, 0);
		long spend = Math.Min(Math.Max(setting.PerDay.Raw, 0), Math.Min(CrudeOilCapacity(country).Raw, held));
		return new CFixedPoint(Math.Max(spend, 0));
	}

	/// <summary>The day's energy into crude oil, by the country's own setting where that decides and by the original's rule where not.</summary>
	public static void EnergyIntoCrudeOil(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		if (IsOwn(state, country, EConversion.EnergyIntoCrudeOil))
		{
			Convert(country, stockpile, OwnEnergyToConvert(country, stockpile));
		}
		else
		{
			EnergyIntoCrudeOil(country, stockpile);
		}
	}

	public static void EnergyIntoCrudeOil(CCountry country, CGoodsPool stockpile)
	{
		Convert(country, stockpile, EnergyToConvert(country, stockpile));
	}

	/// <summary>An amount of energy turned into crude oil at the country's rate, and written to the day's two ledgers.</summary>
	private static void Convert(CCountry country, CGoodsPool stockpile, CFixedPoint energy)
	{
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

		CFixedPoint comingIn = new(CDailyLedger.Income(country, EGoods.CrudeOil));
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
		stockpile[EGoods.Fuel] += Refine(country, stockpile, CrudeOilToRefine(country, stockpile));
	}

	/// <summary>
	/// Refines the day's crude oil out of the stockpile, and the fuel is made where the
	/// factories are, as supplies are: see <see cref="Supply.CSupplyMaking"/>.
	/// </summary>
	public static void CrudeOilIntoFuel(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		// How much is the country's own setting's where that decides, and the original's rule's where not; what becomes
		// of it - the yield, the ledgers, where the fuel is made - is the same either way.
		CFixedPoint fuel = Refine(country, stockpile, IsOwn(state, country, EConversion.CrudeOilIntoFuel)
			? OwnCrudeOilToRefine(country, stockpile)
			: CrudeOilToRefine(country, stockpile));
		if (fuel > CFixedPoint.Zero)
		{
			Supply.CSupplyMaking.ShareOut(state, country, stockpile, EGoods.Fuel, fuel);
		}
	}

	/// <summary>Takes an amount of crude oil out of the stockpile and answers the fuel it makes.</summary>
	private static CFixedPoint Refine(CCountry country, CGoodsPool stockpile, CFixedPoint crudeOil)
	{
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
