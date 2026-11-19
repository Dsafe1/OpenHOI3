using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// Works out every country's IC, as the original does once a day and once on entering a
/// game. The formula and what each step rests on are in <c>docs/mechanics/ic.md</c>,
/// section 3.
///
/// What follows the total in the original is elsewhere: the conversions in
/// <see cref="CConversions"/> and the cap a shortage imposes in <see cref="CProduction"/>.
/// A province's IC is read from its modifier, which has to be current: see
/// <see cref="CProvinceModifiers"/>. The country's own bonus is whatever its modifier
/// holds for <c>global_ic</c>, which today is its laws and ministers, plus what its
/// technologies give it.
/// </summary>
public static class CIndustrialCapacity
{
	public const string ProvinceIc = "ic";
	public const string ProvinceLocalIc = "local_ic";
	private const string CountryGlobalIc = "global_ic";
	public const string TechnologyIc = "ic_modifier";

	/// <summary>The static modifier every country's province sum starts from, and the kind read from it.</summary>
	public const string BaseValues = "base_values";
	public const string BaseValuesIc = "ic";

	public static void Update(CGameState state)
	{
		Update(state, setUp: false);
	}

	/// <summary>
	/// The calculation a new game's set-up makes at its head, for every country, its ministers all counted: those its
	/// history named, the filling of empty posts coming later. It marks every country's IC worked out
	/// (<see cref="CCountry.IcWorkedOut"/>), which a country that owns nothing then keeps (<c>ic.md</c>, section 3).
	/// </summary>
	public static void UpdateAtSetUp(CGameState state)
	{
		Update(state, setUp: true);
	}

	private static void Update(CGameState state, bool setUp)
	{
		CDay day = Prepare(state);
		foreach (CCountry country in state.Countries)
		{
			Apply(state, day, country, setUp);
		}
	}

	/// <summary>What the day's IC is worked out from, over every province at once: the sums by controller.</summary>
	public sealed class CDay
	{
		public readonly Dictionary<string, long> Sums = new(StringComparer.OrdinalIgnoreCase);
		public readonly Dictionary<string, long> CoreHome = new(StringComparer.OrdinalIgnoreCase);
		public readonly HashSet<string> Owners = new(StringComparer.OrdinalIgnoreCase);
		public long Seed;
	}

	/// <summary>The provinces summed by controller, once a day before the first country's day.</summary>
	public static CDay Prepare(CGameState state)
	{
		var day = new CDay();
		var sums = day.Sums;
		var coreHome = day.CoreHome;
		var owners = day.Owners;
		day.Seed = state.StaticModifier(BaseValues, BaseValuesIc).Raw;
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);

		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner)
			{
				owners.Add(owner.Value);
			}
			// The provinces a country controls. Whether the original walks those or the
			// ones it owns has not been established.
			if (province.Controller is not { } controller)
			{
				continue;
			}

			long ic = province.Modifier[ProvinceIc].Raw;
			if (ic == 0)
			{
				continue;
			}
			long localIc = province.Modifier[ProvinceLocalIc].Raw;
			long contribution = ic * (CFixedPoint.Scale + localIc) / CFixedPoint.Scale;

			// Its own ground at home is added up beside the sum: a province it owns and
			// has a core on, in the area its capital is in.
			if (province.Owner == controller && province.Cores.Contains(controller)
				&& state.Country(controller) is { ActingCapital: > 0 } holder
				&& network.AreaOf(province.Id) is { } area && area == network.AreaOf(holder.ActingCapital))
			{
				coreHome.TryGetValue(controller.Value, out long home);
				coreHome[controller.Value] = home + contribution;
			}

			if (contribution <= 0)
			{
				continue;                                    // only a positive one is added
			}
			sums.TryGetValue(controller.Value, out long sum);
			sums[controller.Value] = sum + contribution;
		}

		return day;
	}

	/// <summary>One country's IC worked out from the day's sums, in its own day (<c>docs/mechanics/tick.md</c>, section 3).</summary>
	public static void Apply(CGameState state, CDay day, CCountry country, bool setUp = false)
	{
		day.Sums.TryGetValue(country.Tag.Value, out long sum);

		// The sum does not start at nothing: every country has base_values' IC before
		// any province is counted.
		sum += day.Seed;

		// The fraction is dropped here, before the country's bonuses apply, so it
		// never reaches the total.
		long baseIc = sum / CFixedPoint.Scale;

		day.CoreHome.TryGetValue(country.Tag.Value, out long home);
		country.CoreHomeIc = home;
		country.BaseIc = (int)baseIc;

		// A country that owns no province and is in no exile has no day of its own in which its IC is worked out
		// again: it keeps the figure it last had - for one that never owned land in the game, the one its set-up
		// gave it, its history's ministers counted and the filled posts' not (ic.md, section 3).
		bool daily = day.Owners.Contains(country.Tag.Value) || country.InExile;
		if (!setUp && !daily && country.IcWorkedOut)
		{
			country.LendLeaseIc = 0;
			FillResourcesNeeded(country, country.IcBeforeCap);
			return;
		}

		// 1 + GLOBAL_IC + the technology bonus, which is kept beside the modifier
		// rather than in it.
		long multiplier = CFixedPoint.Scale + country.Modifier[CountryGlobalIc].Raw
			+ country.Technology[TechnologyIc].Raw;
		// Where nothing has worked a landless country's IC out - a loaded game, the original saving no IC - it is
		// worked out without its ministers, which a country that does not exist at a new game's start had none of
		// when its set-up worked it out. They count in everything else.
		if (!setUp && !daily && !country.IcWorkedOut)
		{
			multiplier -= Modifiers.CCountryModifiers.MinisterShare(country, CountryGlobalIc);
		}
		long total = baseIc * CFixedPoint.Scale * multiplier / CFixedPoint.Scale / CFixedPoint.Scale;
		if (total < 1)
		{
			total = 1;                                   // every country has at least one
		}

		country.TotalIc = (int)total;
		country.IcBeforeCap = (int)total;
		if (setUp)
		{
			country.IcWorkedOut = true;
		}
		// What lend-lease adds comes later in the day, after the resource cap (docs/mechanics/aid.md, section 3).
		country.LendLeaseIc = 0;
		// What a day of that IC would cost, before the cap and the lend-lease, as the original's IC fills it.
		FillResourcesNeeded(country, (int)total);
	}

	/// <summary>
	/// What a full day of so much IC would cost in resources, which the economy ledger shows as "Used by Industry": the
	/// pool emptied, then the IC times 1.000 of metal, 2.000 of energy and 0.500 of rare materials, the last cut toward
	/// nought. The IC is the total as worked out, before a shortage cuts it and lend-lease is added - the original
	/// fills the pool between the two (<c>docs/mechanics/ic.md</c>, section 4).
	/// </summary>
	public static void FillResourcesNeeded(CCountry country, int ic)
	{
		country.ResourcesNeeded.Clear();
		long thousandths = (long)ic * CFixedPoint.Scale;
		country.ResourcesNeeded[EGoods.Metal] = new CFixedPoint(thousandths);
		country.ResourcesNeeded[EGoods.Energy] = new CFixedPoint(ic * 2000L);
		country.ResourcesNeeded[EGoods.RareMaterials] = new CFixedPoint(thousandths * CFixedPoint.Scale / 2000);
	}

}
