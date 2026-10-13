using System.Collections.Generic;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// What the map says of each country, worked out in one walk of the provinces and kept for the hour while
/// no province changes hands: how many provinces it owns, which countries are its land neighbours - as
/// <see cref="CCountryLists.Neighbours"/> has them - and whether it has a harbour of its own. The AI's
/// trading asks these thousands of times an hour (<c>docs/mechanics/trade.md</c>, section 9).
/// </summary>
public sealed class CCountryFacts
{
	private readonly Dictionary<CCountryTag, int> _owned = new();
	private readonly Dictionary<CCountryTag, HashSet<CCountryTag>> _neighbours = new();
	private readonly HashSet<CCountryTag> _harbours = new();
	private readonly Dictionary<CCountryTag, HashSet<CCountryTag>> _controllerNeighbours = new();

	private CCountryFacts(long hour, int controlChanges, int provinceCount)
	{
		Hour = hour;
		ControlChanges = controlChanges;
		ProvinceCount = provinceCount;
	}

	private long Hour { get; }

	private int ControlChanges { get; }

	private int ProvinceCount { get; }

	/// <summary>The facts for the state as it stands, worked out again where the hour or the map has moved on.</summary>
	public static CCountryFacts Of(CGameState state)
	{
		if (state.CountryFacts is { } facts && facts.Hour == state.TotalHours && facts.ControlChanges == state.ControlChanges
			&& facts.ProvinceCount == state.Provinces.Count)
		{
			return facts;
		}
		facts = new CCountryFacts(state.TotalHours, state.ControlChanges, state.Provinces.Count);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is { } holder)
			{
				foreach (CProvinceEdge edge in province.Edges)
				{
					CProvince beside = state.Province(edge.To);
					if (beside.Owner is { } owned && owned != holder && beside.Controller is { } controller && controller != holder)
					{
						if (!facts._controllerNeighbours.TryGetValue(holder, out HashSet<CCountryTag>? set))
						{
							facts._controllerNeighbours[holder] = set = new HashSet<CCountryTag>();
						}
						set.Add(controller);
					}
				}
			}
			if (province.Owner is not { } owner)
			{
				continue;
			}
			facts._owned[owner] = facts._owned.GetValueOrDefault(owner) + 1;
			if (CConvoyRules.IsHarbour(state, province))
			{
				facts._harbours.Add(owner);
			}
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (beside.Owner is { } other && beside.Controller == other && other != owner)
				{
					if (!facts._neighbours.TryGetValue(owner, out HashSet<CCountryTag>? set))
					{
						facts._neighbours[owner] = set = new HashSet<CCountryTag>();
					}
					set.Add(other);
				}
			}
		}
		state.CountryFacts = facts;
		return facts;
	}

	public int OwnedCount(CCountryTag country) => _owned.GetValueOrDefault(country);

	public bool AreNeighbours(CCountryTag country, CCountryTag other) =>
		_neighbours.TryGetValue(country, out HashSet<CCountryTag>? set) && set.Contains(other);

	public bool HasHarbour(CCountryTag country) => _harbours.Contains(country);

	/// <summary>Whether the other holds land beside the country's, as <see cref="CCountryLists.ControllerNeighbours"/> has it.</summary>
	public bool AreControllerNeighbours(CCountryTag country, CCountryTag other) =>
		_controllerNeighbours.TryGetValue(country, out HashSet<CCountryTag>? set) && set.Contains(other);
}
