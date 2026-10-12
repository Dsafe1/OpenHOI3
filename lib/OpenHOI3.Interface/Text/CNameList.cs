using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// A list of names as the original's diplomacy texts write one: separated by ", " while two or more
/// remain, and by " " and the text of <c>AND</c> - "and " - before the last. "A, B and C". See
/// <c>docs/mechanics/diplomacyscreen.md</c>, section 5.2.
/// </summary>
public static class CNameList
{
	public static string Join(Func<string, string> localise, IReadOnlyList<string> names)
	{
		var text = new StringBuilder();
		for (int i = 0; i < names.Count; i++)
		{
			text.Append(names[i]);
			int remaining = names.Count - 1 - i;
			if (remaining >= 2)
			{
				text.Append(", ");
			}
			else if (remaining == 1)
			{
				text.Append(' ').Append(localise("AND"));
			}
		}
		return text.ToString();
	}
}
