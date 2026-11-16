using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// Counts the errors and warnings Godot's engine logs - its own, which reach no sink of ours - so that a check can say
/// which of them a step of its own brought on. Godot may call it from any thread, so it locks.
/// </summary>
public partial class CGodotMessageCounter : Logger
{
	private readonly object _lock = new();
	private int _count;
	private string? _first;

	public override void _LogError(string function, string file, int line, string code, string rationale, bool editorNotify,
		int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
	{
		lock (_lock)
		{
			_count++;
			_first ??= rationale.Length > 0 ? rationale : code;
		}
	}

	/// <summary>How many messages were logged since the last call, and the first of them; the count starts again.</summary>
	public int Take(out string? first)
	{
		lock (_lock)
		{
			int count = _count;
			first = _first;
			_count = 0;
			_first = null;
			return count;
		}
	}
}
