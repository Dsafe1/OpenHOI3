namespace OpenHOI3.Sim.Agreements;

/// <summary>What an offer is about. See <c>docs/mechanics/agreements.md</c>, section 1, <c>aid.md</c> and <c>war.md</c>.</summary>
public enum EOfferKind
{
	Alliance,
	NonAggressionPact,
	Guarantee,
	AskMilitaryAccess,
	OfferMilitaryAccess,
	Embargo,
	Influence,
	Align,
	Trade,
	Debt,
	ShareTechnology,
	RequestLendLease,
	OfferLendLease,
	Licence,
	/// <summary>A declaration of war: value set a war, unset a limited war. Never answered.</summary>
	DeclareWar,
	/// <summary>A call to arms against the offer's <c>Versus</c>.</summary>
	CallAlly,
	/// <summary>A war goal added to a war the two are fighting. Never answered.</summary>
	AddWarGoal,
	/// <summary>Peace, with no terms: the war between the two ends.</summary>
	Peace,
	/// <summary>Join Faction where its value is set, Invite to Faction where not (<c>factions.md</c>, section 3).</summary>
	Faction,
}
