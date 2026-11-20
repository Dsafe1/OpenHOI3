using System;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Replay;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A game between two machines with nothing between them but what would go over a wire (<c>docs/multiplayer.md</c>):
/// the host's game records, the client's follows the recording as it is made - every entry through the form a
/// recording has on disk - and what the client's player orders goes to the host in that form and comes back in the
/// host's order.
/// </summary>
public class TwoMachinesTests
{
	private static CGameState State()
	{
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>());
	}

	private sealed class CPair
	{
		public CGameState Host { get; } = State();
		public CGameState Client { get; } = State();
		public CSimulation HostRun { get; }
		public CSimulation ClientRun { get; }
		public CReplay Recording { get; } = CReplay.Recording();

		public CPair()
		{
			HostRun = new CSimulation(Host);
			ClientRun = new CSimulation(Client);
			Host.Replay = Recording;
			Client.Replay = CReplay.Following(Recording, entry => CReplayFile.Through(entry, Client));
			// The client's order, over the wire: read on the host against the host's own game.
			ClientRun.ToHost = command => HostRun.Post(((CReplayPost)CReplayFile.Through(new CReplayPost(0, command), Host)).Command);
		}
	}

	[Fact]
	public void AClientsOrderGivenWhileTheClockStandsRunsOnBothAtOnce()
	{
		var pair = new CPair();
		bool before = pair.Host.FogOfWar;

		Assert.True(pair.ClientRun.Post(new CFogOfWarCommand(!before)));
		// Not the client's to run: it has gone to the host, and has come back posted and not yet run.
		pair.ClientRun.Follow();
		Assert.Equal(before, pair.Client.FogOfWar);

		// The host's next frame, its clock standing, runs it.
		pair.HostRun.Advance(0);
		Assert.Equal(!before, pair.Host.FogOfWar);

		// And the client runs it as soon as it has come back, with no hour between.
		Assert.True(pair.ClientRun.Follow());
		Assert.Equal(!before, pair.Client.FogOfWar);
		Assert.Equal(pair.Host.TotalHours, pair.Client.TotalHours);
	}

	[Fact]
	public void AnOrderOfTheHostsOwnPlayerReachesTheClientToo()
	{
		var pair = new CPair();
		bool before = pair.Host.FogOfWar;

		pair.HostRun.Post(new CFogOfWarCommand(!before));
		pair.HostRun.Advance(0);
		pair.ClientRun.Follow();

		Assert.Equal(!before, pair.Client.FogOfWar);
	}

	[Fact]
	public void TheClientRunsAnHourOnceTheHostHas()
	{
		var pair = new CPair();
		Assert.False(pair.ClientRun.HourHasCome);

		pair.ClientRun.Post(new CFogOfWarCommand(!pair.Host.FogOfWar));
		long start = pair.Client.TotalHours;
		pair.HostRun.RunHours(1);
		// What was ordered before the hour is taken first; then the hour is next.
		pair.ClientRun.Follow();
		Assert.True(pair.ClientRun.HourHasCome);
		pair.ClientRun.RunHours(1);

		Assert.Equal(start + 1, pair.Client.TotalHours);
		Assert.Equal(pair.Host.FogOfWar, pair.Client.FogOfWar);
		Assert.Equal(CStateHash.Of(pair.Host), CStateHash.Of(pair.Client));
		Assert.False(pair.ClientRun.HourHasCome);
	}

	[Fact]
	public void OrdersGivenPausedAndThenHoursKeepTheTwoAlikeDayByDay()
	{
		var pair = new CPair();
		for (int hour = 0; hour < 72; hour++)
		{
			if (hour % 7 == 0)
			{
				// One from each machine in the same frame of the host's, the clock standing.
				pair.ClientRun.Post(new CFogOfWarCommand(hour % 2 == 0));
				pair.HostRun.Post(new CFogOfWarCommand(hour % 2 != 0));
				pair.HostRun.Advance(0);
				pair.ClientRun.Follow();
				Assert.Equal(pair.Host.FogOfWar, pair.Client.FogOfWar);
			}
			pair.HostRun.RunHours(1);
			pair.ClientRun.RunHours(1);
			if (pair.Host.Hour == 0)
			{
				Assert.Equal(CStateHash.Of(pair.Host), CStateHash.Of(pair.Client));
			}
		}
		Assert.Equal(pair.Host.TotalHours, pair.Client.TotalHours);
		Assert.Contains(pair.Recording.Entries, entry => entry is CReplayDrain);
	}

	[Fact]
	public void ARecordingWithPausedDrainsStillPlaysBackFromItsStart()
	{
		var pair = new CPair();
		pair.HostRun.Post(new CFogOfWarCommand(!pair.Host.FogOfWar));
		pair.HostRun.Advance(0);
		pair.HostRun.RunHours(2);

		CGameState replayed = State();
		var run = new CSimulation(replayed);
		replayed.Replay = CReplay.Replaying(pair.Recording.Entries.ToList());
		run.RunHours(2);

		Assert.Equal(pair.Host.FogOfWar, replayed.FogOfWar);
		Assert.Equal(CStateHash.Of(pair.Host), CStateHash.Of(replayed));
	}
}
