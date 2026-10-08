using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// A country's manpower: what a year brings it, what a day adds to the pool, what a new
/// game starts it with, and the figure the original calls its most. See
/// <c>docs/mechanics/manpower.md</c>.
///
/// Only the income is here. What spends manpower - building units, and bringing them
/// back to strength - waits for units.
/// </summary>
public static class CManpower
{
	private const string LocalManpower = "local_manpower";
	private const string LocalManpowerModifier = "local_manpower_modifier";
	private const string GlobalManpower = "global_manpower";
	private const string GlobalManpowerModifier = "global_manpower_modifier";
	private const string TechnologyManpower = "manpower_gain";

	/// <summary>The define that says how many years of manpower a country's most is.</summary>
	public const string MilitarySection = "military";
	public const string MaxManpower = "MAX_MANPOWER";

	private const int MonthsPerYear = 12;

	/// <summary>No country's most is less than this, whatever it holds.</summary>
	private static readonly CFixedPoint LeastMost = CFixedPoint.One;

	/// <summary>
	/// What a country at war is short of when a game begins: a year of its manpower, and
	/// this much of a year again for each month the war has lasted, in thousandths. It is
	/// built into the original.
	/// </summary>
	private const long YearsLostPerMonthOfWar = 120;

	private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

	/// <summary>
	/// What a year brings each country, in thousandths, by tag: what the provinces it
	/// controls give, each scaled by its own modifier and its owner's laws, plus what
	/// every country is given, all scaled by the country's technology.
	/// </summary>
	private static Dictionary<string, long> InAYear(CGameState state)
	{
		var sums = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is not { } controller)
			{
				continue;
			}

			long manpower = province.Manpower.Raw + province.Modifier[LocalManpower].Raw;

			// The law that counts is the owner's, whoever holds the province.
			long factor = CFixedPoint.Scale + province.Modifier[LocalManpowerModifier].Raw;
			if (state.Country(province.Owner) is { } owner)
			{
				factor += owner.Modifier[GlobalManpowerModifier].Raw;
			}
			if (factor < 0)
			{
				factor = 0;                                  // a province cannot be made worth less than nothing
			}

			long contribution = manpower * factor / CFixedPoint.Scale;
			if (contribution <= 0)
			{
				continue;
			}
			sums.TryGetValue(controller.Value, out long sum);
			sums[controller.Value] = sum + contribution;
		}

		var inAYear = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (CCountry country in state.Countries)
		{
			sums.TryGetValue(country.Tag.Value, out long sum);

			// What base_values gives every country is added whole: no law scales it.
			sum += country.Modifier[GlobalManpower].Raw;
			inAYear[country.Tag.Value] = sum * (CFixedPoint.Scale + country.Technology[TechnologyManpower].Raw) / CFixedPoint.Scale;
		}
		return inAYear;
	}

	/// <summary>Works out what a month brings every country, a twelfth of its year, and moves nothing.</summary>
	public static void Update(CGameState state)
	{
		Dictionary<string, long> inAYear = InAYear(state);
		foreach (CCountry country in state.Countries)
		{
			country.MonthlyManpower = new CFixedPoint(inAYear[country.Tag.Value] / MonthsPerYear);
		}
	}

	/// <summary>
	/// Works out every country's most: so many years of what it gets, the define says how
	/// many. The original does this once a month and on entering a game. Nothing holds the
	/// pool to it.
	/// </summary>
	public static void UpdateMost(CGameState state)
	{
		long years = state.Defines[MilitarySection, MaxManpower].Raw;
		Dictionary<string, long> inAYear = InAYear(state);
		foreach (CCountry country in state.Countries)
		{
			long most = inAYear[country.Tag.Value] * years / CFixedPoint.Scale;
			country.MaxManpower = most < LeastMost.Raw ? LeastMost : new CFixedPoint(most);
		}
	}

	/// <summary>What today adds to the pool: the month's figure over the days the month has.</summary>
	public static CFixedPoint MadeToday(CGameState state, CCountry country)
	{
		int days = DaysInMonth[Math.Clamp(state.Date.Month, 1, MonthsPerYear) - 1];
		return new CFixedPoint(country.MonthlyManpower.Raw / days);
	}

	/// <summary>One day of every country's manpower. A day that would take some away adds nothing.</summary>
	public static void RunDaily(CGameState state)
	{
		Update(state);
		foreach (CCountry country in state.Countries)
		{
			CFixedPoint made = MadeToday(state, country);
			if (made > CFixedPoint.Zero)
			{
				country.Manpower += made;
			}
		}
	}

	/// <summary>
	/// What a country at war is short of its most when a game begins. Nothing for one at
	/// peace, or one that owns no province.
	/// </summary>
	public static CFixedPoint LostToWarAtStart(CGameState state, CCountry country, bool ownsLand)
	{
		if (!ownsLand || !state.IsAtWar(country.Tag))
		{
			return CFixedPoint.Zero;
		}
		long years = state.Defines[MilitarySection, MaxManpower].Raw;
		long inAYear = years == 0 ? -1 : country.MaxManpower.Raw * CFixedPoint.Scale / years;
		long months = (long)CStockpile.MonthsAtWar(state, country) * CFixedPoint.Scale;
		long share = months * YearsLostPerMonthOfWar / CFixedPoint.Scale + CFixedPoint.Scale;
		return new CFixedPoint(share * inAYear / CFixedPoint.Scale);
	}

	/// <summary>
	/// The manpower a new game starts a country with: its most, less what a war it is in
	/// has already cost it, and never less than nothing.
	/// </summary>
	public static CFixedPoint AtStart(CGameState state, CCountry country, bool ownsLand)
	{
		CFixedPoint manpower = country.MaxManpower - LostToWarAtStart(state, country, ownsLand);
		return manpower < CFixedPoint.Zero ? CFixedPoint.Zero : manpower;
	}

	/// <summary>The tags of the countries that own at least one province.</summary>
	public static HashSet<string> Landowners(CGameState state)
	{
		var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner)
			{
				owners.Add(owner.Value);
			}
		}
		return owners;
	}

	/// <summary>
	/// Gives every country the manpower a new game starts with. Its most is worked out
	/// for this before its strategic resources count, as the original's is.
	/// </summary>
	public static void SetAtStart(CGameState state)
	{
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state, strategicResources: false);
		UpdateMost(state);

		HashSet<string> owners = Landowners(state);
		foreach (CCountry country in state.Countries)
		{
			country.Manpower = AtStart(state, country, owners.Contains(country.Tag.Value));
		}

		CCountryModifiers.Rebuild(state);
		UpdateMost(state);
		Update(state);
	}
}
