using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// Units lent to another country and taken back: the offer, the hand-over of a unit and all under it, the date a withdrawal sets and
/// the host's day that sends the first unit past its date home. See <c>docs/mechanics/expeditions.md</c>.
/// </summary>
public static class CExpeditions
{
	public const string CostDefine = "EXPEDITION_INFLUENCE_COST";
	public const string ReturnDefine = "EXPEDITION_RETURN_TIME";
	public const string ReclaimDefine = "EXPEDITION_RECLAIM_TIME";

	/// <summary>Who holds the unit an offer is about: the lender's for a sending, the recipient's for a recall, and the host's for a giving back.</summary>
	public static CUnit? UnitOf(CGameState state, COffer offer)
	{
		if (offer.Unit == 0)
		{
			return null;
		}
		return CUnitFinder.Find(state, offer.Value || !offer.Recall ? offer.Actor : offer.Recipient, offer.Unit);
	}

	public static bool IsValid(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (offer.Value && !COfferRules.SameFactionEvenNone(actor, recipient) && !CCountryLists.HasAlliance(state, actor.Tag, recipient.Tag))
		{
			return false;
		}
		return UnitOf(state, offer) is { Kind: not EUnitKind.Theatre };
	}

	public static bool IsSelectable(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient || (offer.Value && (COfferRules.DiplomatEnRoute(state, actor, recipient) || actor.DiplomaticInfluence < COfferRules.Define(state, CostDefine))))
		{
			return false;
		}
		return UnitOf(state, offer) is not { Kind: EUnitKind.Theatre };
	}

	/// <summary>The offer's stage does what it does: only what it says of the value is asked, never who withdrew.</summary>
	public static void Apply(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		CUnit? unit = UnitOf(state, offer);
		if (!offer.Value)
		{
			if (unit is not null)
			{
				unit.RecallHour = state.TotalHours + COfferRules.Define(state, ReturnDefine).Raw / CFixedPoint.Scale * CGameState.HoursPerDay;
			}
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				actor.DiplomaticInfluence = CFixedPoint.Max(CFixedPoint.Zero, actor.DiplomaticInfluence - COfferRules.Define(state, CostDefine));
				break;
			case EOfferStage.Accepted when unit is not null:
				HandOver(state, unit, actor, recipient);
				state.RaisePactMessage(new CPactMessage("OUREXPACC", "EXPOTHER", "EXPOTHER", actor.Tag, recipient.Tag) { Values = new[] { ("UNIT", unit.Name) }, Sound = CPactMessage.Success });
				break;
			case EOfferStage.Declined:
				state.RaisePactMessage(new CPactMessage("OUREXPDEC", "", "", actor.Tag, recipient.Tag) { Told = actor.Tag, Values = new[] { ("UNIT", unit?.Name ?? "") }, Sound = CPactMessage.Failure });
				break;
		}
	}

	/// <summary>The unit and everything under it go to the host, each remembering who it came from first; no recall stands.</summary>
	public static void HandOver(CGameState state, CUnit unit, CCountry lender, CCountry host)
	{
		LeaveParent(unit);
		lender.Units.Remove(unit);
		host.Units.Add(unit);
		unit.RecallHour = null;
		Change(state, unit, lender, host);
		state.ForgetPositions();
	}

	private static void Change(CGameState state, CUnit unit, CCountry lender, CCountry host)
	{
		if (unit.Lender is null)
		{
			unit.Lender = lender.Tag;
			lender.LentUnits.Add(unit);
		}
		unit.Owner = host.Tag;
		if (unit.Agent is { } agent)
		{
			Agents.CAgents.Unbind(state, agent);
		}
		foreach (CUnit child in unit.Children)
		{
			Change(state, child, lender, host);
		}
	}

	/// <summary>The unit goes home alone; what is under it stays with the host, as the host's own.</summary>
	public static void Return(CGameState state, CUnit unit)
	{
		if (unit.Lender is not { } tag || state.Country(tag) is not { } lender || state.Country(unit.Owner) is not { } host)
		{
			return;
		}
		TakeHome(state, unit);
		state.RaisePactMessage(new CPactMessage("EXPRETURNEDWE", "EXPRETURNED", "", host.Tag, lender.Tag) { Values = new[] { ("NAME", unit.Name) } });
	}

	/// <summary>The unit back with its lender, with nobody told.</summary>
	public static void TakeHome(CGameState state, CUnit unit)
	{
		if (unit.Lender is not { } tag || state.Country(tag) is not { } lender || state.Country(unit.Owner) is not { } host)
		{
			return;
		}
		foreach (CUnit child in unit.Children.ToList())
		{
			LeaveParent(child);
			host.Units.Add(child);
		}
		LeaveParent(unit);
		host.Units.Remove(unit);
		lender.LentUnits.Remove(unit);
		unit.Lender = null;
		unit.RecallHour = null;
		if (unit.Agent is { } agent)
		{
			Agents.CAgents.Unbind(state, agent);
		}
		unit.Owner = lender.Tag;
		lender.Units.Add(unit);
		state.ForgetPositions();
	}

	/// <summary>Each host's first unit past its date goes home: several recalled come back one a day.</summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry host in state.Countries)
		{
			if (host.AllUnits().FirstOrDefault(unit => unit.Lender is not null && unit.RecallHour < state.TotalHours) is { } due)
			{
				Return(state, due);
			}
		}
	}

	/// <summary>A country annexed: what was lent to it goes home shattered, and what it lent comes back to be destroyed with the rest.</summary>
	public static void ReturnAll(CGameState state, CCountry annexed)
	{
		foreach (CUnit lent in annexed.AllUnits().Where(unit => unit.Lender is not null).ToList())
		{
			CCountryTag lender = lent.Lender!.Value;
			Return(state, lent);
			if (state.Country(lender) is { } home)
			{
				Combat.CCombatRetreat.Shatter(state, home, lent);
			}
		}
		foreach (CCountry host in state.Countries)
		{
			foreach (CUnit lentOut in host.AllUnits().Where(unit => unit.Lender == annexed.Tag).ToList())
			{
				Return(state, lentOut);
			}
		}
	}

	private static void LeaveParent(CUnit unit)
	{
		unit.Parent?.Children.Remove(unit);
		unit.Parent = null;
	}
}
