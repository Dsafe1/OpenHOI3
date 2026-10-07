using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Map;
using OpenHOI3.Sim;

namespace OpenHOI3;

/// <summary>
/// One game on screen: the game state, the map it is played on, and the nodes that
/// show them.
///
/// A game is an object, not the process. Everything a game owns hangs off this node,
/// so freeing it ends the game and adding another starts a new one - which is all that
/// "new game", "load" and "exit to menu" will need when there is a menu to offer them.
/// </summary>
public partial class CSession : Node
{
	private CDirectorySettings _directories = null!;
	private CMapView _mapView = null!;
	private CLocalisation _localisation = null!;
	private Label _provinceLabel = null!;

	public CGameState State { get; private set; } = null!;

	public CMap Map { get; private set; } = null!;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CDirectorySettings directories)
	{
		_directories = directories;
	}

	public override void _Ready()
	{
		// Loading is done here, on the main thread, and holds the first frame for as
		// long as it takes - about a second and a half for the base game. That is
		// acceptable while there is nothing else to show, and not once there is a
		// loading screen to keep moving.
		var clock = Stopwatch.StartNew();
		CLocalisation localisation = CLocalisation.Load(_directories);
		_localisation = localisation;
		CCountries countries = CCountries.Load(_directories, localisation);
		Map = CMap.Load(_directories);
		State = CGameStateLoader.LoadStart(_directories, Map, localisation, countries);
		GD.Print($"Loaded {Map.ProvinceCount - 1} provinces and {countries.Count} countries " +
			$"for {State.Date} in {clock.ElapsedMilliseconds} ms.");

		_mapView = new CMapView { Name = "Map" };
		_mapView.Setup(Map, State);
		AddChild(_mapView);

		BuildHud(localisation);
		_mapView.ProvinceHovered += ShowProvince;

		RunDeveloperArguments();
	}

	/// <summary>
	/// A date and a province readout, in plain engine controls. This is scaffolding:
	/// the game's own interface comes from the original's interface files, and arrives
	/// with the top bar.
	/// </summary>
	private void BuildHud(CLocalisation localisation)
	{
		var layer = new CanvasLayer { Name = "Hud" };
		AddChild(layer);

		var date = new Label
		{
			Text = State.Date.ToDisplayString(localisation.Localise),
			Position = new Vector2(16, 12),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		date.AddThemeFontSizeOverride("font_size", 22);
		date.AddThemeColorOverride("font_outline_color", Colors.Black);
		date.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(date);

		_provinceLabel = new Label
		{
			Position = new Vector2(16, 48),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_provinceLabel.AddThemeFontSizeOverride("font_size", 18);
		_provinceLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_provinceLabel.AddThemeConstantOverride("outline_size", 6);
		layer.AddChild(_provinceLabel);
	}

	private void ShowProvince(int provinceId)
	{
		if (provinceId == 0)
		{
			_provinceLabel.Text = "";
			return;
		}

		CProvince province = State.Province(provinceId);
		string text = $"{province.Name}  ({province.Id})";
		if (province.IsSea)
		{
			text += "\nSea";
		}
		else
		{
			if (province.Terrain is not null)
			{
				text += "\nTerrain: " + _localisation.Localise(province.Terrain.Name);
			}
			text += "\nOwner: " + CountryName(province.Owner);
			if (!Equals(province.Owner, province.Controller))
			{
				text += "\nController: " + CountryName(province.Controller);
			}
			if (province.Cores.Count > 0)
			{
				text += "\nCores: " + string.Join(", ", province.Cores.Select(core => CountryName(core)));
			}
		}
		_provinceLabel.Text = text;
	}

	private string CountryName(CCountryTag? tag)
	{
		if (tag is null)
		{
			return "nobody";
		}
		return State.Country(tag)?.Name ?? tag.Value.Value;
	}

	/// <summary>
	/// Arguments after <c>--</c> on the command line, for looking at the map without
	/// driving it by hand:
	/// <c>--centre=1861</c> puts a province in the middle and selects it,
	/// <c>--zoom=4</c> sets the zoom, <c>--mode=terrain</c> starts on the terrain map, and
	/// <c>--screenshot=path.png</c> saves what is on screen after a few frames and quits.
	/// </summary>
	private async void RunDeveloperArguments()
	{
		string? screenshot = null;
		int centre = 0;
		float? zoom = null;
		foreach (string argument in OS.GetCmdlineUserArgs())
		{
			if (argument.StartsWith("--screenshot="))
			{
				screenshot = argument["--screenshot=".Length..];
			}
			else if (argument.StartsWith("--centre="))
			{
				int.TryParse(argument["--centre=".Length..], NumberStyles.Integer,
					CultureInfo.InvariantCulture, out centre);
			}
			else if (argument == "--mode=terrain")
			{
				_mapView.Mode = CMapView.MapMode.Terrain;
			}
			else if (argument.StartsWith("--zoom=")
				&& float.TryParse(argument["--zoom=".Length..], NumberStyles.Float,
					CultureInfo.InvariantCulture, out float parsed))
			{
				zoom = parsed;
			}
		}

		if (centre > 0)
		{
			_mapView.CentreOn(centre, zoom);
			_mapView.Select(centre);
			ShowProvince(centre);
		}

		if (screenshot is null)
		{
			return;
		}
		for (int frame = 0; frame < 8; frame++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error error = GetViewport().GetTexture().GetImage().SavePng(screenshot);
		GD.Print(error == Error.Ok ? $"Saved {screenshot}" : $"Could not save {screenshot}: {error}");
		GetTree().Quit();
	}
}
