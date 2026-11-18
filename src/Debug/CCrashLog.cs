using System;
using System.IO;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// What an exception nothing caught leaves behind: a file <c>crash-&lt;time&gt;.log</c> beside the game's log - in the player's
/// folder's <c>logs</c> once the install is known, Godot's own logs folder before it (<c>docs/mechanics/logs.md</c>) - the
/// exception with its stack and inner exceptions, written straight to the disk as the process goes down. An access
/// violation inside a call into the engine ends the process through .NET and never reaches Godot's log, which is where the
/// trace of a crash is wanted.
/// </summary>
public static class CCrashLog
{
	private static string? _folder;

	/// <summary>Where a crash's file is written: the player's folder's logs, set as soon as the install is known.</summary>
	public static string? Folder
	{
		get => _folder;
		set => _folder = value ?? _folder;
	}

	/// <summary>Watches for an exception nothing catches, from the start; once is enough.</summary>
	public static void Install()
	{
		if (_folder is not null)
		{
			return;
		}
		// The folder is worked out now, on the main thread, while the engine can still be asked.
		_folder = ProjectSettings.GlobalizePath("user://logs");
		AppDomain.CurrentDomain.UnhandledException += (_, arguments) => Write(arguments.ExceptionObject);
	}

	private static void Write(object exception)
	{
		try
		{
			Directory.CreateDirectory(_folder!);
			string path = Path.Combine(_folder!, $"crash-{DateTime.Now:yyyy-MM-ddTHH.mm.ss}.log");
			File.WriteAllText(path, $"OpenHOI3 crashed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{exception}\n");
		}
		catch (Exception)
		{
			// The process is going down; nothing more can be done about a log that cannot be written.
		}
	}
}
