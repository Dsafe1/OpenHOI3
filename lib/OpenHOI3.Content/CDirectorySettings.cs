using System;
using System.Collections.Generic;
using System.IO;

namespace OpenHOI3.Content;

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

	/// <summary>The extension a texture's name is written with and need not be stored under.</summary>
	private const string SwappedExtension = ".tga";

	/// <summary>The extension looked for in its place.</summary>
	private const string PreferredExtension = ".dds";

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
	/// Uses a given directory as the install root instead of searching for one. Lets a
	/// caller point at an install in an unusual place, and lets the layering and path
	/// resolution be exercised against a fixture directory.
	/// </summary>
	public static CDirectorySettings At(string gameRoot)
	{
		return new CDirectorySettings(Path.GetFullPath(gameRoot));
	}

	/// <summary>
	/// Searches for the install beside the repository, as documented in the readme:
	/// a "Hearts of Iron 3" folder in the parent of the project directory. Walks
	/// further up as a convenience for differently nested checkouts.
	///
	/// The project directory is passed in rather than asked for, because only the
	/// engine knows where <c>res://</c> is and this library does not reference it.
	/// </summary>
	public static CDirectorySettings? Discover(string projectDir)
	{
		foreach (string candidate in SearchedLocations(projectDir))
		{
			if (IsGameRoot(candidate))
			{
				return new CDirectorySettings(Path.GetFullPath(candidate));
			}
		}
		return null;
	}

	/// <summary>
	/// The install an exported build plays: the folder its executable was placed in, and nothing else. A drop-in
	/// engine is put beside the original's own <c>hoi3_tfh.exe</c>, so the folder that holds it is the install; no
	/// other place is searched, so a build put anywhere else says so rather than playing an install it happened to
	/// find. Null where that folder is not an install.
	/// </summary>
	public static CDirectorySettings? BesideExecutable(string executablePath)
	{
		string? folder = Path.GetDirectoryName(Path.GetFullPath(executablePath));
		return folder is not null && IsGameRoot(folder) ? new CDirectorySettings(folder) : null;
	}

	public static string[] SearchedLocations(string projectDir)
	{
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
	/// Resolves a texture reference across the layers.
	///
	/// A name that says <c>.tga</c> is first looked for as <c>.dds</c>, in every layer,
	/// and the <c>.tga</c> is used only when no layer has one: that is the original's
	/// rule, and the files lean on it - the front end asks for <c>frontend_tile.tga</c>
	/// where only a <c>.dds</c> exists, and <c>select_ship_button</c> ships as both, the
	/// two not the same picture. See <c>docs/mechanics/interface.md</c>, section 5.
	///
	/// A name that is in no layer under either is then tried with the other image
	/// extensions, which the original does not do; it reports the file missing.
	/// </summary>
	public string? ResolveTexture(string gameRelative)
	{
		string normalised = Normalise(gameRelative);
		// The original swaps the first ".tga" it finds in the name, written small.
		int swapped = normalised.IndexOf(SwappedExtension, StringComparison.Ordinal);
		if (swapped >= 0)
		{
			string preferred = normalised[..swapped] + PreferredExtension
				+ normalised[(swapped + SwappedExtension.Length)..];
			foreach (string layer in Layers)
			{
				string candidate = InLayer(layer, preferred);
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
		}
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
	/// <summary>A game-relative folder in each layer, highest priority first, whether or not it exists.</summary>
	public IEnumerable<string> LayerFolders(string gameRelativeDirectory)
	{
		string normalised = Normalise(gameRelativeDirectory);
		foreach (string layer in Layers)
		{
			yield return InLayer(layer, normalised);
		}
	}

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

	/// <summary>A file found under a game-relative directory, and where it sits inside it.</summary>
	public readonly record struct LayeredFile(string RelativePath, string AbsolutePath);

	/// <summary>
	/// Files matching a pattern anywhere under a directory, across every layer.
	///
	/// Here a file is identified by its path below the directory rather than by its
	/// name alone, because that is how the history folders layer: the expansion
	/// replaces <c>africa/9785 - Mekele.txt</c> with a file at the same place, and two
	/// folders are free to hold files of the same name. The results are ordered by
	/// that relative path, compared without regard to case, so the order does not
	/// depend on which layer a file came from or on how the disk lists a folder.
	/// </summary>
	public LayeredFile[] ListFilesRecursive(string gameRelativeDirectory, string searchPattern)
	{
		string normalised = Normalise(gameRelativeDirectory);
		var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (string layer in Layers)
		{
			string dir = InLayer(layer, normalised);
			if (!Directory.Exists(dir))
			{
				continue;
			}
			foreach (string file in Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(dir, file).Replace('\\', '/');
				chosen.TryAdd(relative, file);            // the first layer to have it wins
			}
		}

		var relatives = new List<string>(chosen.Keys);
		relatives.Sort(StringComparer.OrdinalIgnoreCase);

		var result = new LayeredFile[relatives.Count];
		for (int i = 0; i < relatives.Count; i++)
		{
			result[i] = new LayeredFile(relatives[i], chosen[relatives[i]]);
		}
		return result;
	}
}
