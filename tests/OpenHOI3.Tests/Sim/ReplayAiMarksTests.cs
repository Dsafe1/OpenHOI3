using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What of the AI's own the rest of the game reads reaches a machine that plays the AI back and does not run it
/// (<c>docs/multiplayer.md</c>, section 3): the mark that has a country's theatres made again, the wars it prepares, and
/// what the AI's answering leaves of the offers waiting.
/// </summary>
public class ReplayAiMarksTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, SeaIndex = -1 },
			},
			new[] { germany, poland });
	}

	/// <summary>The host's step through a recording, every entry through the form it has on disk, played back on a second game.</summary>
	private static (CGameState Host, CGameState Client, CReplayStep Kept) HostAndClient(System.Action<CGameState> before,
		System.Action<CGameState> step)
	{
		CGameState host = State();
		CGameState client = State();
		before(host);
		before(client);
		CReplay recording = CReplay.Recording();
		recording.RunStep(host, "the AI's step", step);
		var entries = recording.Entries.Select(entry => CReplayFile.Through(entry, client)).ToList();
		CReplay.Replaying(entries).RunStep(client, "the AI's step", _ => throw new System.InvalidOperationException("run on a client"));
		return (host, client, recording.Steps.Single());
	}

	[Fact]
	public void TheMarkThatHasTheatresMadeAgainReachesAMachineThatDoesNotRunTheAi()
	{
		(CGameState host, CGameState client, CReplayStep kept) = HostAndClient(
			state => state.Country(Ger)!.Strategy.TheatresDirty = false,
			state => state.Country(Ger)!.Strategy.TheatresDirty = true);

		Assert.True(host.Country(Ger)!.Strategy.TheatresDirty);
		Assert.True(client.Country(Ger)!.Strategy.TheatresDirty);
		// Only the country whose mark moved is kept.
		Assert.Single(kept.Done);
	}

	[Fact]
	public void AWarPreparedAndOneDroppedReachIt()
	{
		(CGameState host, CGameState client, _) = HostAndClient(
			state => state.Country(Ger)!.Strategy.TheatresDirty = false,
			state => state.Country(Ger)!.WarTargets.Add(new CWarTarget(Pol, null, limited: true, hour: 42) { ValueBits = 7 }));

		CWarTarget target = Assert.Single(client.Country(Ger)!.WarTargets);
		Assert.Equal(Pol, target.Target);
		Assert.True(target.Limited);
		Assert.Equal(42, target.Hour);
		Assert.Equal(7, target.ValueBits);
		Assert.Single(host.Country(Ger)!.WarTargets);

		// And dropped again by a later step.
		CReplay recording = CReplay.Recording();
		recording.RunStep(host, "the AI's step", state => state.Country(Ger)!.WarTargets.Clear());
		CReplay.Replaying(recording.Entries).RunStep(client, "the AI's step", _ => { });
		Assert.Empty(client.Country(Ger)!.WarTargets);
	}

	[Fact]
	public void AStepThatMovedNoMarkKeepsNone()
	{
		(_, _, CReplayStep kept) = HostAndClient(_ => { }, _ => { });
		Assert.Empty(kept.Done);
	}

	[Fact]
	public void WhatTheAnsweringLeavesOfTheOffersReachesIt()
	{
		var offer = new COffer(EOfferKind.Guarantee, Pol, Ger, EOfferStage.Proposed, true, 0);
		(CGameState host, CGameState client, _) = HostAndClient(
			state => state.Country(Ger)!.PendingOffers.Add(offer),
			CPactPasses.AnswerOffers);

		Assert.Empty(host.Country(Ger)!.PendingOffers);
		Assert.Empty(client.Country(Ger)!.PendingOffers);
	}
}
