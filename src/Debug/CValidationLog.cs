using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// Every fault found in the install's or the mods' own files (<c>OpenHOI3.Content.CDiagnostics.Problem</c>), one to a line,
/// written to <c>openhoi3_validation.log</c> beside the run's log - a list for whoever writes a mod to work through
/// (<c>docs/mechanics/logs.md</c>, section 4). It is made afresh at each start. As with the run's log, the folder is known
/// only once the install and the mod are, so what is reported before that is kept and written first, and each line is
/// flushed as it is written. The run's log is told once where the list is. Loaders may report from any thread, so it locks.
/// </summary>
public sealed class CValidationLog
{
	private readonly object _lock = new();
	private readonly List<string> _early = new();
	private StreamWriter? _writer;
	private string _path = "";
	private bool _told;

	/// <summary>The one validation log of the process.</summary>
	public static CValidationLog Instance { get; } = new();

	/// <summary>How many faults have been reported this run.</summary>
	public int Count { get; private set; }

	/// <summary>Starts writing to a file: what was reported before it first. A file that cannot be opened leaves the log unwritten.</summary>
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
				_writer.WriteLine($"OpenHOI3 validation, {DateTime.Now:yyyy-MM-dd HH:mm:ss}: faults in the install's and the mods' files, " +
					"each where it is - file:line: what");
				foreach (string line in _early)
				{
					_writer.WriteLine(line);
				}
				_early.Clear();
				_path = path;
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				_writer = null;
				GD.Print($"The validation log could not be opened at {path}: {e.Message}");
			}
		}
		Tell();
	}

	/// <summary>The faults written, each once: a file several readers parse - an interface file - says its faults once.</summary>
	private readonly HashSet<string> _written = new(StringComparer.Ordinal);

	/// <summary>One fault, as <c>CDiagnostics</c> hands it over; one already written is not written again.</summary>
	public void Write(string problem)
	{
		lock (_lock)
		{
			if (!_written.Add(problem))
			{
				return;
			}
			Count++;
			if (_writer is null)
			{
				_early.Add(problem);
				return;
			}
			try
			{
				_writer.WriteLine(problem);
			}
			catch (Exception e) when (e is IOException or ObjectDisposedException)
			{
				// A log that can no longer be written is let go; the game goes on.
			}
		}
		Tell();
	}

	/// <summary>Once there is a fault and a file, the run's log says where they are listed.</summary>
	private void Tell()
	{
		string path;
		lock (_lock)
		{
			if (_told || Count == 0 || _path.Length == 0)
			{
				return;
			}
			_told = true;
			path = _path;
		}
		GD.Print($"Faults found in the install's or the mods' files are listed in {path}");
	}
}
