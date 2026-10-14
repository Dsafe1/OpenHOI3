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
	public const string Neutrality = "neutrality";

	public static void Rebuild(CGameState state)
	{
		Dictionary<string, List<CProvince>> fromProvinces = ProvincesHeld(state);
		foreach (CCountry country in state.Countries)
		{
			RebuildOne(state, country, fromProvinces);
		}
	}

	/// <summary>One country's modifier made again, as a decision taken does.</summary>
	public static void RebuildOne(CGameState state, CCountry country)
	{
		RebuildOne(state, country, ProvincesHeld(state));
	}

	private static void RebuildOne(CGameState state, CCountry country, Dictionary<string, List<CProvince>> fromProvinces)
	{
		CModifier? baseValues = state.FindStaticModifier(CIndustrialCapacity.BaseValues);
		CModifier? war = state.FindStaticModifier(War);
		CModifier? peace = state.FindStaticModifier(Peace);
		CModifier? dissent = state.FindStaticModifier(Dissent);
		Rebuild(country, baseValues, Diplomacy.CAlignment.FactionGroup(state, country)?.FactionModifier);
		// Its government, after its laws (step 5).
		if (state.GovernmentOf(country) is { } government)
		{
			AddSource(country, government.Modifier);
		}

		// A resource is worth its modifier times how much of it the country has.
		foreach ((string name, CFixedPoint level) in country.StrategicResources)
		{
			if (level > CFixedPoint.Zero && state.FindStrategicResource(name) is { } resource)
			{
				AddSource(country, Scaled(resource, level));
			}
		}

		// Everything the provinces it controls have, the local kinds along with the rest.
		if (fromProvinces.TryGetValue(country.Tag.Value, out List<CProvince>? provinces))
		{
			foreach (CProvince province in provinces)
			{
				AddSource(country, province.Modifier, province.Name);
			}
		}

		if ((state.IsAtWar(country.Tag) ? war : peace) is { } standing)
		{
			AddSource(country, standing);
		}

		// Other countries' spies lowering or raising its national unity (step 9; espionage.md, section 10).
		foreach (CModifier spies in Espionage.CSpyModifiers.On(state, country))
		{
			AddSource(country, spies);
		}

		// What the file gives for dissent is for one point of it.
		if (dissent is not null)
		{
			AddSource(country, Scaled(dissent, country.Dissent));
		}

		// Neutrality's, by its neutrality less the threat from the country it fears most, over a
		// hundred (step 12; docs/mechanics/diplomacy.md, section 6).
		if (state.FindStaticModifier(Neutrality) is { } neutrality)
		{
			CFixedPoint threat = state.Country(country.MostThreatening) is { } feared
				? CGameState.Status(country, feared).Threat
				: CFixedPoint.Zero;
			long effective = Math.Max(country.Neutrality.Raw - threat.Raw, 0);
			AddSource(country, Scaled(neutrality, new CFixedPoint(effective * 1000 / 100000)));
		}

		// What events gave it, each until its time has passed (step 13).
		country.EventModifiers.RemoveAll(given => given.HasEnded(state));
		foreach (CTimedModifier given in country.EventModifiers)
		{
			AddSource(country, given.Modifier);
		}

		// A fractured government, where a party of another group than the ruling party's holds a seat (step 18;
		// politics.md, section 4).
		if (Politics.CCabinet.IsFractured(state, country) && state.FindStaticModifier(Politics.CCabinet.Fractured) is { } fractured)
		{
			AddSource(country, fractured);
		}

		RebuildTechnology(state, country);
	}

	/// <summary>The provinces each country controls that have a modifier at all, by tag, in their order.</summary>
	private static Dictionary<string, List<CProvince>> ProvincesHeld(CGameState state)
	{
		var held = new Dictionary<string, List<CProvince>>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is not { } controller || province.Modifier.Values.Count == 0)
			{
				continue;
			}
			if (!held.TryGetValue(controller.Value, out List<CProvince>? list))
			{
				held[controller.Value] = list = new List<CProvince>();
			}
			list.Add(province);
		}
		return held;
	}

	/// <summary>
	/// A copy of a modifier with every value multiplied by a factor, each product dropping
	/// what does not fit: what the original adds for a strategic resource and for dissent.
	/// </summary>
	public static CModifier Scaled(CModifier modifier, CFixedPoint factor)
	{
		var copy = new CModifier(modifier.Name);
		foreach ((string kind, CFixedPoint value) in modifier.Values)
		{
			copy.Set(kind, new CFixedPoint(value.Raw * factor.Raw / CFixedPoint.Scale));
		}
		return copy;
	}

	/// <summary>Adds a source to the country's modifier and keeps it as one.</summary>
	private static void AddSource(CCountry country, CModifier source, string? heading = null)
	{
		country.Modifier.Add(source);
		country.ModifierSources.Add(new CModifierSource(source, heading));
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
	/// The part of a country's modifier that is its own: the base values, its faction's modifier, its ministers
	/// and its laws.
	/// </summary>
	/// <param name="baseValues">
	/// The static modifier every country starts from. It is where a country's demand for
	/// consumer goods begins, before its laws take theirs off.
	/// </param>
	public static void Rebuild(CCountry country, CModifier? baseValues = null, CModifier? faction = null)
	{
		country.Modifier.Clear();
		country.ModifierSources.Clear();
		if (baseValues is not null)
		{
			AddSource(country, baseValues);
		}
		// Its faction's modifier, where it is in one (factions.md, section 1).
		if (faction is not null)
		{
			AddSource(country, faction);
		}

		foreach ((string position, CMinister minister) in country.Government)
		{
			if (minister.TypeIn(position) is { } type)
			{
				AddSource(country, type.Modifier);
			}
		}
		foreach (CLaw law in country.Laws.Values)
		{
			AddSource(country, law.Modifier);
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
