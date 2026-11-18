using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The provinces each country owns, in the provinces' order, gathered in one walk of the provinces and
/// kept until one changes hands. How many is asked for every country every hour - whether it still
/// exists - and counting afresh each time was a seventh of a game's time; which, by the scripts'
/// <c>any_owned_province</c>, a hundred thousand times a day under BlackICE's triggered modifiers.
/// </summary>
public sealed class COwnedCounts
{
	private readonly Dictionary<CCountryTag, List<CProvince>> _owned = new();

	private COwnedCounts(int controlChanges, int provinceCount)
	{
		ControlChanges = controlChanges;
		ProvinceCount = provinceCount;
	}

	private int ControlChanges { get; }

	private int ProvinceCount { get; }

	/// <summary>The provinces for the state as it stands, gathered again where a province has changed hands.</summary>
	public static COwnedCounts Of(CGameState state)
	{
		if (state.OwnedCounts is { } counts && counts.ControlChanges == state.ControlChanges
			&& counts.ProvinceCount == state.Provinces.Count)
		{
			return counts;
		}
		counts = new COwnedCounts(state.ControlChanges, state.Provinces.Count);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner)
			{
				if (!counts._owned.TryGetValue(owner, out List<CProvince>? list))
				{
					counts._owned[owner] = list = new List<CProvince>();
				}
				list.Add(province);
			}
		}
		state.OwnedCounts = counts;
		return counts;
	}

	public int Count(CCountryTag country) => _owned.TryGetValue(country, out List<CProvince>? list) ? list.Count : 0;

	/// <summary>
	/// The provinces the country owns, in the provinces' order. Never changed once gathered - a change of hands gathers a
	/// new set - so it may be walked while what is asked of each province is answered.
	/// </summary>
	public IReadOnlyList<CProvince> Provinces(CCountryTag country) =>
		_owned.TryGetValue(country, out List<CProvince>? list) ? list : Array.Empty<CProvince>();
}
