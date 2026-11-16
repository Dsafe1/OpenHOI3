using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A country annexed by another, the original's <c>AnnexCountry</c> in the mode <c>inherit</c> and a capitulation use.
/// See <c>docs/mechanics/war.md</c>, section 13.
/// </summary>
public static class CAnnexation
{
	private const string Rebels = "REB";

	/// <summary>How far a country's threat falls towards one annexed, and the annexed's towards it: never below nought.</summary>
	private static readonly CFixedPoint ThreatDropped = CFixedPoint.FromInt(9999);

	/// <summary>
	/// The annexed country is taken over, in the original's order: everybody is told <c>ANNEX</c>; it leaves its
	/// faction; each province it owns goes to whoever holds it, or to the annexer where it holds it itself or the
	/// rebels do, with consequences; its building queue is deleted; every unit it has is taken out of the game; the
	/// land it held of others goes back to their owners; and every other country leaves its wars with it, ends its
	/// alliance, bond and guarantee with it and its trade routes, has its threat both ways fall by 9999, and forgets
	/// it - NAP, embargo, influence, alignment, relation, access, debt, truce, casus belli and occupation policy. Nothing marks the
	/// country dead: it keeps its stockpile, manpower, technology, leaders and capital.
	///
	/// <paramref name="surrender"/> is the mode of <c>surrender_inherit</c> and the console's <c>surrender</c>: the
	/// annexed country's partners at war with the annexer are candidates, a province the annexer takes is held by one of
	/// them holding land beside it, and a unit standing with one goes over to it on an even draw (<see cref="Candidates"/>,
	/// <see cref="CandidateBeside"/>, <see cref="CandidateOf"/>; <c>docs/mechanics/war.md</c>, section 13).
	/// </summary>
	public static void Annex(CGameState state, CCountry annexer, CCountry annexed, bool surrender = false)
	{
		state.RaisePactMessage(new CPactMessage("ANNEX", "ANNEX", "ANNEX", annexer.Tag, annexed.Tag));
		List<CCountry> candidates = surrender ? Candidates(state, annexer, annexed) : new List<CCountry>();
		if (COfferRules.RealFaction(annexed))
		{
			CFactionMembership.Leave(state, annexed);
		}
		// The areas as they stood before the first province changed hands: the original rebuilds none inside the walk.
		Supply.CSupplyNetwork? areas = candidates.Count > 0 ? Supply.CSupplyNetworks.Refresh(state) : null;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != annexed.Tag)
			{
				continue;
			}
			if (province.Controller is { } holder && holder != annexed.Tag && holder.Value != Rebels)
			{
				CProvinceHands.ChangeOwner(state, province, holder, consequences: true);
				continue;
			}
			CCountry? beside = areas is null ? null : CandidateBeside(state, areas, province, candidates);
			CProvinceHands.ChangeOwner(state, province, annexer.Tag, consequences: true);
			if (beside is not null)
			{
				CProvinceHands.SetController(state, province, beside.Tag, consequences: true);
			}
		}
		annexed.Constructions.Clear();
		CExpeditions.ReturnAll(state, annexed);
		foreach (CUnit unit in annexed.UnitsInOrder())
		{
			if (unit.IsRemoved || unit.Owner != annexed.Tag)
			{
				continue;
			}
			// A unit with no candidate takes no draw; one with a candidate goes over on an even draw. Every other unit goes
			// as the original's RemoveUnit takes it: its officers lost, what it commanded left at the top (war.md, section 13).
			if (candidates.Count > 0 && CandidateOf(state, unit, candidates) is { } to && (state.Random.Next() & 1) == 0)
			{
				GoOver(state, annexed, unit, to);
			}
			else
			{
				CPendingUnitRemovals.Remove(state, unit);
			}
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == annexed.Tag && state.Country(province.Owner) is { } owner && owner != annexed)
			{
				// An exile's land goes to the head of its faction.
				CCountryTag to = owner.InExile && COfferRules.Leader(state, owner.Faction) is { } leader ? leader : owner.Tag;
				CProvinceHands.SetController(state, province, to, consequences: true);
			}
		}
		foreach (CCountry other in state.Countries)
		{
			if (other != annexed)
			{
				Forget(state, other, annexed);
			}
		}
	}

	/// <summary>
	/// The candidates of a surrender: the annexed country's <c>non_hostile_countries</c> - those fighting beside it and not
	/// at war with it, the rebels never, in the countries' order - but the annexer, that are at war with the annexer, or
	/// where the annexer is the rebels. The original's list is the day's; this one is made as it stands.
	/// </summary>
	public static List<CCountry> Candidates(CGameState state, CCountry annexer, CCountry annexed)
	{
		var candidates = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (other == annexer || other == annexed || other.Tag.Value == Rebels || annexed.Tag.Value == Rebels
				|| !CCountryLists.FightingTogether(state, annexed.Tag, other.Tag) || CCountryLists.AtWar(state, annexed.Tag, other.Tag))
			{
				continue;
			}
			if (CCountryLists.AtWar(state, annexer.Tag, other.Tag) || annexer.Tag.Value == Rebels)
			{
				candidates.Add(other);
			}
		}
		return candidates;
	}

	/// <summary>
	/// Who holds a province the annexer takes in a surrender: of the areas beside the province's own area, in their order,
	/// each held by the controller of its first province as it stands now - so control handed out earlier in the walk counts
	/// - those held by a candidate; one, that one; several, the one whose area comes nearest the province, the first of
	/// equals. None where no candidate holds an area beside it, or the province is in no area.
	/// </summary>
	public static CCountry? CandidateBeside(CGameState state, Supply.CSupplyNetwork areas, CProvince province, List<CCountry> candidates)
	{
		if (areas.AreaOf(province.Id) is not { } area)
		{
			return null;
		}
		var held = new List<CCountry>();
		foreach (Supply.CSupplyArea beside in area.Neighbours)
		{
			if (Holder(state, beside, candidates) is { } candidate && !held.Contains(candidate))
			{
				held.Add(candidate);
			}
		}
		if (held.Count <= 1)
		{
			return held.Count == 1 ? held[0] : null;
		}
		CCountry? nearest = null;
		long best = 1_000_000;
		foreach (Supply.CSupplyArea beside in area.Neighbours)
		{
			if (Holder(state, beside, candidates) is not { } candidate)
			{
				continue;
			}
			long distance = DistanceTo(state, beside, province);
			if (distance < best)
			{
				best = distance;
				nearest = candidate;
			}
		}
		return nearest;
	}

	/// <summary>The candidate holding an area - its first province's controller - or null where nobody of them does.</summary>
	private static CCountry? Holder(CGameState state, Supply.CSupplyArea area, List<CCountry> candidates)
	{
		if (area.Provinces.Count == 0 || state.Province(area.Provinces[0]).Controller is not { } controller)
		{
			return null;
		}
		return candidates.Find(candidate => candidate.Tag == controller);
	}

	/// <summary>The original's <c>COwnerArea_DistanceToProvince</c>: the nearest of the area's provinces, from a million.</summary>
	private static long DistanceTo(CGameState state, Supply.CSupplyArea area, CProvince province)
	{
		long best = 1_000_000;
		foreach (int id in area.Provinces)
		{
			best = System.Math.Min(best, CMapDistance.Between(province, state.Province(id), state.MapWidth));
		}
		return best;
	}

	/// <summary>
	/// The candidate a unit of the annexed would go over to: for anything but an air unit, the holder of the province it
	/// stands in, where a candidate; else for an air unit with a base, the holder of the base's province - its carrier's
	/// where it is aboard one - where a candidate; else for a fleet at sea, the first candidate, wherever that is.
	/// </summary>
	public static CCountry? CandidateOf(CGameState state, CUnit unit, List<CCountry> candidates)
	{
		if (unit.Kind != EUnitKind.Air && unit.Location > 0
			&& candidates.Find(c => c.Tag == state.Province(unit.Location).Controller) is { } here)
		{
			return here;
		}
		if (unit.Kind == EUnitKind.Air)
		{
			int baseProvince = unit.CarriedBy?.Unit is { } fleet ? fleet.Location : unit.Base;
			if (baseProvince > 0 && candidates.Find(c => c.Tag == state.Province(baseProvince).Controller) is { } based)
			{
				return based;
			}
		}
		if (unit.Kind == EUnitKind.Navy && unit.Location > 0 && state.Province(unit.Location).IsSea)
		{
			return candidates[0];
		}
		return null;
	}

	/// <summary>
	/// A unit goes over: out of its place, the units under it left behind at the top of the annexed's order of battle -
	/// each judged in its turn - and onto the end of the candidate's list, its leader with it.
	/// </summary>
	private static void GoOver(CGameState state, CCountry annexed, CUnit unit, CCountry to)
	{
		foreach (CUnit child in unit.Children.ToArray())
		{
			unit.Children.Remove(child);
			child.Parent = null;
			annexed.Units.Add(child);
		}
		if (unit.Parent is { } parent)
		{
			parent.Children.Remove(unit);
			unit.Parent = null;
		}
		else
		{
			annexed.Units.Remove(unit);
		}
		if (unit.Agent is { } agent)
		{
			Agents.CAgents.Unbind(state, agent);
		}
		unit.Owner = to.Tag;
		to.Units.Add(unit);
		state.ForgetPositions();
	}

	/// <summary>What every other country does towards the annexed, in the original's order.</summary>
	private static void Forget(CGameState state, CCountry other, CCountry annexed)
	{
		CUndeclaredWars.End(state, other.Tag, annexed.Tag);
		if (CWarSides.Between(state, other.Tag, annexed.Tag) is { } war)
		{
			CWarSides.Leave(state, war, annexed.Tag);
		}
		// Its spies in the annexed are gone (espionage.md, section 4).
		CGameState.Presence(other, annexed).Level = 0;
		state.Alliances.RemoveAll(alliance => alliance.Joins(other.Tag, annexed.Tag));
		if (annexed.Overlord == other.Tag)
		{
			annexed.Overlord = null;
		}
		if (other.Overlord == annexed.Tag)
		{
			other.Overlord = null;
		}
		state.Guarantees.RemoveAll(guarantee => guarantee.Joins(other.Tag, annexed.Tag));
		Lower(CGameState.Status(other, annexed));
		Lower(CGameState.Status(annexed, other));
		Trade.CTradeRoutes.EndPair(state, other.Tag, annexed.Tag);

		// The original resets the other's side alone, leaving the annexed's own pointing at the same pacts; here a
		// pact is one object for both, so it goes for both (war.md, Ours).
		state.Naps.RemoveAll(nap => (nap.First == other.Tag && nap.Second == annexed.Tag) || (nap.First == annexed.Tag && nap.Second == other.Tag));
		state.Embargoes.RemoveAll(embargo => (embargo.First == other.Tag && embargo.Second == annexed.Tag)
			|| (embargo.First == annexed.Tag && embargo.Second == other.Tag));
		foreach (CInfluence influence in state.Influences.ToArray())
		{
			if ((influence.Influencer == other.Tag && influence.Influenced == annexed.Tag)
				|| (influence.Influencer == annexed.Tag && influence.Influenced == other.Tag))
			{
				CPacts.EndInfluence(state, influence);
			}
		}
		foreach (CAlign align in state.Aligns.ToArray())
		{
			if ((align.Aligner == other.Tag && align.Leader == annexed.Tag) || (align.Aligner == annexed.Tag && align.Leader == other.Tag))
			{
				CPacts.EndAlign(state, align);
			}
		}
		var status = CGameState.Status(other, annexed);
		status.Relation = CFixedPoint.Zero;
		status.HasMilitaryAccess = false;
		status.Debt = 0;
		status.DailyRepay = 0;
		status.AllowDebts = false;
		status.LastWar = null;
		status.CasusBelli.Clear();
		// And its occupation policy towards it, back to no_occupation (occupation.md, section 1).
		status.OccupationPolicy = 0;
	}

	private static void Lower(Diplomacy.CDiplomacyStatus status)
	{
		CFixedPoint threat = status.Threat - ThreatDropped;
		status.Threat = threat < CFixedPoint.Zero ? CFixedPoint.Zero : threat;
	}
}
