using System;
using System.Net;
using System.Net.Sockets;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>A host's open port: each machine that connects becomes a <see cref="CLineLink"/> (<c>docs/multiplayer.md</c>, section 4.1).</summary>
public sealed class CLineListener : IDisposable
{
	private readonly TcpListener _listener;

	/// <param name="port">The port to listen on; nought for one the system picks, which <see cref="Port"/> then names.</param>
	public CLineListener(int port = CLineLink.DefaultPort)
	{
		_listener = new TcpListener(IPAddress.Any, port);
		_listener.Start();
	}

	/// <summary>The port listened on.</summary>
	public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

	/// <summary>The next machine that has connected, where one has.</summary>
	public bool TryAccept(out CLineLink link)
	{
		if (_listener.Pending())
		{
			link = new CLineLink(_listener.AcceptTcpClient());
			return true;
		}
		link = null!;
		return false;
	}

	public void Dispose()
	{
		_listener.Stop();
	}
}
