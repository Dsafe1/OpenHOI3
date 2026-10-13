using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// How many provinces each country owns, counted in one walk of the provinces and kept until one
/// changes hands. It is asked for every country every hour - whether it still exists - and counting
/// afresh each time was a seventh of a game's time.
/// </summary>
public sealed class COwnedCounts
{
	private readonly Dictionary<CCountryTag, int> _owned = new();

	private COwnedCounts(int controlChanges, int provinceCount)
	{
		ControlChanges = controlChanges;
		ProvinceCount = provinceCount;
	}

	private int ControlChanges { get; }

	private int ProvinceCount { get; }

	/// <summary>The counts for the state as it stands, counted again where a province has changed hands.</summary>
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
				counts._owned[owner] = counts._owned.GetValueOrDefault(owner) + 1;
			}
		}
		state.OwnedCounts = counts;
		return counts;
	}

	public int Count(CCountryTag country) => _owned.GetValueOrDefault(country);
}
