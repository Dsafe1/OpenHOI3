using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Checksum;
using OpenHOI3.Content.Settings;

/// <summary>
/// Checks the file checksum against the figure the original logs:
///
///   OpenHOI3.SaveCheck checksum &lt;install&gt; [mod name]
///
/// Works out the checksum of the install - with the mod of <c>tfh/mod/&lt;name&gt;.mod</c> laid over it where one is named,
/// its user folder and <c>replace_path</c> folders with it - and sets it beside the line
/// <c>Checksum is HoI3 v4.02 (GSKH)    1499173604</c> of the player's <c>logs/setup.log</c>, the mod's user folder's where
/// a mod is named. The log is the original's last start, so it agrees only while no summed file has changed since. See
/// <c>docs/mechanics/checksum.md</c>, section 6.
/// </summary>
public static class CChecksumCheck
{
	private const string LogLine = "Checksum is ";

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("checksum: OpenHOI3.SaveCheck checksum <install> [mod name]");
			return 2;
		}
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		string userFolder = CUserSettingsReader.UserFolder();
		string logFolder = userFolder;
		CChecksumSearchPath search;
		if (args.Length > 2)
		{
			string modFile = Path.Combine(directories.GameRoot, "tfh", "mod", args[2] + ".mod");
			if (!File.Exists(modFile))
			{
				Console.Error.WriteLine($"checksum: there is no '{modFile}'");
				return 2;
			}
			(string? modPath, string? modUser, List<string> replaced) = ReadMod(directories.GameRoot, userFolder, modFile);
			if (modPath is null || modUser is null)
			{
				Console.Error.WriteLine($"checksum: '{modFile}' gives no path or no user_dir");
				return 2;
			}
			var mounts = new List<string> { modUser, modPath };
			mounts.AddRange(directories.LayerFolders("").Select(Path.GetFullPath));
			mounts.Add(userFolder);
			search = new CChecksumSearchPath(mounts, replaced, replaceLimit: 1);
			logFolder = modUser;
		}
		else
		{
			search = CChecksumSearchPath.ForInstall(directories, userFolder);
		}

		var clock = Stopwatch.StartNew();
		(int checksum, int files) = CFileChecksum.Compute(search);
		string ours = $"{CFileChecksum.VersionName} ({CFileChecksum.Letters(checksum)})    {checksum}";
		Console.WriteLine($"ours:     {ours}, {files} files in {clock.ElapsedMilliseconds} ms");

		string log = Path.Combine(logFolder, "logs", "setup.log");
		string? theirs = null;
		if (File.Exists(log))
		{
			// Shared: a running original holds its log open.
			using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream, Encoding.Latin1);
			while (reader.ReadLine() is { } line)
			{
				int at = line.IndexOf(LogLine, StringComparison.Ordinal);
				if (at >= 0)
				{
					theirs = line[(at + LogLine.Length)..].Trim();
				}
			}
		}
		if (theirs is null)
		{
			Console.WriteLine($"original: no '{LogLine.Trim()}' line in '{log}'");
			return 1;
		}
		Console.WriteLine($"original: {theirs}, logged {File.GetLastWriteTime(log):yyyy-MM-dd HH:mm}");
		bool same = theirs == ours.Trim();
		Console.WriteLine(same ? "the same" : "they differ - a summed file may have changed since the original last started");
		return same ? 0 : 1;
	}

	/// <summary>A .mod file's folder, its user folder and the folders it replaces, '/' for '\'.</summary>
	private static (string? Path, string? User, List<string> Replaced) ReadMod(string gameRoot, string userFolder, string modFile)
	{
		string? path = null;
		string? user = null;
		var replaced = new List<string>();
		foreach (string line in File.ReadLines(modFile, Encoding.Latin1))
		{
			int equals = line.IndexOf('=');
			if (equals < 0)
			{
				continue;
			}
			string key = line[..equals].Trim();
			string value = line[(equals + 1)..].Trim().Trim('"');
			switch (key)
			{
				case "path":
					path = System.IO.Path.Combine(gameRoot, value.Replace('/', System.IO.Path.DirectorySeparatorChar));
					break;
				case "user_dir":
					user = System.IO.Path.Combine(userFolder, value);
					break;
				case "replace_path":
					replaced.Add(value.Replace('\\', '/'));
					break;
			}
		}
		return (path, user, replaced);
	}
}
