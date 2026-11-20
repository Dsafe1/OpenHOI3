using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;
using OpenHOI3.Sim.Provinces;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Who plays a country, changed in play (<c>docs/multiplayer.md</c>, sections 2 and 4.1): a player given one nobody
/// plays, one who drops, and one who comes back and takes another.
/// </summary>
public class MultiplayerCountryTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private static CGameState State()
	{
		var countries = new[]
		{
			new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
			new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
			new CCountry { Tag = Eng, Name = "United Kingdom", DefinitionFile = "" },
		};
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, SeaIndex = -1 },
				new CProvince(2, "Warszawa", isSea: false) { Owner = Pol, Controller = Pol, SeaIndex = -1 },
			},
			countries);
	}

	[Fact]
	public void ACountryTakenUpIsPlayedAndOneLeftIsTheAisAgain()
	{
		CGameState state = State();
		CCountry germany = state.Country(Ger)!;
		germany.UnitAi.RunsUnits = true;

		var take = new CSetPlayedCommand(Ger, played: true);
		Assert.True(take.MayExecute(state));
		take.Execute(state);
		Assert.Contains(Ger, state.Played);
		Assert.False(germany.UnitAi.RunsUnits);
		// Taken already: nothing to do.
		Assert.False(take.MayExecute(state));

		new CSetPlayedCommand(Ger, played: false).Execute(state);
		Assert.DoesNotContain(Ger, state.Played);
		Assert.True(germany.UnitAi.RunsUnits);
		Assert.True(germany.PoliticsCountdown >= 1);
	}

	[Fact]
	public void APlayerIsGivenACountryNobodyPlaysAndToldSo()
	{
		CGameState state = State();
		var run = new CSimulation(state);
		var host = new CHostSession(run);
		var told = new List<string>();
		CHostPeer peer = host.Add("A", told.Add);

		// Those that own land and nobody plays; the United Kingdom owns none here.
		Assert.Equal(new[] { Ger, Pol }, host.Unplayed());

		Assert.True(host.Assign(peer, Pol));
		run.Pump();
		Assert.Contains(Pol, state.Played);
		Assert.Equal(Pol, peer.Country);
		Assert.Contains("a POL", told);
		Assert.Equal(new[] { Ger }, host.Unplayed());

		// A country somebody plays is nobody else's to take.
		CHostPeer other = host.Add("B", _ => { });
		Assert.False(host.Assign(other, Pol));
	}

	[Fact]
	public void APlayerWhoDropsLeavesHisCountryToTheAiAndOneWhoComesBackMayTakeAnother()
	{
		CGameState state = State();
		var run = new CSimulation(state);
		var host = new CHostSession(run);
		CHostPeer peer = host.Add("A", _ => { });
		host.Assign(peer, Pol);
		run.Pace.Set(paused: false, 2);
		run.Pump();

		host.Drop(peer);
		run.Pump();
		Assert.True(run.Pace.Paused);
		Assert.DoesNotContain(Pol, state.Played);

		// Back, and offered every country nobody plays - the one he had among them.
		CHostPeer back = host.Add("A", _ => { });
		Assert.Equal(new[] { Ger, Pol }, host.Unplayed());
		Assert.True(host.Assign(back, Ger));
		run.Pump();
		Assert.Contains(Ger, state.Played);
		Assert.DoesNotContain(Pol, state.Played);
	}

	[Fact]
	public void TakingAnotherCountryLeavesTheOneHeldToTheAi()
	{
		CGameState state = State();
		var run = new CSimulation(state);
		var host = new CHostSession(run);
		CHostPeer peer = host.Add("A", _ => { });
		host.Assign(peer, Pol);
		run.Pump();

		Assert.True(host.Assign(peer, Ger));
		run.Pump();
		Assert.Contains(Ger, state.Played);
		Assert.DoesNotContain(Pol, state.Played);
		Assert.Equal(Ger, peer.Country);
	}
}
