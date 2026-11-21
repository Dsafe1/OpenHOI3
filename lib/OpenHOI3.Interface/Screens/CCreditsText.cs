using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The credits' file, <c>interface/credits.txt</c>, cut into lines as the original cuts it: at every line feed and at
/// the file's last byte, whatever that byte is; a line whose first byte is the section sign is bold and loses it. There
/// is no other markup, and an empty line is a row like any other. See <c>docs/mechanics/frontend.md</c>, section 10.
/// </summary>
public static class CCreditsText
{
	/// <summary>The file, under the install's or a mod's root.</summary>
	public const string File = "interface/credits.txt";

	/// <summary>The byte that makes a line bold where it comes first: the section sign in Windows-1252.</summary>
	private const byte Bold = 0xA7;

	/// <summary>
	/// The file's lines added to a list, in order. The original adds to the lines of every visit before and never
	/// empties them, so the caller keeps one list. Ours: the carriage return a line ends in is dropped, where the
	/// original leaves it in the text.
	/// </summary>
	public static void Append(List<CCreditsLine> lines, byte[] file)
	{
		int start = 0;
		for (int at = 0; at < file.Length; at++)
		{
			// The last byte ends the last line as a line feed would, and is no part of it either.
			if (file[at] != (byte)'\n' && at != file.Length - 1)
			{
				continue;
			}
			bool bold = at > start && file[start] == Bold;
			int from = bold ? start + 1 : start;
			int length = at - from;
			if (length > 0 && file[from + length - 1] == (byte)'\r')
			{
				length--;
			}
			lines.Add(new CCreditsLine(Encoding.Latin1.GetString(file, from, length), bold));
			start = at + 1;
		}
	}
}
