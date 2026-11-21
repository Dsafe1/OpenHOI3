using System;
using System.IO;
using System.Text;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// The <c>engine.txt</c> beside an engine, which says what it is without its being started (<c>docs/launcher.md</c>,
/// section 2): <c>version</c>, <c>executable</c> - relative to the folder - and <c>contract</c>. Written by
/// <c>tools/build_engine.ps1</c>; ours, so UTF-8.
/// </summary>
public static class CEngineFile
{
	/// <summary>The file's name in an engine's folder.</summary>
	public const string FileName = "engine.txt";

	/// <summary>The folder a mod keeps its engine in, below its own; the launcher keeps its own the same way.</summary>
	public const string FolderName = "engine";

	/// <summary>The folder a mod keeps a launcher in, below its own, where it ships one (<c>docs/launcher.md</c>, section 2).</summary>
	public const string LauncherFolderName = "launcher";

	/// <summary>
	/// The folder of a launcher's own engine: <c>engine</c> in the launcher's folder, or, where that holds no
	/// <c>engine.txt</c>, <c>engine</c> beside the launcher's folder - which is where a launcher a mod ships in its
	/// <c>launcher</c> folder finds the engine the mod ships in its <c>engine</c> folder, so that the one copy plays the
	/// base game too. With neither, the first, for what is said of it.
	/// </summary>
	public static string OwnFolder(string launcherFolder)
	{
		string inside = Path.Combine(launcherFolder, FolderName);
		if (File.Exists(Path.Combine(inside, FileName)))
		{
			return inside;
		}
		string trimmed = launcherFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		if (Path.GetDirectoryName(trimmed) is { Length: > 0 } parent)
		{
			string beside = Path.Combine(parent, FolderName);
			if (File.Exists(Path.Combine(beside, FileName)))
			{
				return beside;
			}
		}
		return inside;
	}

	/// <summary>
	/// The engine in a folder, or null with the reason where there is none: no <c>engine.txt</c>, no version, no
	/// executable or one that is not there, or a contract that is not a whole number.
	/// </summary>
	public static CEngine? Read(string folder, Action<string>? warn = null)
	{
		string file = Path.Combine(folder, FileName);
		if (!File.Exists(file))
		{
			warn?.Invoke($"There is no engine in {folder}: no {FileName}");
			return null;
		}
		ClausewitzNode root = ClausewitzParser.Parse(File.ReadAllText(file, Encoding.UTF8));
		string? version = null;
		string? executable = null;
		int? contract = null;
		foreach (ClausewitzNode entry in root.Children)
		{
			switch (entry.Key)
			{
				case "version":
					version = entry.Value;
					break;
				case "executable":
					executable = entry.Value;
					break;
				case "contract":
					contract = int.TryParse(entry.Value, out int number) ? number : null;
					break;
				default:
					warn?.Invoke($"{file}: '{entry.Key}' is not a key of {FileName}; ignored");
					break;
			}
		}
		if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(executable) || contract is null)
		{
			warn?.Invoke($"{file} does not say its version, its executable and its contract");
			return null;
		}
		string program = Path.GetFullPath(Path.Combine(folder, executable));
		if (!File.Exists(program))
		{
			warn?.Invoke($"{file} names {executable}, which is not there");
			return null;
		}
		return new CEngine(Path.GetFullPath(folder), version, program, contract.Value);
	}

	/// <summary>The file's text for an engine: what <c>tools/build_engine.ps1</c> writes, and what a test writes.</summary>
	public static string Text(string version, string executable, int contract)
	{
		return "# The engine in this folder, as the launcher reads it (docs/launcher.md).\n"
			+ $"version = \"{version}\"\nexecutable = \"{executable}\"\ncontract = {contract}\n";
	}

	/// <summary>
	/// The version of the engine a program is, by the <c>engine.txt</c> beside it: what the play checksum hashes and an
	/// enhanced save records. Empty where there is none, as for an engine run from its sources.
	/// </summary>
	public static string VersionBeside(string executablePath)
	{
		try
		{
			return Path.GetDirectoryName(executablePath) is { Length: > 0 } folder && Read(folder) is { } engine ? engine.Version : "";
		}
		catch (Exception failed) when (failed is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return "";
		}
	}
}
