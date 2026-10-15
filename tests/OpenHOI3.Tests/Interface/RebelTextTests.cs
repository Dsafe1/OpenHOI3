using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the player is told of rebels, against <c>docs/mechanics/rebels.md</c>, sections 7, 10 and 13, on
/// <see cref="RebelWorld"/>.
/// </summary>
public class RebelTextTests
{
	private static string Localise(string key) => key;

	[Fact]
	public void The_revolt_risk_tooltip_opens_with_the_rebels_the_province_would_choose()
	{
		CGameState state = RebelWorld.State();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		state.Province(4).Intel[germany.Index] = 2;
		state.Province(1).Intel[germany.Index] = 2;

		Assert.StartsWith("LIKELY_REBELS: §Ypartisans_title§W\n", CRevoltRiskTooltip.Text(Localise, state, state.Province(4), germany.Tag));
		Assert.StartsWith("LIKELY_REBELS: §Ydisgruntled_rabble_title§W\n",
			CRevoltRiskTooltip.Text(Localise, state, state.Province(1), germany.Tag));
	}

	[Fact]
	public void A_province_lost_names_the_new_holder_in_place_of_the_players_country()
	{
		CGameState state = RebelWorld.State();
		var table = new Dictionary<string, string> { ["PROVINCELOST_LOG"] = "We lost $PROV$ to $COUNTRY$." };
		string Words(string key) => table.GetValueOrDefault(key, key);
		CMessageTypes types = CMessageTypes.Read(ClausewitzParser.Parse("PROVINCELOST = { } REVOLTSIEGE = { }"), _ => { });
		var lost = new CPactMessage("PROVINCELOST", "", "", RebelWorld.Pol, RebelWorld.Ger)
		{
			Told = RebelWorld.Ger,
			Values = new[] { ("PROV", "Warsaw"), ("COUNTRY", "Poland") },
		};

		CMessage message = CMessages.Pact(Words, types, state, RebelWorld.Ger, lost)!;
		Assert.Equal("We lost Warsaw to Poland.", CMessageText.Text(Words, message, "_LOG"));
		Assert.Null(CMessages.Pact(Words, types, state, RebelWorld.Pol, lost));

		// A siege won whose effect says nothing is told to nobody.
		var siege = new CPactMessage("REVOLTSIEGE", "", "", RebelWorld.Reb, RebelWorld.Ger)
		{
			Told = RebelWorld.Ger,
			Values = new[] { ("REBELS", "Rabble"), ("EFFECT", "disgruntled_rabble"), ("PROV", "p1"), ("PROVINCEID", "1") },
		};
		Assert.Null(CMessages.Pact(Words, types, state, RebelWorld.Ger, siege));
	}
}
