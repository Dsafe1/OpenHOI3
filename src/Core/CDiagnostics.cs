using Godot;

namespace OpenHOI3.Core;

/// <summary>
/// Default destination for loader diagnostics.
///
/// The data loaders take a callback rather than calling the engine directly, because
/// Godot's logging functions are native calls: invoking one outside a running engine
/// kills the process, which makes anything that might warn untestable. This supplies
/// the in-engine default for callers that do not care.
/// </summary>
public static class CDiagnostics
{
	public static void Warn(string message)
	{
		GD.PushWarning(message);
	}

	public static void Error(string message)
	{
		GD.PushError(message);
	}
}
