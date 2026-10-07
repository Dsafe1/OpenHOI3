using System;

namespace OpenHOI3.Content;

/// <summary>
/// Default destination for loader diagnostics.
///
/// The data loaders take a callback rather than reporting directly, and this is what
/// they fall back to when a caller does not pass one. It writes to standard error until
/// told otherwise: this library has no engine in it, so the client points both sinks at
/// Godot's own log when it starts, and a test passes its own callback instead.
/// </summary>
public static class CDiagnostics
{
	public static Action<string> WarnSink { get; set; } = Console.Error.WriteLine;

	public static Action<string> ErrorSink { get; set; } = Console.Error.WriteLine;

	public static void Warn(string message)
	{
		WarnSink(message);
	}

	public static void Error(string message)
	{
		ErrorSink(message);
	}
}
