using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The provinces each country holds - their controller - in the provinces' own order, gathered in one walk of the map
/// and kept until a province changes hands. What runs once a day for every country over the provinces it holds - the
/// roll for a rising, home production - walked the whole map for each, a hundred countries over fourteen thousand
/// provinces; <see cref="Walk"/> gives the same provinces in the same order from here.
/// </summary>
public sealed class CHeldProvinces
{
	private static readonly IReadOnlyList<int> None = Array.Empty<int>();

	/// <summary>Each holder's provinces by its tag, as tags compare: their places in the state's list, in order.</summary>
	private readonly Dictionary<CCountryTag, List<int>> _byTag = new();

	/// <summary>The same by the tag's letters matched without case, as a few readers of the map match them.</summary>
	private readonly Dictionary<string, List<int>> _byLetters = new(StringComparer.OrdinalIgnoreCase);

	private CHeldProvinces(int controlChanges, int provinceCount)
	{
		ControlChanges = controlChanges;
		ProvinceCount = provinceCount;
	}

	private int ControlChanges { get; }

	private int ProvinceCount { get; }

	/// <summary>The provinces for the state as it stands, gathered again where a province has changed hands.</summary>
	public static CHeldProvinces Of(CGameState state)
	{
		if (state.HeldProvinces is { } held && held.ControlChanges == state.ControlChanges
			&& held.ProvinceCount == state.Provinces.Count)
		{
			return held;
		}
		held = new CHeldProvinces(state.ControlChanges, state.Provinces.Count);
		for (int place = 0; place < state.Provinces.Count; place++)
		{
			if (state.Provinces[place].Controller is not { } controller)
			{
				continue;
			}
			if (!held._byTag.TryGetValue(controller, out List<int>? list))
			{
				held._byTag[controller] = list = new List<int>();
			}
			list.Add(place);
			if (controller.Value is { } letters)
			{
				if (!held._byLetters.TryGetValue(letters, out List<int>? anyCase))
				{
					held._byLetters[letters] = anyCase = new List<int>();
				}
				anyCase.Add(place);
			}
		}
		state.HeldProvinces = held;
		return held;
	}

	/// <summary>
	/// Every province of the state whose controller is the country's tag - compared as tags compare, or with
	/// <paramref name="ignoringCase"/> by its letters matched without case - in the state's order, as a walk of the whole
	/// map testing each would give them. Where a province changes hands while the walk goes on, it goes on over the rest of
	/// the map testing each as that walk does, so what the walk's own work changes is seen just as it would have been.
	/// </summary>
	public static IEnumerable<CProvince> Walk(CGameState state, CCountryTag country, bool ignoringCase = false)
	{
		int changes = state.ControlChanges;
		CHeldProvinces held = Of(state);
		IReadOnlyList<int> places = ignoringCase
			? (country.Value is { } letters && held._byLetters.TryGetValue(letters, out List<int>? anyCase) ? anyCase : None)
			: (held._byTag.TryGetValue(country, out List<int>? exact) ? exact : None);
		int next = 0;
		for (int i = 0; i < places.Count; i++)
		{
			if (state.ControlChanges != changes)
			{
				break;
			}
			next = places[i] + 1;
			yield return state.Provinces[places[i]];
		}
		if (state.ControlChanges == changes)
		{
			yield break;
		}
		for (int place = next; place < state.Provinces.Count; place++)
		{
			CProvince province = state.Provinces[place];
			if (province.Controller is { } controller && (ignoringCase
				? string.Equals(controller.Value, country.Value, StringComparison.OrdinalIgnoreCase)
				: controller == country))
			{
				yield return province;
			}
		}
	}
}
