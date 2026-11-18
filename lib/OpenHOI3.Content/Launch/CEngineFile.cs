using System;
using System.IO;
using System.Text;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Launch;

/// <summary>
/// The <c>engine.txt</c> beside an engine, which says what it is without its being started (<c>docs/launcher.md</c>,
/// section 2): <c>version</c>, <c>executable</c> - relative to the folder - and <c>contract</c>. Written by
/// <c>tools/export.ps1</c>; ours, so UTF-8.
/// </summary>
public static class CEngineFile
{
	/// <summary>The file's name in an engine's folder.</summary>
	public const string FileName = "engine.txt";

	/// <summary>The folder a mod keeps its engine in, below its own; the launcher keeps its own the same way.</summary>
	public const string FolderName = "engine";

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

	/// <summary>The file's text for an engine: what <c>tools/export.ps1</c> writes, and what a test writes.</summary>
	public static string Text(string version, string executable, int contract)
	{
		return "# The engine in this folder, as the launcher reads it (docs/launcher.md).\n"
			+ $"version = \"{version}\"\nexecutable = \"{executable}\"\ncontract = {contract}\n";
	}
}
