using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Mods;

/// <summary>
/// Finds and reads <c>.mod</c> files - the launcher's descriptions of mods - and selects the ones a command line names,
/// in the order the original mounts them (<c>docs/mechanics/mods.md</c>, section 2;
/// <c>reversing/findings/FINDINGS-modlayer.md</c>). Every key the original does not read is reported, where it drops it
/// silently.
/// </summary>
public static class CModReader
{
	/// <summary>The keys the original reads a <c>.mod</c> file for.</summary>
	private static readonly string[] Keys =
	{
		"name", "path", "archive", "user_dir", "replace_path", "dependencies", "checksum", "affects_checksum", "steam_id",
		"pdx_id", "gamersgate_id",
	};

	/// <summary>
	/// The one key of ours a <c>.mod</c> file may hold, which the original passes over: the version of the engine the mod
	/// is made for (<c>docs/launcher.md</c>, section 3).
	/// </summary>
	public const string EngineKey = "engine";

	/// <summary>The words of the report of a key no <c>.mod</c> file has, by which the launcher leaves it to the log.</summary>
	public const string UnknownKeyNote = "is not a key of a .mod file";

	/// <summary>
	/// Every descriptor the original lists: <c>mod/*.mod</c> over its first search path - the player's folder, then
	/// <c>tfh/</c>, then the install - each name once, the first folder's copy; as virtual path and file.
	/// </summary>
	public static List<(string VirtualPath, string File)> Descriptors(string gameRoot, string documentsFolder)
	{
		var found = new List<(string, string)>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string root in new[] { documentsFolder, Path.Combine(gameRoot, "tfh"), gameRoot })
		{
			string folder = Path.Combine(root, CModRules.DescriptorFolder);
			if (!Directory.Exists(folder))
			{
				continue;
			}
			foreach (string file in Directory.GetFiles(folder, "*" + CModRules.Extension).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
			{
				string name = Path.GetFileName(file);
				// The pattern also takes ".mod" at the end of a longer extension on Windows; the original lists by the tail.
				if (name.EndsWith(CModRules.Extension, StringComparison.Ordinal) && seen.Add(name))
				{
					found.Add((CModRules.DescriptorFolder + "/" + name, file));
				}
			}
		}
		return found;
	}

