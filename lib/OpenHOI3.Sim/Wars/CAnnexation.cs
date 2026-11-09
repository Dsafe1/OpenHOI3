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
	/// </summary>
	public static void Annex(CGameState state, CCountry annexer, CCountry annexed)
	{
		state.RaisePactMessage(new CPactMessage("ANNEX", "ANNEX", "ANNEX", annexer.Tag, annexed.Tag));
		if (COfferRules.RealFaction(annexed))
		{
			CFactionMembership.Leave(state, annexed);
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != annexed.Tag)
			{
				continue;
			}
			CCountryTag to = province.Controller is { } holder && holder != annexed.Tag && holder.Value != Rebels ? holder : annexer.Tag;
			CProvinceHands.ChangeOwner(state, province, to, consequences: true);
		}
		annexed.Constructions.Clear();
		CExpeditions.ReturnAll(state, annexed);
		foreach (CUnit unit in annexed.UnitsInOrder())
		{
			CCombatRetreat.RemoveFromGame(state, unit);
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

	/// <summary>What every other country does towards the annexed, in the original's order.</summary>
	private static void Forget(CGameState state, CCountry other, CCountry annexed)
	{
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
