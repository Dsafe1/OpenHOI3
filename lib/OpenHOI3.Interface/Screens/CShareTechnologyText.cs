using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the share-technology dialog says: a row's two levels - ours green, theirs red, whole - and the
/// description with nothing chosen and with a theory chosen. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public static class CShareTechnologyText
{
	/// <summary>The sharer's own level, green and whole.</summary>
	public static string Our(CCountry actor, CTechnologyCategory category) =>
		CColouredText.In(CColouredText.Green) + Whole(actor.OwnAbility(category.Name));

	/// <summary>The recipient's level as it reads it - its own or its sharer's - red and whole.</summary>
	public static string Their(CCountry recipient, CTechnologyCategory category) =>
		CColouredText.In(CColouredText.Red) + Whole(recipient.Ability(category.Name));

	private static string Whole(CFixedPoint level) => (level.Raw / CFixedPoint.Scale).ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// <c>TS_NO_SELECTED</c> with nothing chosen; with a theory chosen, <c>TS_SELECTED</c> - the theory, the
	/// recipient, the influence and the leadership it costs, each to one place.
	/// </summary>
	public static string Description(Func<string, string> localise, CGameState state, CCountry recipient, CTechnologyCategory? chosen)
	{
		if (chosen is null)
		{
			return localise("TS_NO_SELECTED");
		}
		return CTextTemplate.Fill(localise("TS_SELECTED"), ("TECH", localise(chosen.Name)), ("COUNTRY", recipient.Name),
			("COST", CNumberText.Format(COfferRules.Define(state, "SHARE_TECH_INFLUENCE_COST"), 1)),
			("LEADERSHIP", CNumberText.Format(COfferRules.Define(state, CTechnologySharing.LeadershipCostDefine), 1)));
	}
}
