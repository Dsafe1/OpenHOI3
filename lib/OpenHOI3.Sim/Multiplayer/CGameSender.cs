using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// The host's sending of a game to one client (<c>docs/multiplayer.md</c>, section 4.1): the game as the bytes of a
/// save, offered by its hash; a client that holds the same bytes already is sent nothing, any other the whole of it in
/// pieces; and the client says when it has loaded it. Making the save, and loading it, are the caller's - a machine
/// that joins, one whose midnight's hash was not the host's, and every machine when one of those happens, all load
/// the one save.
/// </summary>
public sealed class CGameSender
{
	/// <summary>The bytes of one piece before they are written as base64: a line of about 64,000 characters.</summary>
	public const int PieceBytes = 48_000;

	private readonly Action<string> _send;
	private readonly byte[] _game;

	/// <summary>The game deflated, made the first time it is asked for: what the pieces are cut from.</summary>
	private byte[]? _packed;

	private byte[] Packed => _packed ??= Pack(_game);

	/// <summary>
	/// A game's bytes deflated for the way. The connection's own stream deflates every line too, but a piece is base64,
	/// in which the same text three bytes on is other letters, and that stream finds far fewer repeats in it: a save of
	/// 14.0 MB went as 3.2 MB so, and deflated first as 2.5 (<c>docs/multiplayer.md</c>, section 3).
	/// </summary>
	public static byte[] Pack(byte[] game)
	{
		using var packed = new MemoryStream();
		using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
		{
			deflate.Write(game, 0, game.Length);
		}
		return packed.ToArray();
	}

	/// <summary>The bytes <see cref="Pack"/> made of a game, or none where they are no such bytes.</summary>
	public static byte[]? Unpack(byte[] packed)
	{
		try
		{
			using var deflate = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
			using var game = new MemoryStream();
			deflate.CopyTo(game);
			return game.ToArray();
		}
		catch (InvalidDataException)
		{
			return null;
		}
	}

	public CGameSender(Action<string> send, byte[] game)
	{
		_send = send;
		_game = game;
	}

	/// <summary>Whether the client had the game already and was sent nothing.</summary>
	public bool Kept { get; private set; }

	/// <summary>Whether the client has said it has loaded the game.</summary>
	public bool Loaded { get; private set; }

	/// <summary>A game's hash: SHA-256 of its bytes, in hexadecimal.</summary>
	public static string Hash(byte[] game) => Convert.ToHexString(SHA256.HashData(game));

	/// <summary>The game offered.</summary>
	public void Offer()
	{
		// The game's own hash, by which a client knows a save it holds, and how many bytes will come: the packed ones.
		_send(CWire.Line(CWire.GameOffered, Hash(_game) + " " + Packed.Length.ToString(CultureInfo.InvariantCulture)));
	}

	/// <summary>A line from the client; answers whether it was one of the sending's.</summary>
	public bool Receive(string line)
	{
		if (line.Length == 0)
		{
			return false;
		}
		switch (line[0])
		{
			case CWire.GameKept:
				Kept = true;
				return true;
			case CWire.GameWanted:
				byte[] packed = Packed;
				for (int at = 0; at < packed.Length; at += PieceBytes)
				{
					_send(CWire.Line(CWire.GamePiece, Convert.ToBase64String(packed, at, Math.Min(PieceBytes, packed.Length - at))));
				}
				_send(CWire.Line(CWire.GameSent));
				return true;
			case CWire.GameLoaded:
				Loaded = true;
				return true;
			default:
				return false;
		}
	}
}
