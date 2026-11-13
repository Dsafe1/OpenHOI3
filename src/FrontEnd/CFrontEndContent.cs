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
	public CBookmark? Start { get; private init; }
	public CMapCache Cache { get; private init; } = null!;
	public IReadOnlyList<CQuadTile> Tiles { get; private init; } = null!;

	/// <summary>The original's file checksum of the install, which the version label's letters come from.</summary>
	public int Checksum { get; private init; }

	/// <summary>Everything read, each step's line on the loading screen as it runs.</summary>
	public static async Task<CFrontEndContent> Load(CLoadingScreen screen, CDirectorySettings directories)
	{
		var clock = Stopwatch.StartNew();
		CUserSettings settings = CUserSettingsReader.Read(CUserSettingsReader.DefaultPath());
		// The localisation before any line, which is written from it: the picture alone until then.
		CLocalisation localisation = await screen.Work("", () => CLocalisation.Load(directories));
		screen.Localisation = localisation;
		// The original's order where the work is the same, each line stepping the pictures as the original's does with it.
		CGuiContext gui = null!;
		await screen.OnMain(CLoadingSteps.MapSprites, () => gui = CGuiContext.Load(directories, localisation), steps: 1);
		CCountries countries = await screen.Work(CLoadingSteps.Databases, () => CCountries.Load(directories, localisation));
		// The map's cache with the map logic, as the original checks and builds it there, the checksum reading two of its files.
		(CMap map, CMapCache cache) = await screen.Work(CLoadingSteps.MapLogic, () =>
		{
			CMap loaded = CMap.Load(directories);
			return (loaded, CMapView.OpenCache(directories, loaded));
		});
		CAiScripts aiScripts = await screen.Work(CLoadingSteps.SecondDatabases, () => new CAiScripts(directories));
		(CGameState world, CBookmark? start) = await screen.Work(CLoadingSteps.History, () =>
		{
			CGameState made = CGameStateLoader.BuildWorld(directories, map, localisation, countries, CDiagnostics.Warn, aiScripts);
			return (made, CBookmarkReader.ReadStart(directories, countries, made.StartDate));
		}, steps: 1);
		int checksum = await screen.Work(CLoadingSteps.Checksum, () => WorkOutChecksum(directories), steps: 1);
		IReadOnlyList<CQuadTile> tiles = await screen.Work(CLoadingSteps.Graphics,
			() => CQuadCache.Load(cache, problem => GD.PushWarning($"map: {problem}")));
		GD.Print($"Front end read in {clock.ElapsedMilliseconds} ms.");
		return new CFrontEndContent
		{
			Settings = settings,
			Localisation = localisation,
			Countries = countries,
			Map = map,
			AiScripts = aiScripts,
			Gui = gui,
			World = world,
			Start = start,
			Cache = cache,
			Tiles = tiles,
			Checksum = checksum,
		};
	}

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
		(int checksum, int files) = CFileChecksum.Compute(CChecksumSearchPath.ForInstall(directories, CUserSettingsReader.UserFolder()));
		GD.Print($"Checksum is {CFileChecksum.VersionName} ({CFileChecksum.Letters(checksum)})    {checksum}, "
			+ $"{files} files in {clock.ElapsedMilliseconds} ms.");
		_checksum = (directories.GameRoot, checksum);
		return checksum;
	}
}
