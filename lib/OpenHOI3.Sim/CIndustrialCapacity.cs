using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

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
	private const string ProvinceIc = "ic";
	private const string ProvinceLocalIc = "local_ic";
	private const string CountryGlobalIc = "global_ic";
	private const string TechnologyIc = "ic_modifier";

	/// <summary>The static modifier every country's province sum starts from, and the kind read from it.</summary>
	public const string BaseValues = "base_values";
	public const string BaseValuesIc = "ic";

	public static void Update(CGameState state)
	{
		var sums = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
		long seed = state.StaticModifier(BaseValues, BaseValuesIc).Raw;

		foreach (CProvince province in state.Provinces)
		{
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
			if (contribution <= 0)
			{
				continue;                                    // only a positive one is added
			}
			sums.TryGetValue(controller.Value, out long sum);
			sums[controller.Value] = sum + contribution;
		}

		foreach (CCountry country in state.Countries)
		{
			sums.TryGetValue(country.Tag.Value, out long sum);

			// The sum does not start at nothing: every country has base_values' IC before
			// any province is counted.
			sum += seed;

			// The fraction is dropped here, before the country's bonuses apply, so it
			// never reaches the total.
			long baseIc = sum / CFixedPoint.Scale;

			// 1 + GLOBAL_IC + the technology bonus, which is kept beside the modifier
			// rather than in it.
			long multiplier = CFixedPoint.Scale + country.Modifier[CountryGlobalIc].Raw
				+ country.Technology[TechnologyIc].Raw;
			long total = baseIc * CFixedPoint.Scale * multiplier / CFixedPoint.Scale / CFixedPoint.Scale;
			if (total < 1)
			{
				total = 1;                                   // every country has at least one
			}

			country.BaseIc = (int)baseIc;
			country.TotalIc = (int)total;
		}
	}

}
