using System;
using System.Globalization;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// A message's words: its type's texts by suffix, each place filled with its value looked up as a
/// key, and a text that comes to exactly <c>*</c> nothing. A popup's header has its first white
/// turned to <c>§b</c> and its lines every white. See <c>docs/mechanics/messages.md</c>, sections 1,
/// 3, 4 and 5.
/// </summary>
public static class CMessageText
{
	private static readonly string[] Months =
	{
		"January", "February", "March", "April", "May", "June",
		"July", "August", "September", "October", "November", "December",
	};

	/// <summary>A type's text of a suffix - <c>_HEADER</c>, <c>_1</c>, <c>_LOG</c> - filled, and nothing for <c>*</c>.</summary>
	public static string Text(Func<string, string> localise, CMessage message, string suffix)
	{
		string text = localise(message.Type.Name + suffix);
		foreach ((string key, string value) in message.Variables)
		{
			text = text.Replace("$" + key + "$", localise(value));
		}
		return text == "*" ? "" : text;
	}

	/// <summary>The popup's header: its first white made dark.</summary>
	public static string Header(Func<string, string> localise, CMessage message)
	{
		string text = Text(localise, message, "_HEADER");
		int white = text.IndexOf("§W", StringComparison.Ordinal);
		return white < 0 ? text : text[..white] + "§b" + text[(white + 2)..];
	}

	/// <summary>One of the popup's six lines, every white made dark.</summary>
	public static string Line(Func<string, string> localise, CMessage message, int line)
	{
		return Text(localise, message, "_" + line.ToString(CultureInfo.InvariantCulture)).Replace("§W", "§b");
	}

	/// <summary>A line of the log: the date, a space, and the type's <c>_LOG</c>.</summary>
	public static string Log(Func<string, string> localise, CMessage message)
	{
		return message.Date + " " + Text(localise, message, "_LOG");
	}

	/// <summary>" 13:00, 1 January, 1936", a space first, as the original writes an hour's date.</summary>
	public static string Date(Func<string, string> localise, CDate date, int hour)
	{
		return string.Format(CultureInfo.InvariantCulture, " {0}:00, {1} {2}, {3}", hour, date.Day,
			localise(Months[Math.Clamp(date.Month, 1, 12) - 1]), date.Year);
	}
}
