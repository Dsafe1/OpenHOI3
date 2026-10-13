using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the war rows, the declare-war dialog and the call to arms say, against <c>docs/mechanics/war.md</c>,
/// sections 6, 7 and 10, on <see cref="WarTests"/>'s world.
/// </summary>
public class WarTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private static readonly Dictionary<string, string> Table = new()
	{
		["AGRESSION_WAR_NAME"] = "$ORDER$War of $FIRST$ Aggression",
		["ST"] = "st",
		["ND"] = "nd",
		["RD"] = "rd",
		["TH"] = "th",
		["puppet_setup"] = "Install a loyal government in $RECIPIENT$",
		["aquire_territory_setup"] = "Acquire $STATE$ §Yfrom $RECIPIENT$",
		["WARDIALOG_SELECT_STATE"] = "Select a §Yregion claim§W.",
		["DIP_WAR_NEUTR"] = "Our neutrality is §Y$NEUT$§W; $THEM$ allows it at §Y$THREAT$§W.",
		["HAS_TRUCE_WITH"] = "Have truce with ",
		["CALLREQDESC"] = "The nation of $COUNTRY$, your faithful ally, is requesting that you come to their aid in the conflict against $LIST$. ",
		["WCRD_DEF"] = "This is a defensive war, where we need to help our ally.",
		["WCRD_ATT"] = "This is a war that they have started.",
		["CW_WARNALLIES2"] = "$ENEMY$ is allied with $COUNTRY$.",
		["PEACENEWDESC"] = "Do you wish to request peace with $COUNTRY$. $SELF$?\nIf they accept, the war will end. $RESULT$",
		["PEACENEWALLY"] = "They are negotiating for their allies.",
		["PEACENEWWITHE"] = "All territory will revert to its prewar owner.",
		["PEACENEWPROVUS"] = "§Y$NATION$§W will cede §R",
		["PEACENEWPROVTHEM"] = "§Y$NATION$§W will cede §G",
		["PEACETO"] = "to §Y$REC$§W",
		["AND"] = "and ",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	[Fact]
	public void A_war_is_named_by_its_aggressor_and_its_order()
	{
		var war = new CWar("") { MadeName = new CWarName(CWarName.Aggression, "German", "Polish", 1) };
		Assert.Equal("War of German Aggression", CWarText.Name(Localise, war));
		war.MadeName = war.MadeName with { Order = 2 };
		Assert.Equal("2nd War of German Aggression", CWarText.Name(Localise, war));
		war.MadeName = war.MadeName with { Order = 12 };
		Assert.Equal("12th War of German Aggression", CWarText.Name(Localise, war));
		war.MadeName = war.MadeName with { Order = 23 };
		Assert.Equal("23rd War of German Aggression", CWarText.Name(Localise, war));
		Assert.Equal("Second Italo-Abyssinian War", CWarText.Name(Localise, new CWar("Second Italo-Abyssinian War")));
	}

	[Fact]
	public void A_goals_setup_names_the_recipient_in_yellow_and_asks_for_a_region_it_lacks()
	{
		CGameState state = WarTests.World();
		var puppet = new CWarGoal(state.CasusBelliTypes[2], Ger, Pol);
		Assert.Equal("§WInstall a loyal government in §YPOL§W", CWarText.Setup(Localise, state, puppet));
		var territory = new CWarGoal(new CCasusBelliType("aquire_territory", 3), Ger, Pol);
		Assert.Equal("Select a §Yregion claim§W.", CWarText.Setup(Localise, state, territory));
		Assert.Equal("", CWarText.Setup(Localise, state, new CWarGoal(CCasusBelliType.Null, Ger, Pol)));
	}

	[Fact]
	public void Declare_war_comes_first_at_peace_and_says_why_it_may_not_be()
	{
		CGameState state = WarTests.World();
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		List<COffer> rows = CDiplomacyActions.Rows(state, germany, poland);
		Assert.Equal(EOfferKind.DeclareWar, rows[0].Kind);
		// No faction rule: no limited war row.
		Assert.NotEqual(EOfferKind.DeclareWar, rows[1].Kind);

		germany.Neutrality = CFixedPoint.FromInt(90);
		germany.EffectiveNeutrality = CFixedPoint.FromInt(90);
		Assert.Equal("Our neutrality is §Y90.00§W; POL allows it at §Y0.00§W.", CDiplomacyActions.Tooltip(Localise, state, rows[0]));
		CGameState.Status(germany, poland).LastWar = state.TotalHours;
		Assert.Equal("Have truce with POL", CDiplomacyActions.Tooltip(Localise, state, rows[0]));
		Assert.Equal("WARTITLE", CDiplomacyActions.Title(Localise, rows[0]));
	}

	[Fact]
	public void Peace_is_listed_at_war_and_its_dialog_says_who_cedes_what_to_whom()
	{
		CGameState state = WarTests.World();
		new COfferCommand(new COffer(EOfferKind.DeclareWar, Ger, Pol, EOfferStage.Accepted, true, state.TotalHours)
		{
			WarGoal = new CWarGoal(state.CasusBelliTypes[1], Ger, Pol),
		}).Execute(state);
		CCountry germany = state.Country(Ger)!;
		CCountry poland = state.Country(Pol)!;
		List<COffer> rows = CDiplomacyActions.Rows(state, germany, poland);
		int peace = rows.FindIndex(row => row.Kind == EOfferKind.Peace);
		Assert.Equal(EOfferKind.AddWarGoal, rows[peace + 1].Kind);
		Assert.Equal("PEACETITLE", CDiplomacyActions.Title(Localise, rows[peace]));
		Assert.Equal("Do you wish to request peace with POL. They are negotiating for their allies.?\nIf they accept, the war will end. "
			+ "All territory will revert to its prewar owner.", CDiplomacyActions.Description(Localise, state, rows[peace]));

		// Two provinces of Poland's, held by Germany, with its cores: green, Poland not being on Germany's side.
		foreach (int id in new[] { 2, 3 })
		{
			state.Province(id).Owner = Pol;
			state.Province(id).Controller = Ger;
			state.Province(id).Cores.Add(Ger);
		}
		Assert.Equal("§YPOL§W will cede §Gp2 and p3§W to §YGER§W\n", CPeaceText.Result(Localise, state, germany, poland));
	}

	[Fact]
	public void A_call_to_arms_tells_the_side_it_is_against_and_what_ties_the_player()
	{
		CGameState state = WarTests.World();
		new COfferCommand(new COffer(EOfferKind.DeclareWar, Ger, Pol, EOfferStage.Accepted, true, state.TotalHours)
		{
			WarGoal = new CWarGoal(state.CasusBelliTypes[1], Ger, Pol),
		}).Execute(state);
		COffer call = state.Country(Eng)!.PendingOffers.Single();
		Assert.Equal(6, COfferAskText.Kind(call));
		// Poland defends and is Britain's ally; Germany's allies are its faction's members - Italy.
		Assert.Equal("The nation of POL, your faithful ally, is requesting that you come to their aid in the conflict "
			+ "against GER, SLO, ITA. This is a defensive war, where we need to help our ally.GER is allied with ITA.",
			COfferAskText.Description(Localise, state, call));
	}
}
