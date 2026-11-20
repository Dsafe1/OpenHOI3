using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// One connection between two machines of a game, carrying lines of text each way (<c>docs/multiplayer.md</c>, section
/// 4.1): TCP, each direction one Deflate stream at its fastest, flushed after every line so that a line can be read as
/// it comes. A line may be sent from any thread - an hour's entries are sent from the thread that runs the hour - and
/// lines that came are taken on the thread that asks, in the order they came.
/// </summary>
public sealed class CLineLink : IDisposable
{
	/// <summary>The port a host listens on where it names no other. Ours; the original's is no guide, its network not being followed.</summary>
	public const int DefaultPort = 19390;

	private readonly TcpClient _client;
	private readonly DeflateStream _out;
	private readonly object _sending = new();
	private readonly ConcurrentQueue<string> _came = new();
	private readonly Thread _reader;
	private volatile bool _closed;

	public CLineLink(TcpClient client)
	{
		_client = client;
		client.NoDelay = true;
		NetworkStream stream = client.GetStream();
		_out = new DeflateStream(stream, CompressionLevel.Fastest, leaveOpen: true);
		_reader = new Thread(() => Read(stream)) { IsBackground = true, Name = "OpenHOI3 line link" };
		_reader.Start();
	}

	/// <summary>A connection made to a host.</summary>
	public static CLineLink Connect(string address, int port = DefaultPort)
	{
		return new CLineLink(new TcpClient(address, port));
	}

	/// <summary>Whether the other side has gone, or the connection failed: nothing more will come, and what is sent goes nowhere.</summary>
	public bool Closed => _closed;

	/// <summary>Sends one line. It may hold no line break.</summary>
	public void Send(string line)
	{
		if (_closed)
		{
			return;
		}
		byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
		try
		{
			lock (_sending)
			{
				_out.Write(bytes, 0, bytes.Length);
				_out.Flush();
			}
		}
		catch (Exception failed) when (failed is IOException or ObjectDisposedException or SocketException)
		{
			_closed = true;
		}
	}

	/// <summary>The next line that came, where one has.</summary>
	public bool TryReceive(out string line)
	{
		return _came.TryDequeue(out line!);
	}

	private void Read(NetworkStream stream)
	{
		try
		{
			using var inflate = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true);
			using var reader = new StreamReader(inflate, Encoding.UTF8);
			while (reader.ReadLine() is { } line)
			{
				_came.Enqueue(line);
			}
		}
		catch (Exception failed) when (failed is IOException or ObjectDisposedException or SocketException or InvalidDataException)
		{
			// The connection is gone; said below.
		}
		_closed = true;
	}

	public void Dispose()
	{
		_closed = true;
		try
		{
			_client.Close();
		}
		catch (SocketException)
		{
			// Already gone.
		}
	}
}
