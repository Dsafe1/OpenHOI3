using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The head of a save, as the lobby's row reads it: by position, not by key - the first value its date, and the
/// <c>player</c> second, or third after a <c>scenario</c>. A save whose second key is neither has no player. See
/// <c>docs/mechanics/frontend.md</c>, section 3; <c>reversing/findings/FINDINGS-frontend.md</c>, B.3.
/// </summary>
public sealed record CSaveHeader(long? Hours, CCountryTag? Player)
{
	/// <summary>How much of the file is read: the head's three lines, with room to spare.</summary>
	private const int HeadBytes = 512;

	public static CSaveHeader Read(string path)
	{
		byte[] head = new byte[HeadBytes];
		int read;
		try
		{
			using FileStream file = File.OpenRead(path);
			read = file.Read(head, 0, head.Length);
		}
		catch (IOException)
		{
			return new CSaveHeader(null, null);
		}
		return Parse(Encoding.Latin1.GetString(head, 0, read));
	}

	/// <summary>The head of a save's text: its first three <c>key=value</c> lines.</summary>
	public static CSaveHeader Parse(string text)
	{
		var lines = new List<(string Key, string Value)>();
		foreach (string line in text.Split('\n'))
		{
			int equals = line.IndexOf('=');
			if (equals > 0)
			{
				lines.Add((line[..equals].Trim(), line[(equals + 1)..].Trim().Trim('"')));
			}
			if (lines.Count == 3)
			{
				break;
			}
		}
		long? hours = lines.Count > 0 && CSaveValues.TryHours(lines[0].Value, out long at) ? at : null;
		int playerAt = lines.Count > 1 && lines[1].Key == "scenario" ? 2 : 1;
		CCountryTag? player = lines.Count > playerAt && lines[playerAt].Key == "player"
			&& CCountryTag.TryParse(lines[playerAt].Value, out CCountryTag tag) ? tag : null;
		return new CSaveHeader(hours, player);
	}
}
