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
	private readonly Thread _writer;
	private readonly ConcurrentQueue<string> _toSend = new();
	private readonly SemaphoreSlim _some = new(0);
	private long _queuedChars;
	private long _sentChars;
	private volatile bool _closed;
	private volatile bool _ending;

	public CLineLink(TcpClient client)
	{
		_client = client;
		client.NoDelay = true;
		NetworkStream stream = client.GetStream();
		_out = new DeflateStream(stream, CompressionLevel.Fastest, leaveOpen: true);
		_reader = new Thread(() => Read(stream)) { IsBackground = true, Name = "OpenHOI3 line link" };
		_reader.Start();
		_writer = new Thread(Write) { IsBackground = true, Name = "OpenHOI3 line link, sending" };
		_writer.Start();
	}

	/// <summary>A connection made to a host.</summary>
	public static CLineLink Connect(string address, int port = DefaultPort)
	{
		return new CLineLink(new TcpClient(address, port));
	}

	/// <summary>Whether the other side has gone, or the connection failed: nothing more will come, and what is sent goes nowhere.</summary>
	public bool Closed => _closed;

	/// <summary>
	/// The other machine's address as text, an IPv4 address that came through an IPv6 socket as the IPv4 it is; empty
	/// where it is not to be had. What a host knows a banned machine by.
	/// </summary>
	public string RemoteAddress
	{
		get
		{
			try
			{
				if (_client.Client.RemoteEndPoint is System.Net.IPEndPoint remote)
				{
					System.Net.IPAddress address = remote.Address;
					return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
				}
			}
			catch (Exception failed) when (failed is SocketException or ObjectDisposedException)
			{
				// Gone already.
			}
			return "";
		}
	}

	/// <summary>
	/// Sends one line. It may hold no line break. **It does not wait for the connection**: the line joins those still
	/// to go, which a thread of the link's own writes in the order they were given - so a game of many megabytes sent
	/// to a slow machine holds up nobody's frame, and several machines are sent to at once
	/// (<c>docs/multiplayer.md</c>, section 14).
	/// </summary>
	public void Send(string line)
	{
		if (_closed || _ending)
		{
			return;
		}
		lock (_sending)
		{
			_queuedChars += line.Length;
			_toSend.Enqueue(line);
		}
		_some.Release();
	}

	/// <summary>How many characters have been given to <see cref="Send"/> since the link was made.</summary>
	public long QueuedChars => Interlocked.Read(ref _queuedChars);

	/// <summary>How many of them the connection has taken.</summary>
	public long SentChars => Interlocked.Read(ref _sentChars);

	private void Write()
	{
		try
		{
			while (true)
			{
				_some.Wait();
				if (!_toSend.TryDequeue(out string? line))
				{
					// Woken with nothing to send: the link is ending, and everything given it has gone.
					return;
				}
				byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
				_out.Write(bytes, 0, bytes.Length);
				_out.Flush();
				Interlocked.Add(ref _sentChars, line.Length);
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
		// What was given to be sent goes first - a machine told why it is let go has to be told - for a little while.
		_ending = true;
		_some.Release();
		if (!_closed && Thread.CurrentThread != _writer)
		{
			_writer.Join(TimeSpan.FromSeconds(2));
		}
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
