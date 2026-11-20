using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenHOI3.Sim.Multiplayer;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The connection lines travel by (<c>docs/multiplayer.md</c>, section 4.1), over this machine's own loopback on a port
/// the system picks: lines each way, in order, as they are sent; long ones; and the other side going.
/// </summary>
public class LineLinkTests
{
	private static (CLineListener Listener, CLineLink Host, CLineLink Client) Connected()
	{
		var listener = new CLineListener(port: 0);
		CLineLink client = CLineLink.Connect("127.0.0.1", listener.Port);
		CLineLink? host = null;
		Wait(() => listener.TryAccept(out host!));
		return (listener, host!, client);
	}

	private static void Wait(Func<bool> until)
	{
		var clock = Stopwatch.StartNew();
		while (!until())
		{
			Assert.True(clock.ElapsedMilliseconds < 5000, "nothing came within five seconds");
			Thread.Sleep(1);
		}
	}

	private static List<string> Take(CLineLink link, int count)
	{
		var lines = new List<string>();
		Wait(() =>
		{
			while (link.TryReceive(out string line))
			{
				lines.Add(line);
			}
			return lines.Count >= count;
		});
		return lines;
	}

	[Fact]
	public void LinesComeEachWayInTheOrderSentWithNoWaitingForMore()
	{
		(CLineListener listener, CLineLink host, CLineLink client) = Connected();
		using (listener)
		using (host)
		using (client)
		{
			host.Send("e first");
			host.Send("d 1");
			// Each is read as it comes: nothing is held back for a fuller buffer.
			Assert.Equal(new[] { "e first", "d 1" }, Take(client, 2));

			client.Send("c 1");
			Assert.Equal(new[] { "c 1" }, Take(host, 1));
		}
	}

	[Fact]
	public void ALongLineAndManyShortOnesComeWhole()
	{
		(CLineListener listener, CLineLink host, CLineLink client) = Connected();
		using (listener)
		using (host)
		using (client)
		{
			string longLine = "e " + new string('x', 200_000) + " ö";
			host.Send(longLine);
			for (int i = 0; i < 500; i++)
			{
				host.Send("e " + i);
			}
			List<string> came = Take(client, 501);
			Assert.Equal(longLine, came[0]);
			Assert.Equal("e 499", came[500]);
		}
	}

	[Fact]
	public void LinesSentFromAnotherThreadComeWholeToo()
	{
		(CLineListener listener, CLineLink host, CLineLink client) = Connected();
		using (listener)
		using (host)
		using (client)
		{
			var worker = new Thread(() =>
			{
				for (int i = 0; i < 200; i++)
				{
					host.Send("worker " + i);
				}
			});
			worker.Start();
			for (int i = 0; i < 200; i++)
			{
				host.Send("main " + i);
			}
			worker.Join();
			List<string> came = Take(client, 400);
			Assert.Equal(400, came.Count);
			Assert.All(came, line => Assert.Matches("^(worker|main) [0-9]+$", line));
		}
	}

	[Fact]
	public void TheOtherSideGoingIsSeen()
	{
		(CLineListener listener, CLineLink host, CLineLink client) = Connected();
		using (listener)
		using (host)
		{
			Assert.False(host.Closed);
			client.Dispose();
			Wait(() => host.Closed);
		}
	}
}
