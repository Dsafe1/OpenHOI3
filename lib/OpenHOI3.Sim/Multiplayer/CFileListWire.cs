using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// A machine's list of the files its play checksum is made of, as it travels (<c>docs/multiplayer.md</c>, section 15):
/// its lines joined, deflated as a game is (<see cref="CGameSender.Pack"/>) and cut into pieces of base64, a piece a
/// line of the wire's <see cref="CWire.Files"/>.
/// </summary>
public static class CFileListWire
{
	/// <summary>How many bytes of the deflated list a piece holds, as a game's piece.</summary>
	public const int PieceBytes = 48000;

	/// <summary>The lines to send, in order.</summary>
	public static List<string> Lines(IEnumerable<string> list)
	{
		byte[] packed = CGameSender.Pack(Encoding.UTF8.GetBytes(string.Join("\n", list)));
		var lines = new List<string>();
		for (int at = 0; at < packed.Length; at += PieceBytes)
		{
			lines.Add(CWire.Line(CWire.Files, Convert.ToBase64String(packed, at, Math.Min(PieceBytes, packed.Length - at))));
		}
		return lines;
	}

	/// <summary>The list read back from the pieces as they came, each a line's text after its letter; null where they are no list.</summary>
	public static IReadOnlyList<string>? Read(IEnumerable<string> pieces)
	{
		var packed = new List<byte>();
		try
		{
			foreach (string piece in pieces)
			{
				packed.AddRange(Convert.FromBase64String(piece.Trim()));
			}
		}
		catch (FormatException)
		{
			return null;
		}
		return CGameSender.Unpack(packed.ToArray()) is { } bytes ? Encoding.UTF8.GetString(bytes).Split('\n') : null;
	}
}
