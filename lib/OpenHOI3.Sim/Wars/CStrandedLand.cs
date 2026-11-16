using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// Land in no area handed to the enemy beside it, in each province's hour: an owned province with one level of
/// infrastructure or none, whose holder has no ground of its own or a friend's beside it, goes to the enemy that holds the
/// ground around it. See <c>docs/mechanics/war.md</c>, section 11.
/// </summary>
public static class CStrandedLand
{
	/// <summary>
	/// The original's middle of <c>RunHourlyProvincePass</c>. Nothing for the sea, land nobody owns, or a province in a
	/// real area. Otherwise its land neighbours are walked in the order of its ways out, the impassable ones left out:
	/// <list type="bullet">
	/// <item>where any is in a real area, the province goes to the holder of the last such neighbour that is an enemy of
	/// its holder - none where its holder is the rebels - unless one such neighbour's ground is ground its holder may
	/// operate in, its own among it, which keeps it where it is;</item>
	/// <item>where none is, it goes to the enemy holding the first of them, if every enemy among them is that one, and
	/// stays where any of them is held by its holder's side, or by a country fighting beside it that is no enemy.</item>
	/// </list>
	/// The change has its consequences and is peaceful. Answers whether the province changed hands.
	/// </summary>
	public static bool Run(CGameState state, CProvince province)
	{
		if (province.IsSea || province.Owner is null || state.Country(province.Controller) is not { } holder)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (network.AreaOf(province.Id) is not null)
		{
			return false;
		}
		CCountryTag? taker = null;
		var outside = new List<CProvince>();
		int land = 0;
		bool friendly = false;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince neighbour = state.Province(edge.To);
			if (neighbour.IsSea)
			{
				continue;
			}
			land++;
			if (network.AreaOf(neighbour.Id) is null)
			{
				outside.Add(neighbour);
				continue;
			}
			if (state.Country(neighbour.Controller) is { } other && CSupplyRelations.CanOperateIn(state, holder, other))
			{
				friendly = true;
				break;
			}
			if (CMovementRules.IsEnemy(state, holder.Tag, neighbour.Controller) && !CRebelFactions.IsRebels(holder.Tag))
			{
				taker = neighbour.Controller;
			}
		}
		if (outside.Count != land)
		{
			if (friendly)
			{
				return false;
			}
		}
		else
		{
			taker = AllOutside(state, holder, outside);
		}
		if (taker is not { } to)
		{
			return false;
		}
		CProvinceHands.SetController(state, province, to, consequences: true, peaceful: true);
		return province.Controller == to;
	}

	/// <summary>
	/// Where no land neighbour is in a real area: the enemy holding the first of them, if every enemy among them is that
	/// one and none of them is held by the holder's side or by a country fighting beside it that is no enemy of it.
	/// </summary>
	private static CCountryTag? AllOutside(CGameState state, CCountry holder, List<CProvince> outside)
	{
		CCountryTag? first = outside.Count > 0 ? outside[0].Controller : null;
		CCountryTag? taker = null;
		foreach (CProvince neighbour in outside)
		{
			CCountryTag? tag = neighbour.Controller;
			if (!CRebelFactions.IsRebels(tag) && state.Country(tag) is { } other)
			{
				if (CSupplyRelations.IsSameSide(holder, other))
				{
					return null;
				}
				if (CSupplyRelations.IsFightingWith(state, holder, other) && !CMovementRules.IsEnemy(state, holder.Tag, tag))
				{
					return null;
				}
			}
			if (CMovementRules.IsEnemy(state, holder.Tag, tag))
			{
				taker = tag;
				if (tag != first)
				{
					return null;
				}
			}
		}
		return taker;
	}
}