	/// <summary>
	/// The virtual path a developer's name for a mod means: the descriptor whose file is the name, with or without its
	/// extension, case aside; or the name as a virtual path where it already is one; or <c>mod/&lt;name&gt;.mod</c>.
	/// </summary>
	public static string VirtualPathFor(string gameRoot, string documentsFolder, string name)
	{
		string trimmed = name.Trim().Trim('"').Replace('\\', '/');
		if (trimmed.StartsWith(CModRules.DescriptorFolder + "/", StringComparison.Ordinal))
		{
			return trimmed;
		}
		string file = trimmed.EndsWith(CModRules.Extension, StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + CModRules.Extension;
		foreach ((string virtualPath, _) in Descriptors(gameRoot, documentsFolder))
		{
			if (string.Equals(virtualPath[(CModRules.DescriptorFolder.Length + 1)..], file, StringComparison.OrdinalIgnoreCase))
			{
				return virtualPath;
			}
		}
		return CModRules.DescriptorFolder + "/" + file;
	}

	/// <summary>
	/// The mods the values name, front first - the order a lookup walks them. A value is matched byte for byte against a
	/// descriptor's virtual path; one that matches none, or a descriptor that is not valid, selects nothing, and the game
	/// is played without it as the original plays it - reported here, where the original says nothing. Two of one name:
	/// the first kept. Then the dependencies' order: each starts at -1000, a dependency is raised above its dependent
	/// until nothing changes, and they are sorted by order and then by name, the lowest order and the smallest name in
	/// front.
	/// </summary>
	public static IReadOnlyList<CMod> Select(string gameRoot, string documentsFolder, IEnumerable<string> values,
		Action<string>? warn = null)
	{
		// A switch naming no mod is about this run, not the mod's files: it goes to the run's log where nobody is told.
		Action<string> run = warn ?? CDiagnostics.Warn;
		warn ??= CDiagnostics.Problem;
		List<(string VirtualPath, string File)> descriptors = Descriptors(gameRoot, documentsFolder);
		var selected = new List<CMod>();
		foreach (string value in values)
		{
			(string VirtualPath, string File) match = descriptors.FirstOrDefault(d => CModRules.Comparer.Equals(d.VirtualPath, value));
			if (match.File is null)
			{
				run($"-mod={value} names no mod: no '{CModRules.DescriptorFolder}/*{CModRules.Extension}' is '{value}', case and all"
					+ (descriptors.Count > 0 ? $"; there are {string.Join(", ", descriptors.Select(d => $"'{d.VirtualPath}'"))}" : "")
					+ " - played without it, as the original plays it");
				continue;
			}
			if (selected.Any(m => m.VirtualPath == match.VirtualPath))
			{
				continue;
			}
			if (Read(gameRoot, match.File, match.VirtualPath, warn) is not { } mod)
			{
				continue;
			}
			if (selected.Any(m => m.Name == mod.Name))
			{
				// The name's line is not kept with the mod, so the descriptor as a whole is named.
				warn(CDiagnostics.Located(match.VirtualPath, 0,
					$"the mod is named '{mod.Name}' as an earlier mod is; the first is kept, as the original keeps it"));
				continue;
			}
			selected.Add(mod);
		}
		return Order(selected);
	}

	/// <summary>The selected mods in the order a lookup walks them, front first (see <see cref="Select"/>).</summary>
	public static IReadOnlyList<CMod> Order(IReadOnlyList<CMod> mods)
	{
		var order = mods.ToDictionary(m => m.Name, _ => CModRules.StartOrder, StringComparer.Ordinal);
		// A ring of dependencies would raise for ever; the original's loop has no bound (not read), this one stops.
		for (int pass = 0, changed = 1; changed > 0 && pass < mods.Count * mods.Count + 1; pass++)
		{
			changed = 0;
			foreach (CMod mod in mods)
			{
				foreach (string dependency in mod.Dependencies)
				{
					if (order.TryGetValue(dependency, out int theirs) && theirs <= order[mod.Name])
					{
						order[dependency] = order[mod.Name] + 1;
						changed++;
					}
				}
			}
		}
		// Mounted highest order first, the greater name first among equals, each in front of the last: so the front is the
		// lowest order and the smallest name.
		return mods.OrderBy(m => order[m.Name]).ThenBy(m => m.Name, StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// A <c>.mod</c> file read, its folder made absolute against the install - the original's working folder; null, as
	/// the original refuses it with <c>Incorrect MOD descriptor</c>, where it has no name, or neither a path nor an
	/// archive.
	/// </summary>
	public static CMod? Read(string gameRoot, string file, string virtualPath, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		ClausewitzNode root = ClausewitzParser.ParseFile(file);
		string name = "";
		string path = "";
		string archive = "";
		string? userDir = null;
		string? engine = null;
		var replaced = new List<string>();
		var dependencies = new List<string>();
		// The lines of the path and archive that stand, the last given, for what is said of them.
		int pathLine = 0;
		int archiveLine = 0;
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is null)
			{
				warn(CDiagnostics.Located(virtualPath, entry.Line,
					$"{(entry.IsBlock ? "a block" : $"'{entry.Value}'")} with no key, which a .mod file does not hold; ignored"));
				continue;
			}
			if (entry.Key == "dependencies" && entry.IsBlock)
			{
				// A list of names: bare values.
				dependencies.AddRange(entry.Children.Where(c => c.Key is null && c.Value is { Length: > 0 }).Select(c => c.Value!));
				continue;
			}
			if (entry.Value is null)
			{
				warn(CDiagnostics.Located(virtualPath, entry.Line, $"'{entry.Key}' is a block, which it is not in a .mod file; ignored"));
				continue;
			}
			// A key given twice keeps its last value, as the original keeps it; replace_path adds one each time.
			switch (entry.Key)
			{
				case "name":
					name = entry.Value;
					break;
				case "path":
					path = entry.Value;
					pathLine = entry.Line;
					break;
				case "archive":
					archive = entry.Value;
					archiveLine = entry.Line;
					break;
				case "user_dir":
					userDir = entry.Value;
					break;
				case "replace_path":
					replaced.Add(entry.Value.Replace('\\', '/'));
					break;
				case EngineKey:
					// Ours, which the original passes over: the engine the mod is made for (docs/launcher.md, section 3).
					engine = entry.Value.Length > 0 ? entry.Value : null;
					break;
				case "checksum":
				case "affects_checksum":
				case "steam_id":
				case "pdx_id":
				case "gamersgate_id":
					break;
				default:
					warn(CDiagnostics.Located(virtualPath, entry.Line,
						$"'{entry.Key}' {UnknownKeyNote} ({string.Join(", ", Keys)}); the original drops it"));
					break;
			}
		}
		if (name.Length == 0 || (path.Length == 0 && archive.Length == 0))
		{
			warn($"Incorrect MOD descriptor: \"{Path.GetFileName(file)}\" - it has no name, or neither a path nor an archive; "
				+ "the original refuses it and plays without it");
			return null;
		}
		string? folder = null;
		if (path.Length > 0)
		{
			folder = Path.GetFullPath(Path.Combine(gameRoot, path.Replace('/', Path.DirectorySeparatorChar)));
			if (!Directory.Exists(folder))
			{
				warn(CDiagnostics.Located(virtualPath, pathLine,
					$"its path '{path}' is no folder of the install, so the mod lays nothing over it"));
			}
		}
		if (archive.Length > 0)
		{
			warn(CDiagnostics.Located(virtualPath, archiveLine, $"archive = \"{archive}\" is not read here; "
				+ (folder is null ? "the mod lays nothing over the install" : "its path is laid over the install instead, where the original prefers the archive")));
		}
		if (path.Contains('\\') && CModRules.BackslashedPathDisablesReplace && replaced.Count > 0)
		{
			warn(CDiagnostics.Located(virtualPath, pathLine,
				$"its path is written with a backslash, so its {replaced.Count} replace_path lines do nothing, as in the original"));
		}
		return new CMod(name, Path.GetFullPath(file), virtualPath, folder, path.Length > 0 ? path : null, archive.Length > 0 ? archive : null,
			userDir, replaced, dependencies, engine);
	}
}
