using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// Everything the game prints - each line Godot logs, ours and the engine's, warnings and errors with where they were raised -
/// written to <c>openhoi3.log</c> in the player's folder's <c>logs</c>, where the original keeps its own
/// (<c>OpenHOI3.Content.Logs.CLogFiles</c>; <c>docs/mechanics/logs.md</c>). The folder is known only once the install and the
/// mod are, so what is logged before that is kept and written first. Each line is flushed as it is written, so a crash
/// loses nothing of it. Godot may call it from any thread, so it locks.
/// </summary>
public partial class CFileLog : Logger
{
	private readonly object _lock = new();
	private readonly List<string> _early = new();
	private StreamWriter? _writer;

	/// <summary>The one log of the process, from its start.</summary>
	public static CFileLog? Instance { get; private set; }

	/// <summary>Makes the log and has Godot hand it every message from now on; once is enough.</summary>
	public static void Install()
	{
		if (Instance is not null)
		{
			return;
		}
		Instance = new CFileLog();
		OS.AddLogger(Instance);
	}

	/// <summary>Starts writing to a file: what was logged before it first. A file that cannot be opened leaves the log unwritten.</summary>
	public void Open(string path)
	{
		lock (_lock)
		{
			if (_writer is not null)
			{
				return;
			}
			try
			{
				_writer = new StreamWriter(new FileStream(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.ReadWrite),
					new UTF8Encoding(false)) { AutoFlush = true };
				_writer.WriteLine($"OpenHOI3 log, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
				foreach (string line in _early)
				{
					_writer.WriteLine(line);
				}
				_early.Clear();
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_writer = null;
				GD.Print($"The log could not be opened at {path}: {e.Message}");
			}
		}
	}

	public override void _LogMessage(string message, bool error)
	{
		Write((error ? "ERROR: " : "") + message.TrimEnd('\n', '\r'));
	}

	public override void _LogError(string function, string file, int line, string code, string rationale, bool editorNotify,
		int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
	{
		// As Godot's own console writes it: the kind, the message, and where it was raised.
		string kind = errorType == (int)ErrorType.Warning ? "WARNING" : "ERROR";
		string text = rationale.Length > 0 ? rationale : code;
		Write($"{kind}: {text}\n   at: {function} ({file}:{line})");
	}

	private void Write(string text)
	{
		string stamped = $"[{DateTime.Now:HH:mm:ss}] {text}";
		lock (_lock)
		{
			if (_writer is null)
			{
				_early.Add(stamped);
				return;
			}
			try
			{
				_writer.WriteLine(stamped);
			}
			catch (Exception e) when (e is IOException or ObjectDisposedException)
			{
				// A log that can no longer be written is let go; the game goes on.
			}
		}
	}
}
