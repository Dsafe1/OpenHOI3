using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A country made another's puppet, the original's <c>MakeVassal</c>, and a puppet set free by a capitulation.
/// See <c>docs/mechanics/war.md</c>, section 13.
/// </summary>
public static class CPuppets
{
	/// <summary>What the relation between a new puppet and its overlord is raised by, before it is held to 200.</summary>
	private static readonly CFixedPoint PuppetRelation = CFixedPoint.FromInt(500);

	/// <summary>
	/// The vassal becomes the overlord's puppet, in the original's order: its provinces carrying an overlord's core
	/// that either holds go to the overlord, peacefully; its own cores the overlord's side holds come back to it; it
	/// leaves its faction and every war; every agreement it has with anyone ends - its puppets go free; it is
	/// allied to the overlord and stands where the overlord stands on the triangle; it joins the overlord's real
	/// faction and with it the faction leader's wars; it becomes a puppet; the relation is the overlord's raised by
	/// 500, held to 200, both ways; and it takes the overlord's government. Nobody is told.
	/// </summary>
	public static void Make(CGameState state, CCountry overlord, CCountry vassal)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == vassal.Tag && province.Cores.Contains(overlord.Tag)
				&& (province.Controller == vassal.Tag || province.Controller == overlord.Tag))
			{
				CProvinceHands.ChangeOwner(state, province, overlord.Tag, consequences: false, peaceful: true);
			}
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == vassal.Tag && province.Cores.Contains(vassal.Tag) && province.Controller is { } holder
				&& (holder == overlord.Tag
					|| (COfferRules.RealFaction(overlord) && state.Country(holder)?.Faction == overlord.Faction)))
			{
				CProvinceHands.SetController(state, province, vassal.Tag, consequences: true, peaceful: true);
			}
		}
		if (COfferRules.RealFaction(vassal))
		{
			CFactionMembership.Leave(state, vassal);
		}
		foreach (CWar war in state.Wars.ToArray())
		{
			CWarSides.Leave(state, war, vassal.Tag);
		}
		EndAgreements(state, vassal);

		state.Alliances.Add(new CAlliance(overlord.Tag, vassal.Tag));
		vassal.AlignmentX = overlord.AlignmentX;
		vassal.AlignmentY = overlord.AlignmentY;
		if (COfferRules.RealFaction(overlord))
		{
			CFactions.AddCountry(state, vassal, overlord.Faction, joinWars: true);
		}
		vassal.Overlord = overlord.Tag;
		CFixedPoint relation = CRelations.Clamp(CRelations.Of(overlord, vassal) + PuppetRelation);
		CGameState.Status(overlord, vassal).Relation = relation;
		CGameState.Status(vassal, overlord).Relation = relation;
		// The overlord's government, and every post filled again for it (politics.md, section 7).
		vassal.GovernmentType = overlord.GovernmentType;
		Politics.CMinisterPosts.Fill(state, vassal, refillAll: true, electedOnly: false);
		Politics.CLaws.OnDiplomacyChanged(state, vassal);
	}

	/// <summary>
	/// A puppet set free, as the capitulation frees one a war goal names: the alliance and the bond with its overlord
	/// end, and military access, guarantees and leave to run a debt between the two. It stays in its wars.
	/// </summary>
	public static void Release(CGameState state, CCountry vassal)
	{
		if (state.Country(vassal.Overlord) is not { } overlord)
		{
			return;
		}
		state.Alliances.RemoveAll(alliance => alliance.Joins(overlord.Tag, vassal.Tag));
		vassal.Overlord = null;
		ClearBetween(state, overlord, vassal);
	}

	/// <summary>
	/// Every agreement the country has with anyone ends: alliances, bonds - its own puppets go free, and it leaves its
	/// overlord - NAPs and embargoes both ways; its own aligning, and others' influence on it; military access,
	/// guarantees and leave to run a debt both ways.
	/// </summary>
	private static void EndAgreements(CGameState state, CCountry country)
	{
		state.Alliances.RemoveAll(alliance => alliance.First == country.Tag || alliance.Second == country.Tag);
		foreach (CCountry other in state.Countries)
		{
			if (other.Overlord == country.Tag)
			{
				other.Overlord = null;
			}
		}
		country.Overlord = null;
		state.Naps.RemoveAll(nap => nap.First == country.Tag || nap.Second == country.Tag);
		state.Embargoes.RemoveAll(embargo => embargo.First == country.Tag || embargo.Second == country.Tag);
		foreach (CAlign align in state.Aligns.ToArray())
		{
			if (align.Aligner == country.Tag)
			{
				CPacts.EndAlign(state, align);
			}
		}
		foreach (CInfluence influence in state.Influences.ToArray())
		{
			if (influence.Influenced == country.Tag)
			{
				CPacts.EndInfluence(state, influence);
			}
		}
		foreach (CCountry other in state.Countries)
		{
			if (other != country)
			{
				ClearBetween(state, country, other);
			}
		}
	}

	/// <summary>Military access, guarantees and leave to run a debt, both ways between the two.</summary>
	private static void ClearBetween(CGameState state, CCountry one, CCountry other)
	{
		CGameState.Status(one, other).HasMilitaryAccess = false;
		CGameState.Status(other, one).HasMilitaryAccess = false;
		CGameState.Status(one, other).AllowDebts = false;
		CGameState.Status(other, one).AllowDebts = false;
		state.Guarantees.RemoveAll(guarantee => guarantee.Joins(one.Tag, other.Tag));
	}
}
