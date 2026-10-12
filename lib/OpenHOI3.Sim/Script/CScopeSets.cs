using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The blocks asked over a set - a country's provinces, a province's neighbours, a country's
/// neighbours, a province's cores, a region's provinces - each true at the first member in whose
/// scope every condition holds, and false for an empty set: <c>docs/mechanics/triggers.md</c>,
/// section 3.
/// </summary>
public static class CScopeSets
{
	public static bool OwnedProvince(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		if (scope.Country is not { } country)
		{
			return false;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == country && CConditions.All(state, block, scope.With(scope.Country, province.Id), report))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The province's neighbours that have an owner, in the order of its edges.</summary>
	public static bool NeighbourProvince(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		foreach (CProvinceEdge edge in CConditions.Here(state, scope).Edges)
		{
			if (state.Province(edge.To).Owner is not null
				&& CConditions.All(state, block, scope.With(scope.Country, edge.To), report))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The country's neighbours, the province kept.</summary>
	public static bool NeighbourCountry(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		if (state.Country(scope.Country) is not { } country)
		{
			return false;
		}
		foreach (CCountry neighbour in CCountryLists.Neighbours(state, country))
		{
			if (CConditions.All(state, block, scope.With(neighbour.Tag, scope.Province), report))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The countries with a core on the province, the province kept.</summary>
	public static bool Core(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		foreach (CCountryTag core in CConditions.Here(state, scope).Cores.ToArray())
		{
			if (CConditions.All(state, block, scope.With(core, scope.Province), report))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The region's provinces, in its file's order, the country kept.</summary>
	public static bool Region(CGameState state, CTrigger block, CEventScope scope, CScriptReport? report)
	{
		foreach (CRegion region in state.Regions)
		{
			if (!region.Name.Equals(block.Key, StringComparison.Ordinal))
			{
				continue;
			}
			foreach (int province in region.Provinces)
			{
				if (CConditions.All(state, block, scope.With(scope.Country, province), report))
				{
					return true;
				}
			}
			return false;
		}
		return false;
	}
}
