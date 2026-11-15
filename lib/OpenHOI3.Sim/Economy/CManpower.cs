using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Economy;

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
	/// What a province gives in a year: its manpower and its <c>local_manpower</c>, times one and its
	/// <c>local_manpower_modifier</c> and its owner's <c>global_manpower_modifier</c> - the owner's law, whoever holds
	/// it - never below nothing. Also what the province view shows (<c>provinceview.md</c>, section 2).
	/// </summary>
	public static CFixedPoint OfProvince(CGameState state, CProvince province)
	{
		return OfProvince(province, state.Country(province.Owner)?.Modifier[GlobalManpowerModifier] ?? CFixedPoint.Zero);
	}

	/// <summary>As <see cref="OfProvince(CGameState, CProvince)"/>, given the owner's <c>global_manpower_modifier</c>.</summary>
	private static CFixedPoint OfProvince(CProvince province, CFixedPoint ownersModifier)
	{
		long manpower = province.Manpower.Raw + province.Modifier[LocalManpower].Raw;
		long factor = CFixedPoint.Scale + province.Modifier[LocalManpowerModifier].Raw + ownersModifier.Raw;
		if (factor < 0)
		{
			factor = 0;                                      // a province cannot be made worth less than nothing
		}
		return new CFixedPoint(Math.Max(0, manpower * factor / CFixedPoint.Scale));
	}

	/// <summary>
	/// What a year brings each country, in thousandths, by tag: what the provinces it
	/// controls give, each scaled by its own modifier and its owner's laws, plus what
	/// every country is given, all scaled by the country's technology.
	/// </summary>
	/// <param name="setUpBefore">
	/// A new game's set-up only: each country's <c>global_manpower_modifier</c> before its own set-up moved its laws. The
	/// original sets the countries up one after another, each working out its most in its turn, so a province owned by a
	/// country later in the order is weighed by that country's law as its history left it (<c>manpower.md</c>, section 4).
	/// </param>
	private static Dictionary<string, long> InAYear(CGameState state, IReadOnlyDictionary<string, CFixedPoint>? setUpBefore = null)
	{
		Dictionary<string, int>? order = null;
		if (setUpBefore is not null)
		{
			order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (CCountry country in state.Countries)
			{
				order[country.Tag.Value] = order.Count;
			}
		}
		var sums = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is not { } controller)
			{
				continue;
			}

			long contribution = setUpBefore is not null && order is not null && province.Owner is { } owner
				&& order.TryGetValue(owner.Value, out int owners) && order.TryGetValue(controller.Value, out int controllers)
				&& owners > controllers && setUpBefore.TryGetValue(owner.Value, out CFixedPoint before)
				? OfProvince(province, before).Raw
				: OfProvince(state, province).Raw;
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
		UpdateMost(state, null);
	}

	private static void UpdateMost(CGameState state, IReadOnlyDictionary<string, CFixedPoint>? setUpBefore)
	{
		long years = state.Defines[MilitarySection, MaxManpower].Raw;
		Dictionary<string, long> inAYear = InAYear(state, setUpBefore);
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
	/// Each country's <c>global_manpower_modifier</c> by tag, its modifier made again first: taken as a new game is
	/// entered, before any country's set-up brings its laws forward, for <see cref="SetAtStart"/>.
	/// </summary>
	public static Dictionary<string, CFixedPoint> BeforeSetUp(CGameState state)
	{
		CCountryModifiers.Rebuild(state);
		var factors = new Dictionary<string, CFixedPoint>(StringComparer.OrdinalIgnoreCase);
		foreach (CCountry country in state.Countries)
		{
			factors[country.Tag.Value] = country.Modifier[GlobalManpowerModifier];
		}
		return factors;
	}

	/// <summary>
	/// Gives every country the manpower a new game starts with. No country has the use
	/// of a strategic resource yet: those are first worked out when the first day is run.
	/// </summary>
	/// <param name="setUpBefore">
	/// A new game only: what <see cref="BeforeSetUp"/> gave before the laws were brought forward, so that each country's
	/// most is worked out in its turn, with a later country's land weighed by its law as its history left it.
	/// </param>
	public static void SetAtStart(CGameState state, IReadOnlyDictionary<string, CFixedPoint>? setUpBefore = null)
	{
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state);
		UpdateMost(state, setUpBefore);
		Update(state);

		HashSet<string> owners = Landowners(state);
		foreach (CCountry country in state.Countries)
		{
			country.Manpower = AtStart(state, country, owners.Contains(country.Tag.Value));
		}
	}
}
