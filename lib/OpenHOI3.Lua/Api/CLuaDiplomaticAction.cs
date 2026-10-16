using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A diplomatic action a script makes - <c>CAllianceAction(actor, recipient)</c> and its kin - proposed, value set and
/// dated the hour it was made, as the original's <c>Create</c> makes one; the call to arms with the war's enemy too.
/// See <c>docs/mechanics/foreignminister.md</c>, section 3.
/// </summary>
public sealed class CLuaDiplomaticAction
{
	public CLuaDiplomaticAction(EOfferKind kind, CCountryTag actor, CCountryTag recipient, long date)
	{
		Kind = kind;
		Actor = actor;
		Recipient = recipient;
		Date = date;
	}

	public EOfferKind Kind { get; }

	public CCountryTag Actor { get; }

	public CCountryTag Recipient { get; }

	public long Date { get; }

	public bool Value { get; set; } = true;

	/// <summary>The enemy of a call to arms; none for the rest.</summary>
	public CCountryTag? Versus { get; set; }

	/// <summary>The offer it stands for, proposed.</summary>
	public COffer Offer => new(Kind, Actor, Recipient, EOfferStage.Proposed, Value, Date) { Versus = Versus };
}
