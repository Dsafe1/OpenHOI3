using System;
using System.IO;

namespace OpenHOI3.Content.Logs;

/// <summary>
/// Where the game's own log goes and how the runs before it are kept (<c>docs/mechanics/logs.md</c>). The folder is the
/// original's: <c>logs</c> in the player's folder - <c>Documents\Paradox Interactive\Hearts of Iron III</c>, or a mod's user
/// folder below it with a mod (<c>docs/mechanics/mods.md</c>, section 2). The files are ours - <c>openhoi3.log</c> this run,
/// <c>openhoi3_1.log</c> to <c>openhoi3_4.log</c> the four before it, newest first - so the original's own logs beside
/// them (<c>game.log</c>, <c>error.log</c>, <c>setup.log</c> and the rest) are never written: decided on
/// 2026-11-18.
/// </summary>
public static class CLogFiles
{
	/// <summary>The name this run's log is written under, without its extension.</summary>
	public const string Name = "openhoi3";

	/// <summary>How many runs before this one are kept.</summary>
	public const int Kept = 4;

	/// <summary>The logs folder of the player's folder of what is played.</summary>
	public static string Folder(CDirectorySettings directories) => Path.Combine(directories.UserFolder, "logs");

	/// <summary>The file of a run: 0 this one, 1 the one before it, and so on.</summary>
	public static string Of(string folder, int run) =>
		Path.Combine(folder, run == 0 ? Name + ".log" : $"{Name}_{run}.log");

	/// <summary>
	/// Makes the folder where it is missing and moves each kept run one place back - the oldest dropped - so that this
	/// run's file is free, and answers its path. A file that cannot be moved is left where it is; this run's then writes
	/// over it.
	/// </summary>
	public static string Start(string folder)
	{
		Directory.CreateDirectory(folder);
		for (int run = Kept; run >= 1; run--)
		{
			string older = Of(folder, run);
			string newer = Of(folder, run - 1);
			try
			{
				if (File.Exists(newer))
				{
					File.Move(newer, older, overwrite: true);
				}
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException)
			{
				// Kept as it is: another program may hold it open.
			}
		}
		return Of(folder, 0);
	}
}
