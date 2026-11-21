using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The original's <c>RunDailyRebelFactionPass</c>: each faction's day, after every province's, a faction that asks to
/// go taken away at once. See <c>docs/mechanics/rebels.md</c>, section 9.
/// </summary>
public static class CRebelFactionDay
{
	/// <summary>A type both reinforcing and smart stays put below this much strength a brigade, in thousandths.</summary>
	public const long LeastStrength = 660;

	/// <summary>What a province not beside the army scores of what it would beside it, in thousandths.</summary>
	public const long FarScore = 500;

	public static void RunDaily(CGameState state)
	{
		for (int index = 0; index < state.RebelFactions.Count; index++)
		{
			CRebelFaction faction = state.RebelFactions[index];
			Run(state, faction);
			if (faction.WantsRemoval)
			{
				state.RebelFactions.RemoveAt(index);
				index--;
			}
		}
	}

	/// <summary>One faction's day, in the original's order.</summary>
	public static void Run(CGameState state, CRebelFaction faction)
	{
		CResilientRetreat.Run(state, faction);
		if (CCountryLists.OwnedCount(state, faction.Country) <= 0 && !faction.Type.AnyArea)
		{
			faction.WantsRemoval = true;
			return;
		}
		if (faction.Independence is { } freed && CCountryLists.OwnedCount(state, freed) > 0)
		{
			faction.WantsRemoval = true;
			return;
		}
		HandBack(state, faction);
		ClearArmies(state, faction);
		if (faction.Armies.Count == 0 && faction.Provinces.Count == 0)
		{
			faction.WantsRemoval = true;
			return;
		}
		March(state, faction);
	}

	/// <summary>
	/// Each province on the list the rebels still hold, owned, whose owner the faction counts a friend, goes back to
	/// its owner with a capture's consequences; one the rebels do not hold, or unowned, comes off the list.
	/// </summary>
	private static void HandBack(CGameState state, CRebelFaction faction)
	{
		var kept = new List<CProvince>();
		foreach (int id in new List<int>(faction.Provinces))
		{
			CProvince province = state.Province(id);
			if (!CRebelFactions.IsRebels(province.Controller) || province.Owner is null)
			{
				faction.Provinces.Remove(id);
			}
			else if (CRebelFactions.IsFriendly(state, faction, province.Owner))
			{
				kept.Add(province);
			}
		}
		foreach (CProvince province in kept)
		{
			if (province.Owner is { } owner)
			{
				CProvinceHands.SetController(state, province, owner, consequences: true);
			}
		}
	}

	/// <summary>An army that is gone, or is no longer the rebels', comes off the list.</summary>
	private static void ClearArmies(CGameState state, CRebelFaction faction)
	{
		faction.Armies.RemoveAll(id => CRebelFactions.FindUnit(state, id) is not { } unit || !CRebelFactions.IsRebels(unit.Owner));
	}

	/// <summary>
	/// Each army not moving, not falling back and not fighting goes where its type scores highest; with nowhere, and
	/// standing on ground it does not want, home where home is wanted - and where it is not and home's holder is a
	/// friend, it is disbanded. Its way is cut after the first province in reach that is not a friend's.
	/// </summary>
	private static void March(CGameState state, CRebelFaction faction)
	{
		if (state.Country(CRebelFactions.Tag) is not { } rebels)
		{
			return;
		}
		foreach (int id in new List<int>(faction.Armies))
		{
			if (CRebelFactions.FindUnit(state, id) is not { } unit || unit.IsMoving || unit.IsRetreating || unit.IsFighting)
			{
				continue;
			}
			bool idle = !CRebelFactions.IsHostile(state, faction, state.Province(unit.Location));
			CProvince? target = ChooseTarget(state, faction, unit);
			if (target is null && idle)
			{
				CProvince home = state.Province(faction.Province);
				if (CRebelFactions.IsHostile(state, faction, home))
				{
					target = home;
				}
				else if (CRebelFactions.IsFriendly(state, faction, home.Controller))
				{
					faction.Armies.Remove(id);
					Units.CPendingUnitRemovals.Remove(state, unit);
					continue;
				}
			}
			if (target is null || CRouteFinder.Find(state, rebels, unit, unit.Location, target.Id) is not { Count: > 0 } route)
			{
				continue;
			}
			CMovement.SetPath(unit, Cut(state, faction, route), clear: true);
		}
	}

	/// <summary>The way, up to and with the first province in reach whose owner or holder is not a friend; else its first.</summary>
	public static List<int> Cut(CGameState state, CRebelFaction faction, List<int> route)
	{
		for (int index = 0; index < route.Count; index++)
		{
			CProvince province = state.Province(route[index]);
			if (CRebelFactions.InReach(faction, province)
				&& !(CRebelFactions.IsFriendly(state, faction, province.Owner) && CRebelFactions.IsFriendly(state, faction, province.Controller)))
			{
				return route.GetRange(0, index + 1);
			}
		}
		return route.GetRange(0, 1);
	}

	/// <summary>
	/// The original's <c>RebelFaction_ChooseMoveTarget</c>: a type both reinforcing and smart stays where it is short of
	/// strength; otherwise, of the provinces of the area the army stands in and then of each area touching it, in order,
	/// the wanted one scoring strictly highest above nought on the type's <c>movement_evaluation</c> - half that where it
	/// is not beside the army - in a scope of the province with no country, seeded by one draw.
	/// </summary>
	public static CProvince? ChooseTarget(CGameState state, CRebelFaction faction, CUnit unit)
	{
		CRebelType type = faction.Type;
		if (type.Reinforcing && type.Smart && unit.SubUnits.Count > 0)
		{
			long strength = 0;
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				strength += brigade.Strength.Raw;
			}
			if (strength / unit.SubUnits.Count < LeastStrength)
			{
				return null;
			}
		}
		uint seed = (uint)state.Random.Next();
		if (CRebelFactions.AreaOf(state, unit.Location) is not { } area)
		{
			return null;
		}
		CProvince here = state.Province(unit.Location);
		CProvince? chosen = null;
		long best = 0;
		void Weigh(int id)
		{
			CProvince candidate = state.Province(id);
			if (!CRebelFactions.IsHostile(state, faction, candidate))
			{
				return;
			}
			CEventScope scope = CEventScope.ForRebels(null, id, seed);
			long score = CEventChance.Days(state, type.MovementEvaluation, scope);
			if (here.EdgeTo(id) is null)
			{
				score = score * FarScore / 1000;
			}
			if (score > best)
			{
				best = score;
				chosen = candidate;
			}
		}
		foreach (int id in area.Provinces)
		{
			Weigh(id);
		}
		foreach (CSupplyArea next in area.Neighbours)
		{
			foreach (int id in next.Provinces)
			{
				Weigh(id);
			}
		}
		return chosen;
	}
}
