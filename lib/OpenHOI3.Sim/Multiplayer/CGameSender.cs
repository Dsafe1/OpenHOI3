using System;
using System.Globalization;
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
		_send(CWire.Line(CWire.GameOffered, Hash(_game) + " " + _game.Length.ToString(CultureInfo.InvariantCulture)));
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
				for (int at = 0; at < _game.Length; at += PieceBytes)
				{
					_send(CWire.Line(CWire.GamePiece, Convert.ToBase64String(_game, at, Math.Min(PieceBytes, _game.Length - at))));
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
