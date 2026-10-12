using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects that pick members - the <c>any_*</c> and <c>random_*</c> blocks and a region - and
/// <c>random</c> and <c>random_list</c>, each moving the state of chance the way the original's
/// does: <c>docs/mechanics/effects.md</c>, section 2.
/// </summary>
public static class CEffectSets
{
	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope, CScriptReport? report)
	{
		CCountry? country = state.Country(scope.Country);
		switch (effect.Keyword)
		{
			case "any_owned":
				EachFresh(state, effect, scope, Owned(state, scope.Country), report);
				return true;
			case "any_controlled":
				EachFresh(state, effect, scope, Controlled(state, scope.Country), report);
				return true;
			case "any_country":
				EachCountry(state, effect, scope, state.Countries, skipOwn: true, report);
				return true;
			case "any_neighbor_country":
				EachCountry(state, effect, scope, country is null ? new List<CCountry>() : CCountryLists.Neighbours(state, country),
					skipOwn: true, report);
				return true;
			case "any_neighbor_province":
				EachProvince(state, effect, scope, OwnedNeighbours(state, scope.Province), report);
				return true;
			case "any_nearby_province":
				EachProvince(state, effect, scope, Nearby(state, effect, scope.Province), report);
				return true;
			case "random_country":
			{
				var candidates = new List<CCountryTag>();
				foreach (CCountry other in state.Countries)
				{
					if (other.Tag != scope.Country && CCountryLists.OwnedCount(state, other.Tag) > 0
						&& CEffects.Passes(state, effect, scope.With(other.Tag, scope.Province), report))
					{
						candidates.Add(other.Tag);
					}
				}
				if (candidates.Count > 0)
				{
					CCountryTag drawn = candidates[scope.Draw(candidates.Count)];
					CEffects.Run(state, effect.Children, scope.With(drawn, scope.Province), report);
				}
				return true;
			}
			case "random_owned":
				OneOf(state, effect, scope, Owned(state, scope.Country), report);
				return true;
			case "random_neighbor_province":
				OneOf(state, effect, scope, OwnedNeighbours(state, scope.Province), report);
				return true;
			case "random_empty_neighbor_province":
				OneOf(state, effect, scope, Provinces(state, effect, scope), report);
				return true;
			case "random":
			{
				// The state moves whether the effects run or not.
				long chance = CConditions.WholeOf(CEffects.Field(effect, "chance")?.Amount ?? CFixedPoint.Zero);
				if (scope.Draw(100) < chance)
				{
					CEffects.Run(state, effect.Children, scope, report);
				}
				return true;
			}
			case "random_list":
				RandomList(state, effect, scope, report);
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// The provinces an <c>any_*</c> or <c>random_*</c> block of provinces goes over, before its
	/// <c>limit</c>: what a tooltip of the block lists, the same as the block does.
	/// </summary>
	public static List<int> Provinces(CGameState state, CEffect effect, CEventScope scope)
	{
		switch (effect.Keyword)
		{
			case "any_owned":
			case "random_owned":
				return Owned(state, scope.Country);
			case "any_controlled":
				return Controlled(state, scope.Country);
			case "any_neighbor_province":
			case "random_neighbor_province":
				return OwnedNeighbours(state, scope.Province);
			case "any_nearby_province":
				return Nearby(state, effect, scope.Province);
			case "random_empty_neighbor_province":
			{
				var empty = new List<int>();
				foreach (CProvinceEdge edge in state.Province(scope.Province).Edges)
				{
					CProvince beside = state.Province(edge.To);
					if (!beside.IsSea && beside.Owner is null)
					{
						empty.Add(edge.To);
					}
				}
				return empty;
			}
			default:
				return new List<int>();
		}
	}

	/// <summary>A region's provinces in its file's order, anyone's, one copy of the scope for all.</summary>
	public static void Region(CGameState state, CEffect effect, CEventScope scope, CScriptReport? report)
	{
		foreach (CRegion region in state.Regions)
		{
			if (region.Name.Equals(effect.Key, StringComparison.Ordinal))
			{
				EachProvince(state, effect, scope, region.Provinces, report);
				return;
			}
		}
	}

	/// <summary>Each province in a fresh copy of the scope, so every one starts from the same state of chance.</summary>
	private static void EachFresh(CGameState state, CEffect effect, CEventScope scope, List<int> provinces, CScriptReport? report)
	{
		foreach (int province in provinces)
		{
			CEventScope member = scope.With(scope.Country, province);
			if (CEffects.Passes(state, effect, member, report))
			{
				CEffects.Run(state, effect.Children, member, report);
			}
		}
	}

	/// <summary>Each province with one copy of the scope for all: a draw in one carries into the next.</summary>
	private static void EachProvince(CGameState state, CEffect effect, CEventScope scope, IReadOnlyList<int> provinces,
		CScriptReport? report)
	{
		uint carried = scope.RandomState;
		foreach (int province in provinces)
		{
			CEventScope member = scope.With(scope.Country, province);
			member.RandomState = carried;
			if (CEffects.Passes(state, effect, member, report))
			{
				CEffects.Run(state, effect.Children, member, report);
			}
			carried = member.RandomState;
		}
	}

	/// <summary>Each country with one copy of the scope for all, the province kept; its own is asked, then skipped.</summary>
	private static void EachCountry(CGameState state, CEffect effect, CEventScope scope, IReadOnlyList<CCountry> countries,
		bool skipOwn, CScriptReport? report)
	{
		uint carried = scope.RandomState;
		foreach (CCountry other in countries)
		{
			CEventScope member = scope.With(other.Tag, scope.Province);
			member.RandomState = carried;
			bool passes = CEffects.Passes(state, effect, member, report);
			if (passes && !(skipOwn && other.Tag == scope.Country))
			{
				CEffects.Run(state, effect.Children, member, report);
			}
			carried = member.RandomState;
		}
	}

	/// <summary>One province drawn from those the limit lets through; the draw moves the scope outside.</summary>
	private static void OneOf(CGameState state, CEffect effect, CEventScope scope, List<int> provinces, CScriptReport? report)
	{
		var candidates = new List<int>();
		foreach (int province in provinces)
		{
			if (CEffects.Passes(state, effect, scope.With(scope.Country, province), report))
			{
				candidates.Add(province);
			}
		}
		if (candidates.Count > 0)
		{
			int drawn = candidates[scope.Draw(candidates.Count)];
			CEffects.Run(state, effect.Children, scope.With(scope.Country, drawn), report);
		}
	}

	/// <summary>
	/// The first entry whose running sum the roll does not pass: the first has one chance more
	/// than its weight, the last one fewer.
	/// </summary>
	private static void RandomList(CGameState state, CEffect effect, CEventScope scope, CScriptReport? report)
	{
		long total = 0;
		foreach (CEffect entry in effect.Children)
		{
			total += CConditions.WholeOf(entry.Argument.Amount);
		}
		if (total <= 0)
		{
			return;
		}
		long roll = scope.Draw((int)total);
		foreach (CEffect entry in effect.Children)
		{
			roll -= CConditions.WholeOf(entry.Argument.Amount);
			if (roll <= 0)
			{
				CEffects.Run(state, entry.Children, scope, report);
				return;
			}
		}
	}

	private static List<int> Owned(CGameState state, CCountryTag? country)
	{
		var owned = new List<int>();
		if (country is null)
		{
			return owned;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == country)
			{
				owned.Add(province.Id);
			}
		}
		return owned;
	}

	private static List<int> Controlled(CGameState state, CCountryTag? country)
	{
		var controlled = new List<int>();
		if (country is null)
		{
			return controlled;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == country)
			{
				controlled.Add(province.Id);
			}
		}
		return controlled;
	}

	/// <summary>The province's neighbours that have an owner, in the order of its edges.</summary>
	private static List<int> OwnedNeighbours(CGameState state, int province)
	{
		var owned = new List<int>();
		foreach (CProvinceEdge edge in state.Province(province).Edges)
		{
			if (state.Province(edge.To).Owner is not null)
			{
				owned.Add(edge.To);
			}
		}
		return owned;
	}

	/// <summary>
	/// The provinces the province's controller controls within <c>distance</c>, the province itself
	/// among them: four kilometres to a pixel between centres, the short way round the world.
	/// </summary>
	private static List<int> Nearby(CGameState state, CEffect effect, int province)
	{
		CProvince here = state.Province(province);
		long distance = CConditions.WholeOf(CEffects.Field(effect, "distance")?.Amount ?? CFixedPoint.Zero);
		var near = new List<int>();
		foreach (int id in Controlled(state, here.Controller))
		{
			CProvince other = state.Province(id);
			long dx = Math.Abs(other.CentreX - here.CentreX);
			if (state.MapWidth > 0 && dx > state.MapWidth / 2)
			{
				dx = state.MapWidth - dx;
			}
			long dy = other.CentreY - here.CentreY;
			if (16 * (dx * dx + dy * dy) <= distance * distance)
			{
				near.Add(id);
			}
		}
		return near;
	}
}
