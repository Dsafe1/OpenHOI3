using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Rules;

namespace OpenHOI3.Content.Rules;

/// <summary>
/// Reads <c>common/openhoi_enhanced.txt</c>, the file of ours by which a mod's data turns the enhanced mode on
/// (<c>docs/enhanced.md</c>, <i>The shape</i>). The file being there, in any layer, makes a game an enhanced one; with
/// none a game is the faithful one. The original never parses a file under <c>common/</c> it does not ask for by name,
/// so a mod made for both engines may hold it. What it may say:
/// <code>
/// without = { minister_tech_decay }
/// </code>
/// the fixes the mod does not want, by their names (<see cref="CFixes"/>). Anything else is reported.
/// </summary>
public static class CRulesReader
{
	/// <summary>The file, relative to a layer.</summary>
	public const string FileName = "common/openhoi_enhanced.txt";

	/// <summary>The key of the fixes a mod does not want.</summary>
	public const string WithoutKey = "without";

	/// <summary>Whether a layer has the file: whether games made from these folders are enhanced ones.</summary>
	public static bool IsEnhanced(CDirectorySettings directories)
	{
		return File.Exists(directories.Resolve(FileName));
	}

	/// <summary>The rules the folders give a game: the faithful ones where no layer has the file.</summary>
	public static CRules Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		string path = directories.Resolve(FileName);
		return File.Exists(path) ? Parse(ClausewitzParser.ParseFile(path), path, warn ?? CDiagnostics.Warn) : new CRules();
	}

	/// <summary>An enhanced game's rules by the file's text.</summary>
	public static CRules Parse(string text, string source, Action<string> warn)
	{
		return Parse(ClausewitzParser.Parse(text, source), source, warn);
	}

	private static CRules Parse(ClausewitzNode root, string source, Action<string> warn)
	{
		var without = new List<EFix>();
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key != WithoutKey || !entry.IsBlock)
			{
				warn(CDiagnostics.Located(source, entry.Line, $"'{entry.Key ?? entry.Value}' is not a key of {FileName}; ignored"));
				continue;
			}
			foreach (ClausewitzNode name in entry.Children)
			{
				if (name.Key is null && name.Value is { } value && CFixes.Named(value) is { } fix)
				{
					without.Add(fix);
				}
				else
				{
					warn(CDiagnostics.Located(source, name.Line, $"'{name.Key ?? name.Value}' is not a fix of the enhanced mode; ignored"));
				}
			}
		}
		return new CRules(without);
	}
}
