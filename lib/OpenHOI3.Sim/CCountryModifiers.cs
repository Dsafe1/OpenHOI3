using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// Rebuilds every country's modifier from what currently applies to it: the base values
/// every country shares, its laws, the ministers in its government, each of whom
/// counts as the minister type he has for the seat he holds, and the strategic
/// resources in the provinces it controls. See
/// <c>docs/mechanics/modifiers.md</c>, sections 4 and 5.
///
/// The total is cleared and summed again every time. Nothing is ever added to it and
/// taken away later, so there is nothing to forget.
/// </summary>
public static class CCountryModifiers
{
	/// <param name="strategicResources">
	/// False leaves the strategic resources out, which is how the original has a country
	/// when it works out the manpower a new game starts it with.
	/// </param>
	public static void Rebuild(CGameState state, bool strategicResources = true)
	{
		CModifier? baseValues = state.FindStaticModifier(CIndustrialCapacity.BaseValues);
		Dictionary<string, SortedSet<string>> resources = strategicResources
			? StrategicResourcesHeld(state)
			: new Dictionary<string, SortedSet<string>>();
		foreach (CCountry country in state.Countries)
		{
			Rebuild(country, baseValues);
			if (resources.TryGetValue(country.Tag.Value, out SortedSet<string>? held))
			{
				foreach (string name in held)
				{
					if (state.FindStrategicResource(name) is { } resource)
					{
						country.Modifier.Add(resource);
					}
				}
			}
			RebuildTechnology(state, country);
		}
	}

	/// <summary>
	/// The strategic resources each country has, by tag: those in a province it controls.
	/// A resource counts once however many provinces of it a country holds.
	/// </summary>
	private static Dictionary<string, SortedSet<string>> StrategicResourcesHeld(CGameState state)
	{
		var held = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.StrategicResource.Length == 0 || province.Controller is not { } controller)
			{
				continue;
			}
			if (!held.TryGetValue(controller.Value, out SortedSet<string>? names))
			{
				names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
				held[controller.Value] = names;
			}
			names.Add(province.StrategicResource);
		}
		return held;
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

		foreach (CLaw law in country.Laws.Values)
		{
			total.Add(law.Modifier);
		}
		foreach ((string position, CMinister minister) in country.Government)
		{
			if (minister.TypeIn(position) is { } type)
			{
				total.Add(type);
			}
		}
	}
}
