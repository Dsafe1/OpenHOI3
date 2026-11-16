using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Checksum;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Frontend;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Settings;
using OpenHOI3.Gui;
using OpenHOI3.Loading;
using OpenHOI3.Lua;
using OpenHOI3.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// What the front end is made from - the player's settings, the install's databases and map, its Lua, its interface and
/// the world at the start date - read under the loading screen, a step a line (<c>docs/mechanics/loadingscreen.md</c>).
/// </summary>
public sealed class CFrontEndContent
{
	public CUserSettings Settings { get; private init; } = CUserSettings.Default;
	public CLocalisation Localisation { get; private init; } = null!;
	public CCountries Countries { get; private init; } = null!;
	public CMap Map { get; private init; } = null!;
	public CAiScripts AiScripts { get; private init; } = null!;
	public CGuiContext Gui { get; private init; } = null!;
	public CGameState World { get; private init; } = null!;
	public IReadOnlyList<CBookmark> Bookmarks { get; private init; } = System.Array.Empty<CBookmark>();
	public CMapCache Cache { get; private init; } = null!;
	public IReadOnlyList<CQuadTile> Tiles { get; private init; } = null!;

	/// <summary>The original's file checksum of the install, which the version label's letters come from.</summary>
	public int Checksum { get; private init; }

	/// <summary>Everything read, each step's line on the loading screen as it runs.</summary>
	/// <param name="surfaces">The keeper of the map's surface, whose borders' corners are read here, off the main thread.</param>
	public static async Task<CFrontEndContent> Load(CLoadingScreen screen, CDirectorySettings directories, CMapSurfaceKeeper surfaces)
	{
		var clock = Stopwatch.StartNew();
		CUserSettings settings = CUserSettingsReader.Read(CUserSettingsReader.DefaultPath(directories));
		// What no game changes is read once a run and kept for every front end after the first: each line is still
		// shown, as the steps the pictures are timed by (loadingscreen.md, section 3). What a game takes as its own - the
		// countries, the AI's Lua and the world - is read anew every time.
		CKeptFiles? kept = _kept is { } held && held.Root == directories.GameRoot ? held : null;
		// The localisation before any line, which is written from it: the picture alone until then.
		CLocalisation localisation = kept?.Localisation ?? await screen.Work("", () => CLocalisation.Load(directories));
		screen.Localisation = localisation;
		// The original's order where the work is the same, each line stepping the pictures as the original's does with it.
		CGuiContext gui = kept?.Gui!;
		await screen.OnMain(CLoadingSteps.MapSprites, () => gui ??= CGuiContext.Load(directories, localisation), steps: 1);
		gui.KeyboardFocus = null;
		CCountries countries = await screen.Work(CLoadingSteps.Databases, () => CCountries.Load(directories, localisation));
		// The map's cache with the map logic, as the original checks and builds it there, the checksum reading two of its files.
		(CMap map, CMapCache cache) = await screen.Work(CLoadingSteps.MapLogic, () =>
		{
			if (kept is not null)
			{
				return (kept.Map, kept.Cache);
			}
			CMap loaded = CMap.Load(directories);
			return (loaded, CMapView.OpenCache(directories, loaded));
		});
		CAiScripts aiScripts = await screen.Work(CLoadingSteps.SecondDatabases, () => new CAiScripts(directories));
		(CGameState world, IReadOnlyList<CBookmark> bookmarks) = await screen.Work(CLoadingSteps.History, () =>
		{
			CGameState made = CGameStateLoader.BuildWorld(directories, map, localisation, countries, CDiagnostics.Warn, aiScripts);
			return (made, CBookmarkReader.Read(directories, countries));
		}, steps: 1);
		int checksum = await screen.Work(CLoadingSteps.Checksum, () => WorkOutChecksum(directories), steps: 1);
		IReadOnlyList<CQuadTile> tiles = await screen.Work(CLoadingSteps.Graphics, () =>
		{
			surfaces.PrepareCorners(cache, map);
			return kept?.Tiles ?? CQuadCache.Load(cache, problem => GD.PushWarning($"map: {problem}"));
		});
		_kept = new CKeptFiles(directories.GameRoot, localisation, gui, map, cache, tiles);
		GD.Print($"Front end read in {clock.ElapsedMilliseconds} ms{(kept is null ? "" : ", its unchanging files kept")}.");
		return new CFrontEndContent
		{
			Settings = settings,
			Localisation = localisation,
			Countries = countries,
			Map = map,
			AiScripts = aiScripts,
			Gui = gui,
			World = world,
			Bookmarks = bookmarks,
			Cache = cache,
			Tiles = tiles,
			Checksum = checksum,
		};
	}

	/// <summary>What no game changes, read by the first front end of a run and kept for the install it was read from.</summary>
	private static CKeptFiles? _kept;

	/// <summary>The localisation, the interface's files, the map with its cache and the cache's quads.</summary>
	private sealed record CKeptFiles(string Root, CLocalisation Localisation, CGuiContext Gui, CMap Map, CMapCache Cache,
		IReadOnlyList<CQuadTile> Tiles);

	/// <summary>The install whose checksum was worked out, and the figure: once a run, as the original does it at startup.</summary>
	private static (string Root, int Checksum)? _checksum;

	/// <summary>The original's file checksum of the install, worked out at the first front end and kept for the rest.</summary>
	private static int WorkOutChecksum(CDirectorySettings directories)
	{
		if (_checksum is { } kept && kept.Root == directories.GameRoot)
		{
			return kept.Checksum;
		}
		var clock = Stopwatch.StartNew();
		(int checksum, int files) = CFileChecksum.Compute(CChecksumSearchPath.For(directories));
		GD.Print($"Checksum is {CFileChecksum.VersionName} ({CFileChecksum.Letters(checksum)})    {checksum}, "
			+ $"{files} files in {clock.ElapsedMilliseconds} ms.");
		_checksum = (directories.GameRoot, checksum);
		return checksum;
	}
}
