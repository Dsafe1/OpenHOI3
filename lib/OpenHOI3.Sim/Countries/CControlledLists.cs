using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The provinces each country controls, in the order of its list of them (<see cref="CCountryLists.Controlled"/>), each
/// country's gathered the first time it is asked for and kept until a province changes hands - which is the only time a
/// controller or a place in the list changes in play; a loaded save, which sets the places afresh, drops them
/// (<c>CSaveLoader</c>). The scripts ask for a country's provinces a hundred and fifty times an hour under BlackICE, and
/// each walk of the map and sort was a tenth of a millisecond.
///
/// The scripts ask side by side, on several Lua states at once (<c>CAiWorkers</c>), so the lists are kept for being
/// asked from several threads: two that make one country's list at once make the same list, and either is kept.
/// </summary>
public sealed class CControlledLists
{
	private readonly ConcurrentDictionary<CCountryTag, List<CProvince>> _lists = new();

	private CControlledLists(int controlChanges, int provinceCount)
	{
		ControlChanges = controlChanges;
		ProvinceCount = provinceCount;
	}

	private int ControlChanges { get; }

	private int ProvinceCount { get; }

	/// <summary>The country's provinces in its list's order. Never changed once made: a change of hands makes them again.</summary>
	public static IReadOnlyList<CProvince> Of(CGameState state, CCountryTag tag)
	{
		if (state.ControlledLists is not { } lists || lists.ControlChanges != state.ControlChanges
			|| lists.ProvinceCount != state.Provinces.Count)
		{
			state.ControlledLists = lists = new CControlledLists(state.ControlChanges, state.Provinces.Count);
		}
		return lists._lists.GetOrAdd(tag, Gather, state);
	}

	private static List<CProvince> Gather(CCountryTag tag, CGameState state)
	{
		var list = new List<CProvince>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == tag)
			{
				list.Add(province);
			}
		}
		CCountryLists.SortByControlOrder(list);
		return list;
	}
}
