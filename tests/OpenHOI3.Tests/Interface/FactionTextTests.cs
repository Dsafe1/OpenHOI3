using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What Join Faction and Invite to Faction say, against <c>docs/mechanics/factions.md</c>, section 3, on
/// <see cref="FactionTests.World"/>: Germany leads the axis, Poland is in no faction.
/// </summary>
public class FactionTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static readonly Dictionary<string, string> Table = new()
	{
		["axis"] = "Axis",
		["DIP_TO_NEUTR_THREAT"] = "ours $VAL$ under $LIM$",
		["DIP_THEY_TO_NEUTR_THREAT"] = "theirs $VAL$ under $LIM$",
		["FACTION_DISTANCE_2"] = "under $VAL$ from the $FACTION$, now $VALC$",
		["INVITEFACTIONDESC"] = "Invite them to join our faction",
		["INVITEFACTIONNEWDESC"] = "Invite $COUNTRY$ to the $FACTION$?",
		["JOINFACTIONNEWDESC"] = "Join the $FACTION$?",
		["DIP_INFLUENCECOST"] = "Costs $COST$.",
		["ASKFACTIONREQDESC"] = "$ACTOR$ asks to join the $FACTION$.",
		["INVITEFACTIONREQDESC"] = "$ACTOR$ invites us to the $FACTION$.",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static COffer Offer(CGameState state, CCountryTag actor, CCountryTag recipient, bool join) =>
		new(EOfferKind.Faction, actor, recipient, EOfferStage.Proposed, join, state.TotalHours);

	[Fact]
	public void The_neutrality_line_prints_the_other_directions_limit_and_the_distance_line_the_distance()
	{
		CGameState state = FactionTests.World();
		state.Defines.Set(COfferRules.Section, "JOIN_FACTION_NEUTRALITY", CFixedPoint.FromInt(30));
		CCountry poland = state.Country(Pol)!;
		COffer invite = Offer(state, Ger, Pol, join: false);

		poland.EffectiveNeutrality = CFixedPoint.FromInt(26);
		Assert.Equal("theirs 26.00 under 30", CDiplomacyActions.Tooltip(Localise, state, invite));

		poland.EffectiveNeutrality = CFixedPoint.Zero;
		poland.AlignmentX = 140000;
		poland.AlignmentY = 200000;
		Assert.Equal("under 50.00 from the Axis, now 60.00", CDiplomacyActions.Tooltip(Localise, state, invite));

		poland.AlignmentX = 190000;
		Assert.Equal("Invite them to join our faction", CDiplomacyActions.Tooltip(Localise, state, invite));
	}

	[Fact]
	public void The_dialog_prints_the_alliances_costs_and_the_asked_player_the_right_faction()
	{
		CGameState state = FactionTests.World();
		state.Defines.Set(COfferRules.Section, "JOIN_ALLIANCE_INFLUENCE_COST", CFixedPoint.FromInt(5));
		state.Defines.Set(COfferRules.Section, "LEAVE_ALLIANCE_INFLUENCE_COST", CFixedPoint.One);

		Assert.Equal("Join the Axis?\nCosts 5.00.", CDiplomacyActions.Description(Localise, state, Offer(state, Pol, Ger, join: true)));
		Assert.Equal("Invite POL to the Axis?\nCosts 1.00.", CDiplomacyActions.Description(Localise, state, Offer(state, Ger, Pol, join: false)));

		Assert.Equal("POL asks to join the Axis.", COfferAskText.Description(Localise, state, Offer(state, Pol, Ger, join: true)));
		Assert.Equal("GER invites us to the Axis.", COfferAskText.Description(Localise, state, Offer(state, Ger, Pol, join: false)));
		Assert.Equal(8, COfferAskText.Kind(Offer(state, Ger, Pol, join: false)));
	}
}
