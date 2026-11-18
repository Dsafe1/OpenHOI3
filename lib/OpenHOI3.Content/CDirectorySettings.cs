using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Content;

/// <summary>
/// Locates the player's Hearts of Iron 3 installation and turns the game-relative,
/// backslash-separated paths found in script files into absolute ones - through the
/// mod played, where one is, laid over the install as the original lays it
/// (<c>docs/mechanics/mods.md</c>).
///
/// The mod is part of the settings a game is made with and not of the process: a
/// session is made with one of these, and another may be made with another mod.
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
	/// interface. A mod's folder goes in front of them.
	/// </summary>
	private static readonly string[] InstallLayers = { "tfh", "" };

	/// <summary>The player's folder of the base game, below the Documents folder.</summary>
	private static readonly string[] DocumentsGameFolder = { "Paradox Interactive", "Hearts of Iron III" };

	public string GameRoot { get; }

	/// <summary>The mods laid over the install, front first - the order a lookup walks them; none for the base game.</summary>
	public IReadOnlyList<CMod> Mods { get; }

	/// <summary>
	/// The base game's player folder, <c>Documents\Paradox Interactive\Hearts of Iron III</c> - or what <c>-userdir=</c>
	/// gave - mod or none: the folder a mod's own is made below.
	/// </summary>
	public string DocumentsFolder { get; }

	/// <summary>
	/// The player's folder of what is played: the base game's <see cref="DocumentsFolder"/>, or below it the selected mods'
	/// <c>user_dir</c>s joined with '_' where any has one. It is the original's write folder - the settings, the saves,
	/// the message settings, the map cache, the logs - so everything that reads or writes one of those asks here.
	/// </summary>
	public string UserFolder { get; }

	/// <summary>Every folder a game file is looked for in, highest priority first, absolute.</summary>
	private readonly string[] _layers;

	/// <summary>
	/// For each folder a selected mod's <c>replace_path</c> names, exactly as written but '\' made '/': the last layer a
	/// listed file's first copy may be in - the frontmost of the mods that name it.
	/// </summary>
	private readonly Dictionary<string, int> _replaceLimit;

	private CDirectorySettings(string gameRoot, IReadOnlyList<CMod>? mods = null, string? documentsFolder = null)
	{
		GameRoot = gameRoot;
		Mods = mods ?? Array.Empty<CMod>();
		DocumentsFolder = documentsFolder ?? DefaultDocumentsFolder();
		// Joined in the order they are mounted, the back first.
		string joined = string.Join(CModRules.UserDirJoiner, Mods.Reverse().Select(m => m.UserDir).Where(d => d is { Length: > 0 }));
		UserFolder = joined.Length > 0 ? Path.Combine(DocumentsFolder, joined.Replace('/', Path.DirectorySeparatorChar)) : DocumentsFolder;
		var layers = new List<string>();
		if (Mods.Count > 0 && CModRules.UserFolderIsLayer)
		{
			layers.Add(UserFolder);
		}
		_replaceLimit = new Dictionary<string, int>(CModRules.Comparer);
		foreach (CMod mod in Mods)
		{
			if (mod.Folder is null)
			{
				continue;
			}
			layers.Add(mod.Folder);
			if (CModRules.BackslashedPathDisablesReplace && mod.PathAsWritten?.Contains('\\') == true)
			{
				continue;
			}
			foreach (string replaced in mod.ReplacePaths)
			{
				_replaceLimit.TryAdd(replaced, layers.Count - 1);          // the frontmost mod naming it
			}
		}
		foreach (string layer in InstallLayers)
		{
			layers.Add(layer.Length == 0 ? gameRoot : Path.Combine(gameRoot, layer));
		}
		_layers = layers.ToArray();
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
	/// The install at a directory with mods laid over it, front first, or none; and the player's folder of the base game
	/// somewhere else than the Documents folder's - <c>-userdir=</c>'s, or a fixture's.
	/// </summary>
	public static CDirectorySettings At(string gameRoot, IReadOnlyList<CMod>? mods, string? documentsFolder = null)
	{
		return new CDirectorySettings(Path.GetFullPath(gameRoot), mods, documentsFolder);
	}

	/// <summary>The same install with mods laid over it, front first - or with none.</summary>
	public CDirectorySettings WithMods(IReadOnlyList<CMod> mods)
	{
		return new CDirectorySettings(GameRoot, mods, DocumentsFolder);
	}

	/// <summary>The same install with another base game's player folder: <c>-userdir=</c>'s.</summary>
	public CDirectorySettings WithDocumentsFolder(string documentsFolder)
	{
		return new CDirectorySettings(GameRoot, Mods, documentsFolder);
	}

	/// <summary>
	/// The same install with the mod a developer's name means laid over it - the file name of its <c>.mod</c>, with or
	/// without the extension (<see cref="CModReader.VirtualPathFor"/>) - or null with the reason reported where no mod is
	/// selected by it.
	/// </summary>
	public CDirectorySettings? WithMod(string name, Action<string>? warn = null)
	{
		IReadOnlyList<CMod> mods = CModReader.Select(GameRoot, DocumentsFolder,
			new[] { CModReader.VirtualPathFor(GameRoot, DocumentsFolder, name) }, warn);
		return mods.Count > 0 ? WithMods(mods) : null;
	}
	/// The base game's player folder on this machine, where the original keeps its settings, the message types the player
	/// chose, the map cache and the saves (<c>docs/mechanics/ingamemenu.md</c>, section 5).
	/// </summary>
	public static string DefaultDocumentsFolder()
	{
		string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		return Path.Combine(documents, Path.Combine(DocumentsGameFolder));
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

	/// <summary>
	/// The install at a folder the launcher names (<c>--install=</c>, <c>docs/launcher.md</c>, section 5), or null where
	/// that folder is not one. Unlike <see cref="At(string)"/> it is checked, as a folder a player chose has to be.
	/// </summary>
	public static CDirectorySettings? Named(string folder)
	{
		return IsInstall(folder) ? new CDirectorySettings(Path.GetFullPath(folder)) : null;
	}

	/// <summary>Whether a folder is an install: it holds the folders every install has beside its executable.</summary>
	public static bool IsInstall(string folder)
	{
		return folder.Length > 0 && IsGameRoot(folder);
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
	private string InLayer(int layer, string normalised)
	{
		return Path.Combine(_layers[layer], normalised);
	}

	/// <summary>
	/// A listed folder as the original compares it with a <c>replace_path</c>: '\' made '/' and nothing else - no slash
	/// trimmed, no case folded (<i>confirmed</i>).
	/// </summary>
	private static string AsListed(string gameRelativeFolder)
	{
		return gameRelativeFolder.Replace('\\', '/');
	}

	/// <summary>
	/// Whether a listed file whose first copy is in a layer is passed over: in a folder a selected mod's
	/// <c>replace_path</c> names, a file is listed only where its first copy is in that mod or in front of it.
	/// </summary>
	private bool Hidden(int layer, string gameRelativeFolder)
	{
		return _replaceLimit.Count > 0 && _replaceLimit.TryGetValue(AsListed(gameRelativeFolder), out int limit) && layer > limit;
	}

	/// <summary>Whether a game-relative folder is one a selected mod's <c>replace_path</c> names, exactly.</summary>
	public bool IsReplaced(string gameRelativeFolder)
	{
		return _replaceLimit.ContainsKey(AsListed(gameRelativeFolder));
	}

	/// <summary>
	/// Absolute path for a game-relative one, preferring the highest priority layer
	/// that actually has the file. Falls back to the base install path so callers can
	/// still report a sensible location when nothing exists. A file opened by its name
	/// is never kept from the install by a <c>replace_path</c>: that rule is a listing's
	/// alone, as in the original.
	/// </summary>
	public string Resolve(string gameRelative)
	{
		string normalised = Normalise(gameRelative);
		for (int layer = 0; layer < _layers.Length; layer++)
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
			for (int layer = 0; layer < _layers.Length; layer++)
			{
				string candidate = InLayer(layer, preferred);
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
		}
		for (int layer = 0; layer < _layers.Length; layer++)
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
	/// A game-relative folder in each layer, highest priority first, whether or not it exists. A caller that lists one
	/// asks <see cref="MayList"/> of each file's first copy, so that a folder a mod replaces lists as it should.
	/// </summary>
	public IEnumerable<string> LayerFolders(string gameRelativeDirectory)
	{
		string normalised = Normalise(gameRelativeDirectory);
		for (int layer = 0; layer < _layers.Length; layer++)
		{
			yield return InLayer(layer, normalised);
		}
	}

	/// <summary>
	/// The player's folder, then the same folder in each layer: where the saves are listed from - the original's whole
	/// search path - the first being where one is written (<c>docs/mechanics/ingamemenu.md</c>, section 5). With a mod the
	/// player's folder is the mod's.
	/// </summary>
	public IEnumerable<string> UserAndLayerFolders(string gameRelativeDirectory)
	{
		yield return Path.Combine(UserFolder, Normalise(gameRelativeDirectory));
		foreach (string folder in LayerFolders(gameRelativeDirectory))
		{
			yield return folder;
		}
	}

	/// <summary>
	/// Whether a file whose first copy is in one of <see cref="LayerFolders"/>'s folders, by its index there, may be listed.
	/// </summary>
	public bool MayList(int layerIndex, string gameRelativeDirectory)
	{
		return !Hidden(layerIndex, gameRelativeDirectory);
	}

	/// <summary>
	/// The original's whole search path, front first (<c>docs/mechanics/mods.md</c>, section 2): with mods, the player's
	/// folder, each mod's folder and the install's layers - the base game's Documents folder not at all; without, the
	/// Documents folder and then the install's layers. And for each folder a mod replaces, the last of them a listed file's
	/// first copy may be in.
	/// </summary>
	public (string[] Mounts, IReadOnlyDictionary<string, int> ReplaceLimits) SearchPath()
	{
		var mounts = new List<string> { UserFolder };
		var limits = new Dictionary<string, int>(CModRules.Comparer);
		foreach (CMod mod in Mods)
		{
			if (mod.Folder is null)
			{
				continue;
			}
			mounts.Add(mod.Folder);
			if (CModRules.BackslashedPathDisablesReplace && mod.PathAsWritten?.Contains('\\') == true)
			{
				continue;
			}
			foreach (string replaced in mod.ReplacePaths)
			{
				limits.TryAdd(replaced, mounts.Count - 1);
			}
		}
		foreach (string layer in InstallLayers)
		{
			mounts.Add(layer.Length == 0 ? GameRoot : Path.Combine(GameRoot, layer));
		}
		return (mounts.ToArray(), limits);
	}

	/// <summary>Every selected mod's <c>replace_path</c> folders, front first, or none.</summary>
	public IReadOnlyList<string> ReplacedFolders => Mods.SelectMany(m => m.ReplacePaths).ToList();

	/// <summary>
	/// Files matching a pattern across every layer.
	///
	/// A name present in more than one layer resolves to the highest priority copy, so
	/// an expansion replacing a script does not leave the base version to be loaded as
	/// well. The results are ordered base layer first, because the registries built
	/// from them resolve conflicts by load order and the expansion has to win. In a
	/// folder a mod replaces, a file is listed only where its first copy is that mod's
	/// or in front of it.
	/// </summary>
	public string[] ListFiles(string gameRelativeDirectory, string searchPattern)
	{
		string normalised = Normalise(gameRelativeDirectory);
		var chosen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var layerOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		for (int layerIndex = 0; layerIndex < _layers.Length; layerIndex++)
		{
			string dir = InLayer(layerIndex, normalised);
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
		if (_replaceLimit.Count > 0)
		{
			foreach (string name in new List<string>(chosen.Keys))
			{
				if (Hidden(layerOf[name], gameRelativeDirectory))
				{
					chosen.Remove(name);
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
	///
	/// Each folder is listed by its own <c>replace_path</c>: the original lists a folder's
	/// subfolders with no replace test and then lists each, so replacing a folder does
	/// not replace the folders inside it - which is why BlackICE names
	/// <c>history\provinces</c> and each of its subfolders.
	/// </summary>
	public LayeredFile[] ListFilesRecursive(string gameRelativeDirectory, string searchPattern)
	{
		string normalised = Normalise(gameRelativeDirectory);
		string listed = AsListed(gameRelativeDirectory);
		var chosen = new Dictionary<string, (string File, int Layer)>(StringComparer.OrdinalIgnoreCase);

		for (int layerIndex = 0; layerIndex < _layers.Length; layerIndex++)
		{
			string dir = InLayer(layerIndex, normalised);
			if (!Directory.Exists(dir))
			{
				continue;
			}
			foreach (string file in Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(dir, file).Replace('\\', '/');
				chosen.TryAdd(relative, (file, layerIndex));            // the first layer to have it wins
			}
		}

		var relatives = new List<string>();
		foreach ((string relative, (string _, int layer)) in chosen)
		{
			int slash = relative.LastIndexOf('/');
			if (!Hidden(layer, slash < 0 ? listed : listed + "/" + relative[..slash]))
			{
				relatives.Add(relative);
			}
		}
		relatives.Sort(StringComparer.OrdinalIgnoreCase);

		var result = new LayeredFile[relatives.Count];
		for (int i = 0; i < relatives.Count; i++)
		{
			result[i] = new LayeredFile(relatives[i], chosen[relatives[i]].File);
		}
		return result;
	}

	/// <summary>A file's path relative to the install, written with '/', for a report: a mod's file begins with its folder.</summary>
	public string Relative(string absolutePath)
	{
		return Path.GetRelativePath(GameRoot, absolutePath).Replace('\\', '/');
	}

	/// <summary>The layers' folders, highest priority first, for a report.</summary>
	public IReadOnlyList<string> Layers => _layers;
}
