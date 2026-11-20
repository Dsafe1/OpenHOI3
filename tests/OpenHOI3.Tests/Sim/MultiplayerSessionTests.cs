using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A host and its clients with nothing between them but lines of text (<c>docs/multiplayer.md</c>, section 5): the
/// rules of play around a game that follows another - an order's way there and back, the hour held until it is whole,
/// the host's waiting, anybody's pause, the midnight's hash, a client gone.
/// </summary>
public class MultiplayerSessionTests
{
	private static CGameState State()
	{
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>());
	}

	/// <summary>A host and clients, each line kept in a list until it is delivered - a wire that can be held up.</summary>
	private sealed class CTable
	{
		public CSimulation HostRun { get; } = new(State());
		public CHostSession Host { get; }
		public List<(CSimulation Run, CClientSession Session, CHostPeer Peer, Queue<string> ToClient, Queue<string> ToHost)> Clients { get; } = new();

		public CTable(int window = CHostSession.DefaultWindow)
		{
			Host = new CHostSession(HostRun, window);
		}

		public int Join(string name)
		{
			var run = new CSimulation(State());
			var toClient = new Queue<string>();
			var toHost = new Queue<string>();
			var session = new CClientSession(run, toHost.Enqueue);
			CHostPeer peer = Host.Add(name, toClient.Enqueue);
			Clients.Add((run, session, peer, toClient, toHost));
			return Clients.Count - 1;
		}

		/// <summary>Every line under way delivered, both ways, for one client or all.</summary>
		public void Deliver(int only = -1)
		{
			for (int i = 0; i < Clients.Count; i++)
			{
				if (only >= 0 && only != i)
				{
					continue;
				}
				var client = Clients[i];
				while (client.ToClient.Count > 0)
				{
					client.Session.Receive(client.ToClient.Dequeue());
				}
				while (client.ToHost.Count > 0)
				{
					Host.Receive(client.Peer, client.ToHost.Dequeue());
				}
			}
		}

		/// <summary>The host's frame with an hour due, then everything delivered and every client's frame.</summary>
		public void HostHour()
		{
			HostRun.Pace.Set(paused: false, CClock.SpeedCount - 1);
			HostRun.Advance(1.0);
			Host.Tell();
		}

		public void ClientsCatchUp()
		{
			Deliver();
			foreach (var client in Clients)
			{
				client.Session.Step(mostHours: 100);
			}
			Deliver();
		}
	}

	[Fact]
	public void AClientsOrderGoesToTheHostAndRunsOnBoth()
	{
		var table = new CTable();
		int a = table.Join("A");
		bool before = table.HostRun.State.FogOfWar;

		Assert.True(table.Clients[a].Run.Post(new CFogOfWarCommand(!before)));
		Assert.Equal(before, table.Clients[a].Run.State.FogOfWar);
		table.Deliver();
		table.HostRun.Advance(0);
		Assert.Equal(!before, table.HostRun.State.FogOfWar);

		table.ClientsCatchUp();
		Assert.Equal(!before, table.Clients[a].Run.State.FogOfWar);
	}

	[Fact]
	public void AnHourIsNotBegunBeforeTheHostSaysItIsWhole()
	{
		var table = new CTable();
		int a = table.Join("A");
		long start = table.HostRun.State.TotalHours;
		table.HostHour();

		// Everything of the hour but its last line has come.
		var client = table.Clients[a];
		while (client.ToClient.Count > 0 && client.ToClient.Peek()[0] != CWire.HourDone)
		{
			client.Session.Receive(client.ToClient.Dequeue());
		}
		Assert.NotEmpty(client.ToClient);
		Assert.Equal(0, client.Session.Step(mostHours: 5));
		Assert.Equal(start, client.Run.State.TotalHours);

		table.ClientsCatchUp();
		Assert.Equal(start + 1, client.Run.State.TotalHours);
		Assert.Equal(start + 1, client.Peer.ConfirmedHour);
	}

	[Fact]
	public void TheHostWaitsForAMachineMoreThanTheWindowBehind()
	{
		var table = new CTable(window: 2);
		int fast = table.Join("fast");
		int slow = table.Join("slow");
		long start = table.HostRun.State.TotalHours;

		// The slow machine's lines are not delivered: the host runs two hours and then no more.
		for (int i = 0; i < 6; i++)
		{
			table.HostHour();
			table.Deliver(fast);
			table.Clients[fast].Session.Step(mostHours: 100);
			table.Deliver(fast);
		}
		Assert.Equal(start + 2, table.HostRun.State.TotalHours);
		Assert.Equal(new[] { "slow" }, table.Host.WaitingFor().ConvertAll(peer => peer.Name));
		Assert.Equal(new[] { "slow" }, table.Clients[fast].Session.WaitingFor);

		// It catches up, and the host goes on.
		table.ClientsCatchUp();
		table.HostHour();
		Assert.Equal(start + 3, table.HostRun.State.TotalHours);
		table.ClientsCatchUp();
		Assert.Empty(table.Clients[fast].Session.WaitingFor);
	}

	[Fact]
	public void AnybodyPausesAndTheClientsClockShowsTheHosts()
	{
		var table = new CTable();
		int a = table.Join("A");
		table.HostRun.Pace.Set(paused: false, 3);
		table.Host.Tell();
		table.Deliver();
		Assert.False(table.Clients[a].Run.Pace.Paused);
		Assert.Equal(3, table.Clients[a].Run.Pace.Speed);

		// The client's player pauses: nothing here until the host has taken it and said so.
		table.Clients[a].Run.Pace.TogglePause();
		Assert.False(table.Clients[a].Run.Pace.Paused);
		table.Deliver();
		Assert.True(table.HostRun.Pace.Paused);
		table.Host.Tell();
		table.Deliver();
		Assert.True(table.Clients[a].Run.Pace.Paused);

		// The speed is not the client's to set.
		Assert.False(table.Clients[a].Run.Pace.ChangeSpeed(1));
	}

	[Fact]
	public void TheMidnightsHashAgreesDayAfterDay()
	{
		var table = new CTable(window: 48);
		int a = table.Join("A");
		for (int hour = 0; hour < 72; hour++)
		{
			table.HostHour();
			if (hour % 5 == 0)
			{
				table.ClientsCatchUp();
			}
		}
		table.ClientsCatchUp();

		Assert.False(table.Clients[a].Session.Parted);
		Assert.Equal(table.HostRun.State.TotalHours, table.Clients[a].Run.State.TotalHours);
		Assert.Equal(CStateHash.Of(table.HostRun.State), CStateHash.Of(table.Clients[a].Run.State));
	}

	[Fact]
	public void AClientWhoseGameHasPartedSaysSoAndIsNoLongerWaitedFor()
	{
		var table = new CTable(window: 2);
		int a = table.Join("A");
		CHostPeer? told = null;
		table.Host.Parted += peer => told = peer;

		// Something only this machine did to its game.
		table.Clients[a].Run.State.Provinces[1].Owner = new CCountryTag("GER");
		for (int hour = 0; hour < 24; hour++)
		{
			table.HostHour();
			table.ClientsCatchUp();
		}

		Assert.True(table.Clients[a].Session.Parted);
		Assert.Same(table.Clients[a].Peer, told);
		Assert.Empty(table.Host.WaitingFor());
		// It runs nothing more until it is sent the game again.
		long stoppedAt = table.Clients[a].Run.State.TotalHours;
		table.HostHour();
		table.ClientsCatchUp();
		Assert.Equal(stoppedAt, table.Clients[a].Run.State.TotalHours);
	}

	[Fact]
	public void AClientGonePausesTheGame()
	{
		var table = new CTable();
		int a = table.Join("A");
		table.HostHour();
		Assert.False(table.HostRun.Pace.Paused);

		table.Host.Drop(table.Clients[a].Peer);
		Assert.True(table.HostRun.Pace.Paused);
		Assert.Empty(table.Host.Peers);
	}
}
