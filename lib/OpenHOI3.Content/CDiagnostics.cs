using System;

namespace OpenHOI3.Content;

/// <summary>
/// Default destination for loader diagnostics.
///
/// The data loaders take a callback rather than reporting directly, and this is what
/// they fall back to when a caller does not pass one. It writes to standard error until
/// told otherwise: this library has no engine in it, so the client points the sinks at
/// its own logs when it starts, and a test passes its own callback instead.
///
/// <b>Two kinds of report.</b> A <see cref="Problem(string)"/> is a fault in the install's or a mod's own files - a key
/// nothing reads, a name nothing declares, a localisation key given twice - which a modder can mend, and the client writes
/// them one to a line into <c>openhoi3_validation.log</c> (<c>docs/mechanics/logs.md</c>, section 4). A <see cref="Warn"/> is about
/// this program or this run - what is not built, a deliberate departure, a file that could not be written - and goes to
/// the run's own log.
/// </summary>
public static class CDiagnostics
{
	public static Action<string> WarnSink { get; set; } = Console.Error.WriteLine;

	public static Action<string> ErrorSink { get; set; } = Console.Error.WriteLine;

	/// <summary>Where a fault in the content goes: one line each, its place first where it is known.</summary>
	public static Action<string> ProblemSink { get; set; } = message => Console.Error.WriteLine("PROBLEM: " + message);

	public static void Warn(string message)
	{
		WarnSink(message);
	}

	public static void Error(string message)
	{
		ErrorSink(message);
	}

	/// <summary>A fault in the content whose message says where it is, or that is of no one file.</summary>
	public static void Problem(string message)
	{
		ProblemSink(message);
	}

	/// <summary>A fault in the content at a file and a line of it; a line of nought or less is the file as a whole.</summary>
	public static void Problem(string file, int line, string message)
	{
		ProblemSink(Located(file, line, message));
	}

	/// <summary>
	/// A problem's line: <c>file:line: message</c>, <c>file: message</c> where no line is known, or the message alone where
	/// no file is - the form compilers write, which editors and terminals open at the place.
	/// </summary>
	public static string Located(string file, int line, string message)
	{
		if (file.Length == 0)
		{
			return message;
		}
		return line > 0 ? $"{file}:{line}: {message}" : $"{file}: {message}";
	}
}
