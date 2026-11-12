using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using OpenHOI3.Content;
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
		CMap map = await screen.Work(CLoadingSteps.MapLogic, () => CMap.Load(directories));
		CAiScripts aiScripts = await screen.Work(CLoadingSteps.SecondDatabases, () => new CAiScripts(directories));
		(CGameState world, CBookmark? start) = await screen.Work(CLoadingSteps.History, () =>
		{
			CGameState made = CGameStateLoader.BuildWorld(directories, map, localisation, countries, CDiagnostics.Warn, aiScripts);
			return (made, CBookmarkReader.ReadStart(directories, countries, made.StartDate));
		}, steps: 1);
		// The original steps once more before this, at its checksum, which has no step here.
		(CMapCache cache, IReadOnlyList<CQuadTile> tiles) = await screen.Work(CLoadingSteps.Graphics, () =>
		{
			CMapCache opened = CMapView.OpenCache(directories, map);
			return (opened, CQuadCache.Load(opened, problem => GD.PushWarning($"map: {problem}")));
		}, steps: 1);
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
		};
	}
}
