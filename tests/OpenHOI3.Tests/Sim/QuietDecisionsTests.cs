using System;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Rules;
using OpenHOI3.Sim.Script;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The decisions a country takes out of its "decisions available" alert (<c>docs/mechanics/events.md</c>, section 12):
/// the order that changes them, the save's key, and what the tick says.
/// </summary>
public class QuietDecisionsTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State(out CCountry germany, bool enhanced = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { germany, poland })
		{
			Decisions = new[]
			{
				new CDecision("trade_nickel", EDecisionGroup.Diplomatic, "decisions/test.txt", 1),
				new CDecision("trade_zinc", EDecisionGroup.Diplomatic, "decisions/test.txt", 2),
				new CDecision("road_network", EDecisionGroup.Diplomatic, "decisions/test.txt", 3),
			},
		};
		if (enhanced)
		{
			state.Rules.CopyFrom(new CRules(Array.Empty<EFix>()));
		}
		return state;
	}

	[Fact]
	public void TheOrderTakesDecisionsOutOfTheAlertAndPutsThemBackInAnEnhancedGameAlone()
	{
		CGameState state = State(out CCountry germany);
		var quiet = new CQuietDecisionsCommand(Ger, new[] { "trade_zinc", "trade_nickel" }, quiet: true);
		Assert.True(quiet.MayExecute(state));
		quiet.Execute(state);
		// Kept in the names' order, whatever order they were given in.
		Assert.Equal(new[] { "trade_nickel", "trade_zinc" }, germany.QuietDecisions);
		// Another country's are its own.
		Assert.Empty(state.Country(Pol)!.QuietDecisions);

		new CQuietDecisionsCommand(Ger, new[] { "trade_nickel" }, quiet: false).Execute(state);
		Assert.Equal(new[] { "trade_zinc" }, germany.QuietDecisions);

		// A name that is no decision, no name at all and a country that is not there are refused whole.
		Assert.False(new CQuietDecisionsCommand(Ger, new[] { "road_network", "no_such_decision" }, quiet: true).MayExecute(state));
		Assert.False(new CQuietDecisionsCommand(Ger, Array.Empty<string>(), quiet: true).MayExecute(state));
		Assert.False(new CQuietDecisionsCommand(new CCountryTag("XXX"), new[] { "road_network" }, quiet: true).MayExecute(state));

		// A faithful game has no such list.
		CGameState faithful = State(out CCountry other, enhanced: false);
		var refused = new CQuietDecisionsCommand(Ger, new[] { "road_network" }, quiet: true);
		Assert.False(refused.MayExecute(faithful));
		refused.Execute(faithful);
		Assert.Empty(other.QuietDecisions);
	}

	[Fact]
	public void TheOrderIsItsCountrysToGiveAndTravelsAsItIs()
	{
		CGameState state = State(out CCountry germany);
		var order = new CQuietDecisionsCommand(Ger, new[] { "trade_zinc", "road_network" }, quiet: true);
		// A machine playing another country may not give it.
		Assert.True(COrderCheck.MayGive(order, Ger));
		Assert.False(COrderCheck.MayGive(order, Pol));

		// Written for the wire - and so for a recording - and read back, it does what it did.
		CCommand sent = CWire.Read(CWire.Write(order), state);
		Assert.IsType<CQuietDecisionsCommand>(sent);
		Assert.True(sent.MayExecute(state));
		sent.Execute(state);
		Assert.Equal(new[] { "road_network", "trade_zinc" }, germany.QuietDecisions);
	}

	[Fact]
	public void AnEnhancedSaveHoldsTheQuietDecisionsAndAFaithfulOneNoKey()
	{
		CGameState state = State(out CCountry germany);
		var writer = new CSaveTextWriter();
		// With none, nothing is written.
		CSaveQuietDecisions.Write(writer, state, germany);
		Assert.Equal("", writer.Text);

		germany.QuietDecisions.Add("trade_zinc");
		germany.QuietDecisions.Add("road_network");
		CSaveQuietDecisions.Write(writer, state, germany);
		Assert.Contains("openhoi_quiet_decisions", writer.Text);
		Assert.Contains("road_network trade_zinc", writer.Text);

		// Read into a country that held others: the save's, and only the save's.
		CGameState loadedState = State(out CCountry loaded);
		loaded.QuietDecisions.Add("trade_nickel");
		var warnings = new System.Collections.Generic.List<string>();
		CSaveQuietDecisions.Read(loadedState, loaded, ClausewitzParser.Parse(writer.Text), warnings.Add);
		Assert.Equal(new[] { "road_network", "trade_zinc" }, loaded.QuietDecisions);
		Assert.Empty(warnings);

		// A name the game has no decision for any more is left out and said.
		CSaveQuietDecisions.Read(loadedState, loaded, ClausewitzParser.Parse("openhoi_quiet_decisions={ trade_zinc gone_decision }"), warnings.Add);
		Assert.Equal(new[] { "trade_zinc" }, loaded.QuietDecisions);
		Assert.Contains("gone_decision", Assert.Single(warnings));

		// A block with no such key clears what the country held.
		CSaveQuietDecisions.Read(loadedState, loaded, ClausewitzParser.Parse("capital=1"), warnings.Add);
		Assert.Empty(loaded.QuietDecisions);

		// A faithful game writes no key, whatever its country holds.
		CGameState faithful = State(out CCountry plain, enhanced: false);
		plain.QuietDecisions.Add("trade_zinc");
		var plainWriter = new CSaveTextWriter();
		CSaveQuietDecisions.Write(plainWriter, faithful, plain);
		Assert.Equal("", plainWriter.Text);
	}

	[Fact]
	public void TheTickShowsItsMarkWhileTheDecisionCountsAndSaysWhatCtrlDoes()
	{
		Assert.Equal("X", CDecisionAlertText.Mark(counts: true));
		Assert.Equal("", CDecisionAlertText.Mark(counts: false));
		Assert.Equal(CDecisionAlertText.CountsKey, CDecisionAlertText.Tooltip(key => key, counts: true).Text);
		Assert.Equal(CDecisionAlertText.QuietKey, CDecisionAlertText.Tooltip(key => key, counts: false).Text);
		Assert.Equal(CDecisionAlertText.AllKey, CDecisionAlertText.Tooltip(key => key, counts: false).Delayed);
	}
}
