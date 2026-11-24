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
	public void ALinkSendsWithoutWaitingInTheOrderGivenAndEverythingBeforeItCloses()
	{
		// docs/multiplayer.md, section 14: on this machine's loopback, a port the system picks.
		using var listener = new CLineListener(port: 0);
		CLineLink client = CLineLink.Connect("127.0.0.1", listener.Port);
		CLineLink? host = null;
		var patience = System.Diagnostics.Stopwatch.StartNew();
		while (!listener.TryAccept(out host!) && patience.ElapsedMilliseconds < 5000)
		{
			System.Threading.Thread.Sleep(1);
		}
		Assert.NotNull(host);

		// Some megabytes given at once come back from Send at once, are counted, and all arrive in order - the last
		// line too, given just before the link is closed.
		string piece = new('x', 60_000);
		for (int i = 0; i < 200; i++)
		{
			host.Send(i + " " + piece);
		}
		host.Send("last");
		Assert.True(host.QueuedChars > 12_000_000);
		host.Dispose();

		var came = new List<string>();
		while (came.Count < 201 && patience.ElapsedMilliseconds < 20000)
		{
			if (client.TryReceive(out string line))
			{
				came.Add(line);
			}
			else
			{
				System.Threading.Thread.Sleep(1);
			}
		}
		client.Dispose();
		Assert.Equal(201, came.Count);
		Assert.Equal("last", came[200]);
		Assert.All(came.Take(200).Select((line, i) => (line, i)), pair => Assert.StartsWith(pair.i + " ", pair.line));
	}

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
		// Bytes with nothing to pack in them, so that what is sent is as long as the game.
		byte[] game = new byte[130_000];
		new Random(7).NextBytes(game);
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
	public void AGameIsDeflatedBeforeItIsCutIntoPieces()
	{
		byte[] game = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("province = { owner = GER controller = GER }", 4000)));
		byte[] packed = CGameSender.Pack(game);
		Assert.True(packed.Length < game.Length / 20);
		Assert.Equal(game, CGameSender.Unpack(packed));
		Assert.Null(CGameSender.Unpack(new byte[] { 255, 255, 255, 255 }));

		(CGameSender sender, CGameReceiver receiver, List<string> toClient) = Pair(game, _ => null);
		sender.Offer();
		Assert.Equal(game, receiver.Game);
		// Offered, one piece and the end, where the bytes as they are would be four pieces.
		Assert.Equal(3, toClient.Count);
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
	public void AGameComingSaysHowMuchOfItIsHere()
	{
		byte[] game = { 1, 2, 3, 4, 5, 6, 7, 8 };
		// What comes is the game deflated, and it is counted in those bytes.
		byte[] packed = CGameSender.Pack(game);
		int half = packed.Length / 2;
		var receiver = new CGameReceiver(_ => { }, _ => null);
		Assert.Equal(0, receiver.Percent);
		receiver.Receive(CWire.Line(CWire.GameOffered, CGameSender.Hash(game) + " " + packed.Length));
		Assert.Equal(packed.Length, receiver.Total);
		Assert.Equal(0, receiver.Percent);
		receiver.Receive(CWire.Line(CWire.GamePiece, Convert.ToBase64String(packed, 0, half)));
		Assert.Equal(100 * half / packed.Length, receiver.Percent);
		receiver.Receive(CWire.Line(CWire.GamePiece, Convert.ToBase64String(packed, half, packed.Length - half)));
		Assert.Equal(100, receiver.Percent);
		receiver.Receive(CWire.Line(CWire.GameSent));
		Assert.Equal(100, receiver.Percent);
		Assert.Equal(game, receiver.Game);

		// One this machine holds already is here at once.
		var holder = new CGameReceiver(_ => { }, _ => game);
		holder.Receive(CWire.Line(CWire.GameOffered, CGameSender.Hash(game) + " 8"));
		Assert.Equal(100, holder.Percent);
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
