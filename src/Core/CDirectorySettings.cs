using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace OpenHOI3.Core;

/// <summary>
/// Locates the player's Hearts of Iron 3 installation and turns the game-relative,
/// backslash-separated paths found in script files into absolute ones.
/// </summary>
public sealed class CDirectorySettings
{
	/// <summary>Marks a candidate directory as an actual HoI3 install.</summary>
	private static readonly string[] RequiredEntries = { "interface", "gfx", "localisation" };

	/// <summary>Tried in order when a referenced file does not exist verbatim.</summary>
	private static readonly string[] TextureExtensions = { ".dds", ".tga", ".png" };

	/// <summary>
	/// Expansion directories layered over the base install, highest priority first.
	///
	/// Each expansion mirrors the whole tree and replaces only the files it ships, so a
	/// lookup walks the layers and takes the first hit. The install root holds For the
	/// Motherland; Their Finest Hour lives in <c>tfh/</c> and is what this project
	/// targets, so reading the root alone silently produces the previous expansion's
	/// interface.
	/// </summary>
	private static readonly string[] Layers = { "tfh", "" };

	public string GameRoot { get; }

	private CDirectorySettings(string gameRoot)
	{
		GameRoot = gameRoot;
	}

	/// <summary>
	/// Searches for the install beside the repository, as documented in the readme:
	/// a "Hearts of Iron 3" folder in the parent of the project directory. Walks
	/// further up as a convenience for differently nested checkouts.
	/// </summary>
	public static CDirectorySettings? Discover()
	{
		string projectDir = ProjectSettings.GlobalizePath("res://");
		var candidates = new List<string>();

		DirectoryInfo? dir = new DirectoryInfo(projectDir);
		for (int depth = 0; depth < 4 && dir is not null; depth++)
		{
			candidates.Add(Path.Combine(dir.FullName, "Hearts of Iron 3"));
			dir = dir.Parent;
		}

		foreach (string candidate in candidates)
		{
			if (IsGameRoot(candidate))
			{
				return new CDirectorySettings(Path.GetFullPath(candidate));
			}
		}
		return null;
	}

	public static string[] SearchedLocations()
	{
		string projectDir = ProjectSettings.GlobalizePath("res://");
		var result = new List<string>();
		DirectoryInfo? dir = new DirectoryInfo(projectDir);
		for (int depth = 0; depth < 4 && dir is not null; depth++)
		{
			result.Add(Path.Combine(dir.FullName, "Hearts of Iron 3"));
			dir = dir.Parent;
		}
		return result.ToArray();
	}

	private static bool IsGameRoot(string path)
	{
		if (!Directory.Exists(path))
		{
			return false;
		}
		foreach (string entry in RequiredEntries)
		{
			if (!Directory.Exists(Path.Combine(path, entry)))
			{
				return false;
			}
		}
		return true;
	}

	private static string Normalise(string gameRelative)
	{
		return gameRelative.Replace('\\', '/').TrimStart('/')
			.Replace('/', Path.DirectorySeparatorChar);
	}

	/// <summary>Absolute path in a specific layer, without checking existence.</summary>
	private string InLayer(string layer, string normalised)
	{
		return layer.Length == 0
			? Path.Combine(GameRoot, normalised)
			: Path.Combine(GameRoot, layer, normalised);
	}

	/// <summary>
	/// Absolute path for a game-relative one, preferring the highest priority layer
	/// that actually has the file. Falls back to the base install path so callers can
	/// still report a sensible location when nothing exists.
	/// </summary>
	public string Resolve(string gameRelative)
	{
		string normalised = Normalise(gameRelative);
		foreach (string layer in Layers)
		{
			string candidate = InLayer(layer, normalised);
			if (File.Exists(candidate) || Directory.Exists(candidate))
			{
				return candidate;
			}
		}
		return Path.Combine(GameRoot, normalised);
	}

	/// <summary>
	/// Resolves a texture reference across the layers, retrying with the other image
	/// extensions. Script files routinely name a file that shipped in a different
	/// format - the front end asks for <c>frontend_tile.tga</c> but only <c>.dds</c>
	/// exists.
	/// </summary>
	public string? ResolveTexture(string gameRelative)
	{
		string normalised = Normalise(gameRelative);
		foreach (string layer in Layers)
		{
			string direct = InLayer(layer, normalised);
			if (File.Exists(direct))
			{
				return direct;
			}
			string withoutExtension = Path.ChangeExtension(direct, null);
			foreach (string extension in TextureExtensions)
			{
				string candidate = withoutExtension + extension;
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Files matching a pattern across every layer.
	///
	/// A name present in more than one layer resolves to the highest priority copy, so
	/// an expansion replacing a script does not leave the base version to be loaded as
	/// well. The results are ordered base layer first, because the registries built
	/// from them resolve conflicts by load order and the expansion has to win.
	/// </summary>
	public string[] ListFiles(string gameRelativeDirectory, string searchPattern)
	{
		string normalised = Normalise(gameRelativeDirectory);
		var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var layerOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		for (int layerIndex = 0; layerIndex < Layers.Length; layerIndex++)
		{
			string dir = InLayer(Layers[layerIndex], normalised);
			if (!Directory.Exists(dir))
			{
				continue;
			}
			foreach (string file in Directory.GetFiles(dir, searchPattern))
			{
				string name = Path.GetFileName(file);
				if (chosen.TryAdd(name, file))
				{
					layerOf[name] = layerIndex;
				}
			}
		}

		var names = new List<string>(chosen.Keys);
		names.Sort((a, b) =>
		{
			// Higher layer index is lower priority, so it loads first.
			int byLayer = layerOf[b].CompareTo(layerOf[a]);
			return byLayer != 0 ? byLayer : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
		});

		var result = new string[names.Count];
		for (int i = 0; i < names.Count; i++)
		{
			result[i] = chosen[names[i]];
		}
		return result;
	}
}
