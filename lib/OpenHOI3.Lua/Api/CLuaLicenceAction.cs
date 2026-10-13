using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A <c>CLicenceTechnologyAction</c> as a script holds it - one it made, or the one it is asked to answer:
/// the buyer and the seller, whether it buys, the hour it was made, and the unit type, the runs and the money
/// a unit, nought and none until set. See <c>docs/mechanics/aid.md</c>, section 4.
/// </summary>
public sealed class CLuaLicenceAction
{
	public CLuaLicenceAction(CCountryTag actor, CCountryTag recipient, long date)
	{
		Actor = actor;
		Recipient = recipient;
		Date = date;
	}

	public CCountryTag Actor { get; }

	public CCountryTag Recipient { get; }

	public long Date { get; }

	public bool Value { get; set; } = true;

	/// <summary>The unit type's key; empty where none is chosen.</summary>
	public string UnitType { get; set; } = "";

	public int Serial { get; set; }

	public int Parallel { get; set; }

	public CFixedPoint Money { get; set; }

	/// <summary>The offer it stands for, proposed.</summary>
	public COffer Offer => new(EOfferKind.Licence, Actor, Recipient, EOfferStage.Proposed, Value, Date)
	{
		Licence = UnitType.Length == 0 ? null : new CLicenceTerms(UnitType, Serial, Parallel, Money),
	};

	/// <summary>The one an offer stands for.</summary>
	public static CLuaLicenceAction Of(COffer offer)
	{
		var action = new CLuaLicenceAction(offer.Actor, offer.Recipient, offer.Date) { Value = offer.Value };
		if (offer.Licence is { } terms)
		{
			action.UnitType = terms.UnitType;
			action.Serial = terms.Serial;
			action.Parallel = terms.Parallel;
			action.Money = terms.Money;
		}
		return action;
	}
}
