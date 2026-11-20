using System;
using System.Globalization;
using System.IO;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// A client's taking of a game from the host (<see cref="CGameSender"/>): offered a game by its hash, it answers that
/// it holds those bytes already - asked of the caller, who knows its saves - or takes the whole in pieces and checks
/// them against the hash. The bytes are then the caller's to load; when it has, it says so with <see cref="SayLoaded"/>.
/// </summary>
public sealed class CGameReceiver
{
	private readonly Action<string> _send;
	private readonly Func<string, byte[]?> _own;
	private MemoryStream? _pieces;
	private string _hash = "";

	/// <param name="send">Sends the host a line.</param>
	/// <param name="own">The bytes of a game this machine holds with that hash, or null where it holds none.</param>
	public CGameReceiver(Action<string> send, Func<string, byte[]?> own)
	{
		_send = send;
		_own = own;
	}

	/// <summary>The game, once it is here whole: this machine's own copy, or what was sent.</summary>
	public byte[]? Game { get; private set; }

	/// <summary>Whether the game was this machine's own copy, nothing having been sent.</summary>
	public bool Kept { get; private set; }

	/// <summary>Whether what was sent was not the game offered: its hash differs.</summary>
	public bool Failed { get; private set; }

	/// <summary>A line from the host; answers whether it was one of the sending's.</summary>
	public bool Receive(string line)
	{
		if (line.Length == 0)
		{
			return false;
		}
		string rest = line.Length > 2 ? line[2..] : "";
		switch (line[0])
		{
			case CWire.GameOffered:
			{
				string[] parts = rest.Split(' ');
				_hash = parts[0];
				Game = null;
				Failed = false;
				if (_own(_hash) is { } own && CGameSender.Hash(own) == _hash)
				{
					Game = own;
					Kept = true;
					_send(CWire.Line(CWire.GameKept));
					return true;
				}
				Kept = false;
				_pieces = new MemoryStream(parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0);
				_send(CWire.Line(CWire.GameWanted));
				return true;
			}
			case CWire.GamePiece:
				if (_pieces is not null)
				{
					byte[] piece = Convert.FromBase64String(rest);
					_pieces.Write(piece, 0, piece.Length);
				}
				return true;
			case CWire.GameSent:
				if (_pieces is not null)
				{
					byte[] game = _pieces.ToArray();
					_pieces = null;
					if (CGameSender.Hash(game) == _hash)
					{
						Game = game;
					}
					else
					{
						Failed = true;
					}
				}
				return true;
			default:
				return false;
		}
	}

	/// <summary>Tells the host this machine has loaded the game.</summary>
	public void SayLoaded()
	{
		_send(CWire.Line(CWire.GameLoaded));
	}
}
