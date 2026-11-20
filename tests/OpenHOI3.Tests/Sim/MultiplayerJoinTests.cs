using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;
using OpenHOI3.Sim.Provinces;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What comes before and around play between machines (<c>docs/multiplayer.md</c>, section 4.1): a machine let in by
/// its play checksum, a game sent or kept by its hash, and an order dropped that is not its sender's country's.
/// </summary>
public class MultiplayerJoinTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	[Fact]
	public void AMachineIsLetInByItsPlayChecksumAndToldTheHostsWhereItDiffers()
	{
		var told = new List<string>();
		Assert.Equal("David R", CHostSession.Admit(CClientSession.JoinLine("6B30DA67", "David R"), "6B30DA67", told.Add));
		Assert.Equal(new[] { "y " }, told);

		told.Clear();
		Assert.Null(CHostSession.Admit(CClientSession.JoinLine("4AFB2CB2", "David R"), "6B30DA67", told.Add));
		Assert.Equal(new[] { "n 6B30DA67" }, told);

		// Anything else first is no asking.
		told.Clear();
		Assert.Null(CHostSession.Admit("o {}", "6B30DA67", told.Add));
		Assert.Empty(told);
	}

	/// <summary>A sender and a receiver with their lines handed straight across.</summary>
	private static (CGameSender Sender, CGameReceiver Receiver, List<string> ToClient) Pair(byte[] game, Func<string, byte[]?> own)
	{
		CGameSender? sender = null;
		var toClient = new List<string>();
		var receiver = new CGameReceiver(line => sender!.Receive(line), own);
		sender = new CGameSender(line =>
		{
			toClient.Add(line);
			receiver.Receive(line);
		}, game);
		return (sender, receiver, toClient);
	}

	[Fact]
	public void AGameIsSentWholeInPiecesAndComesAsItWas()
	{
		byte[] game = Enumerable.Range(0, 130_000).Select(i => (byte)(i * 31)).ToArray();
		(CGameSender sender, CGameReceiver receiver, List<string> toClient) = Pair(game, _ => null);

		sender.Offer();
		Assert.Equal(game, receiver.Game);
		Assert.False(receiver.Kept);
		Assert.False(sender.Kept);
		// Offered, three pieces of 48,000 bytes or fewer, and the end.
		Assert.Equal(5, toClient.Count);
		Assert.All(toClient, line => Assert.DoesNotContain('\n', line));

		Assert.False(sender.Loaded);
		receiver.SayLoaded();
		Assert.True(sender.Loaded);
	}

	[Fact]
	public void AMachineThatHoldsTheGameIsSentNothing()
	{
		byte[] game = { 1, 2, 3, 4 };
		(CGameSender sender, CGameReceiver receiver, List<string> toClient) = Pair(game, _ => new byte[] { 1, 2, 3, 4 });

		sender.Offer();
		Assert.True(receiver.Kept);
		Assert.True(sender.Kept);
		Assert.Equal(game, receiver.Game);
		Assert.Single(toClient);
	}

	[Fact]
	public void ACopyThatIsNotTheGameOfferedIsNotKept()
	{
		byte[] game = { 1, 2, 3, 4 };
		(CGameSender sender, CGameReceiver receiver, _) = Pair(game, _ => new byte[] { 9, 9 });

		sender.Offer();
		Assert.False(receiver.Kept);
		Assert.Equal(game, receiver.Game);
	}

	[Fact]
	public void WhatComesDamagedIsNotTakenForTheGame()
	{
		var receiver = new CGameReceiver(_ => { }, _ => null);
		receiver.Receive(CWire.Line(CWire.GameOffered, CGameSender.Hash(new byte[] { 1, 2, 3 }) + " 3"));
		receiver.Receive(CWire.Line(CWire.GamePiece, Convert.ToBase64String(new byte[] { 1, 2, 4 })));
		receiver.Receive(CWire.Line(CWire.GameSent));

		Assert.True(receiver.Failed);
		Assert.Null(receiver.Game);
	}

	[Fact]
	public void AnOrderIsItsSendersCountrysOrIsDropped()
	{
		Assert.True(COrderCheck.MayGive(new CAutomateUnitCommand(Ger, 5), Ger));
		Assert.False(COrderCheck.MayGive(new CAutomateUnitCommand(Ger, 5), Pol));
		// A player with no country gives none that names one.
		Assert.False(COrderCheck.MayGive(new CAutomateUnitCommand(Ger, 5), null));
		// One that names no country is let through.
		Assert.True(COrderCheck.MayGive(new CFogOfWarCommand(true), null));
	}

	[Fact]
	public void TheHostDropsAnOrderForAnotherCountryAndPostsItsSendersOwn()
	{
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) };
		var state = new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>());
		var host = new CHostSession(new CSimulation(state));
		CHostPeer peer = host.Add("A", _ => { });
		peer.Country = Pol;

		host.Receive(peer, CWire.Line(CWire.Order, CWire.Write(new CAutomateUnitCommand(Ger, 5))));
		Assert.Equal(1, host.OrdersDropped);
		host.Receive(peer, CWire.Line(CWire.Order, CWire.Write(new CAutomateUnitCommand(Pol, 5))));
		Assert.Equal(1, host.OrdersDropped);
	}
}
