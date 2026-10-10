using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// Rebuilds every country's modifier from what currently applies to it, in the
/// original's order: the base values every country shares, the ministers in its
/// government, each of whom counts as the minister type he has for the seat he holds,
/// its laws, the strategic resources it has the use of, the modifier of every province
/// it controls, the modifier for being at war or at peace, and the one for its dissent.
/// See <c>docs/mechanics/modifiers.md</c>, sections 4 and 7.
///
/// The total is cleared and summed again every time. Nothing is ever added to it and
/// taken away later, so there is nothing to forget.
/// </summary>
public static class CCountryModifiers
{
	/// <summary>The static modifiers the rebuild asks for by name.</summary>
	public const string War = "war";
	public const string Peace = "peace";
	public const string Dissent = "dissent";

	public static void Rebuild(CGameState state)
	{
		CModifier? baseValues = state.FindStaticModifier(CIndustrialCapacity.BaseValues);
		CModifier? war = state.FindStaticModifier(War);
		CModifier? peace = state.FindStaticModifier(Peace);
		CModifier? dissent = state.FindStaticModifier(Dissent);
		Dictionary<string, CModifier> fromProvinces = ProvinceModifiersHeld(state);

		foreach (CCountry country in state.Countries)
		{
			Rebuild(country, baseValues);
			CModifier total = country.Modifier;

			// A resource is worth its modifier times how much of it the country has.
			foreach ((string name, CFixedPoint level) in country.StrategicResources)
			{
				if (level > CFixedPoint.Zero && state.FindStrategicResource(name) is { } resource)
				{
					AddScaled(total, resource, level);
				}
			}

			// Everything the provinces it controls have, the local kinds along with the rest.
			if (fromProvinces.TryGetValue(country.Tag.Value, out CModifier? provinces))
			{
				total.Add(provinces);
			}

			if ((state.IsAtWar(country.Tag) ? war : peace) is { } standing)
			{
				total.Add(standing);
			}

			// What the file gives for dissent is for one point of it.
			if (dissent is not null)
			{
				AddScaled(total, dissent, country.Dissent);
			}

			RebuildTechnology(state, country);
		}
	}

	/// <summary>The modifiers of the provinces each country controls, summed, by tag.</summary>
	private static Dictionary<string, CModifier> ProvinceModifiersHeld(CGameState state)
	{
		var held = new Dictionary<string, CModifier>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is not { } controller || province.Modifier.Values.Count == 0)
			{
				continue;
			}
			if (!held.TryGetValue(controller.Value, out CModifier? sum))
			{
				held[controller.Value] = sum = new CModifier();
			}
			sum.Add(province.Modifier);
		}
		return held;
	}

	/// <summary>Adds a modifier with every value multiplied by a factor, each product dropping what does not fit.</summary>
	private static void AddScaled(CModifier total, CModifier modifier, CFixedPoint factor)
	{
		foreach ((string kind, CFixedPoint value) in modifier.Values)
		{
			total.Add(kind, new CFixedPoint(value.Raw * factor.Raw / CFixedPoint.Scale));
		}
	}

	/// <summary>Each technology's effect, times the level the country has of it.</summary>
	public static void RebuildTechnology(CGameState state, CCountry country)
	{
		CModifier total = country.Technology;
		total.Clear();
		foreach ((string name, int level) in country.TechnologyLevels)
		{
			if (level <= 0 || !state.Technologies.TryGetValue(name, out CTechnology? technology))
			{
				continue;
			}
			foreach ((string kind, CFixedPoint value) in technology.EffectPerLevel.Values)
			{
				total.Add(kind, value * level);
			}
		}
	}

	/// <summary>
	/// The part of a country's modifier that is its own: the base values, its ministers
	/// and its laws.
	/// </summary>
	/// <param name="baseValues">
	/// The static modifier every country starts from. It is where a country's demand for
	/// consumer goods begins, before its laws take theirs off.
	/// </param>
	public static void Rebuild(CCountry country, CModifier? baseValues = null)
	{
		CModifier total = country.Modifier;
		total.Clear();
		if (baseValues is not null)
		{
			total.Add(baseValues);
		}

		foreach ((string position, CMinister minister) in country.Government)
		{
			if (minister.TypeIn(position) is { } type)
			{
				total.Add(type.Modifier);
			}
		}
		foreach (CLaw law in country.Laws.Values)
		{
			total.Add(law.Modifier);
		}
	}

	/// <summary>
	/// What the ministers in a country's government add to one kind of modifier, in
	/// thousandths: each counts as the type he has for the seat he holds.
	/// </summary>
	public static long MinisterShare(CCountry country, string kind)
	{
		long total = 0;
		foreach ((string position, CMinister minister) in country.Government)
		{
			if (minister.TypeIn(position) is { } type)
			{
				total += type.Modifier[kind].Raw;
			}
		}
		return total;
	}

	/// <summary>
	/// What the ministers in a country's government add to a technology category's decay,
	/// in thousandths: each counts as the type he has for the seat he holds.
	/// </summary>
	public static long MinisterDecay(CCountry country, string category)
	{
		long total = 0;
		foreach ((string position, CMinister minister) in country.Government)
		{
			if (minister.TypeIn(position) is { } type && type.Decay.TryGetValue(category, out CFixedPoint amount))
			{
				total += amount.Raw;
			}
		}
		return total;
	}
}
