using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Screens;

/// <summary>
/// The province view, <c>province_view</c>: up while provinces are selected, about the one most recently selected. What
/// it says of the province is written as it is selected and not again; its weather, its building rows, its underground
/// and its covert-ops points are made again every frame. See <c>docs/mechanics/provinceview.md</c>.
/// </summary>
public sealed partial class CProvinceView : Control
{
	private const string WindowName = "province_view";

	/// <summary>The text boxes that answer for themselves though they let the mouse through.</summary>
	private static readonly string[] PassiveTexts =
	{
		"text_oil", "text_metal", "text_energy", "text_raremat", "text_espionage", "text_partisans", "text_manpower",
		"text_leadership", "temperature_txt",
	};

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry? _player;

	/// <summary>
	/// Whose eyes the view sees through: the player's, or with nobody played - a replay looking on - the province's
	/// controller's, else its owner's (docs/replay.md, section 6).
	/// </summary>
	private CCountry _viewer = null!;

	private CGuiWindow? _window;
	private CGuiWindow? _underground;
	private CGuiWindow? _covertOps;
	private CProvince? _province;
	private IReadOnlyList<CProvince> _selected = Array.Empty<CProvince>();
	private CCountryTag? _owner;
	private CCountryTag? _controller;
	private readonly List<CBuildingMemberEntry> _buildings = new();
	private readonly List<CProvinceWayEntry> _ways = new();
	private readonly List<CCovertOpsEntry> _operations = new();
	private readonly List<CAlliedObjectiveEntry> _allies = new();
	private int _askedCount = -1;

	public void Setup(CGuiContext context, CSimulation simulation, CCountry? player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
		_viewer = player!;
	}

	/// <summary>A replay, which takes no order: nothing in the view may be pressed (docs/replay.md, section 6).</summary>
	private bool LooksOn => !_simulation.TakesCommands;

	public bool IsOpen => _window is { Visible: true };

	/// <summary>The close button was pressed: the selection is to be cleared.</summary>
	public event Action? CloseRequested;

	/// <summary>A flag was pressed: its country, and whether it is the occupier's.</summary>
	public event Action<CCountryTag, bool>? FlagPressed;

	private CGameState State => _simulation.State;

	private Func<string, string> Localise => _context.Localisation.Localise;

	public override void _Ready()
	{
		Name = "ProvinceView";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_underground = _window.FindWindow("underground");
		_covertOps = _window.FindWindow("cov_ops");

		_window.SetTooltips(Tooltip, PassiveTexts);
		if (_window.TryFindButton("owner_flag") is { } ownerFlag)
		{
			ownerFlag.Pressed += () => Flag(occupier: false);
			ownerFlag.Tooltip = () => FlagTooltip(_owner, "owner_flag");
		}
		if (_window.TryFindButton("occupier_flag") is { } occupierFlag)
		{
			occupierFlag.Pressed += () => Flag(occupier: true);
			occupierFlag.Tooltip = () => FlagTooltip(_controller, "occupier_flag");
		}
		foreach (CGuiWindow? window in new[] { _window, _underground })
		{
			if (window?.TryFindButton("close_button") is { } close)
			{
				close.Pressed += () => CloseRequested?.Invoke();
			}
		}
		if (_window.TryFindButton("open_cov_ops") is { } openCovertOps)
		{
			openCovertOps.Pressed += ToggleCovertOps;
		}
		if (_window.TryFindButton("set_cooperation") is { } setCooperation)
		{
			setCooperation.Pressed += ToggleAllies;
		}
		if (_underground is not null)
		{
			if (_underground.TryFindButton("spawn_partisan") is { } partisans)
			{
				partisans.Pressed += () => Spawn(partisans: true);
			}
			if (_underground.TryFindButton("spawn_underground") is { } spread)
			{
				spread.Pressed += () => Spawn(partisans: false);
			}
			if (_underground.TryFindButton("underground_mission") is { } mission)
			{
				mission.Pressed += () => StepMission(mission, forward: true);
				mission.RightPressed += () => StepMission(mission, forward: false);
			}
		}
		_window.Visible = false;
	}

	/// <summary>
	/// The view up on the province, written now: with several selected, how many and nothing else of the province's own.
	/// Both panels are closed.
	/// </summary>
	public void Show(CProvince province, IReadOnlyList<CProvince> selected)
	{
		if (_window is null)
		{
			return;
		}
		if ((_player ?? State.Country(province.Controller) ?? State.Country(province.Owner)) is not { } viewer)
		{
			Hide();
			return;
		}
		_viewer = viewer;
		_province = province;
		_selected = selected;
		_window.Visible = true;
		MakeBuildingRows();
		if (selected.Count > 1)
		{
			ShowSeveral(selected.Count);
		}
		else
		{
			ShowOne(province);
		}
		SetPanel(_covertOps, false);
		SetAllies(false);
		UpdateFrame();
	}

	/// <summary>Puts the view's window away; the node itself stays.</summary>
	public new void Hide()
	{
		if (_window is not null)
		{
			_window.Visible = false;
		}
	}

	public override void _Process(double delta)
	{
		if (IsOpen)
		{
			UpdateFrame();
		}
	}

	private void ShowSeveral(int count)
	{
		foreach (string name in new[]
		{
			"day_night", "owner_flag", "occupier_flag", "text_espionage", "text_partisans", "text_manpower",
			"text_leadership", "text_oil", "text_metal", "text_energy", "text_raremat", "strategic_resource",
			"temperature_txt", "wind_status",
		})
		{
			SetVisible(name, false);
		}
		SetVisible(_window!.FindListBox("adj"), false);
		SetVisible(_window.FindBox("claims"), false);
		SetText("text_title", CProvinceViewText.MultiTitle(Localise, count));
	}

	private void ShowOne(CProvince province)
	{
		CGuiWindow window = _window!;
		foreach (string name in new[]
		{
			"day_night", "owner_flag", "text_espionage", "text_partisans", "text_manpower", "text_leadership", "text_oil",
			"text_metal", "text_energy", "text_raremat", "temperature_txt", "wind_status",
		})
		{
			SetVisible(name, true);
		}
		SetText("text_title", province.Name);
		if (_underground?.FindTextBox("text_title") is { } undergroundTitle)
		{
			undergroundTitle.Text = province.Name;
		}

		_owner = province.Owner;
		_controller = province.Controller;
		if (window.TryFindButton("owner_flag") is { } ownerFlag && State.Country(_owner) is { } owner)
		{
			ownerFlag.SetCountry(owner);
		}
		if (window.TryFindButton("occupier_flag") is { } occupierFlag)
		{
			occupierFlag.Visible = _controller != _owner && State.Country(_controller) is not null;
			if (State.Country(_controller) is { } controller)
			{
				occupierFlag.SetCountry(controller);
			}
		}

		if (window.TryFindIcon("terrain") is { } terrain)
		{
			string picture = CProvinceViewText.PicturePath(province);
			if (_context.Textures.Exists(picture) && terrain.SetSprite("GFX_terrainimg_plains")
				&& _context.Textures.LoadFromGamePath(picture) is { } texture)
			{
				terrain.ShowPicture(texture);
			}
			else
			{
				terrain.SetSprite(CProvinceViewText.TerrainSprite(province));
			}
		}

		SetText("text_espionage", CProvinceViewText.Espionage(State, province, _viewer.Tag));
		SetText("text_partisans", CRevoltRiskTooltip.Readout(State, province, _viewer.Tag));
		SetText("text_manpower", CProvinceViewText.Manpower(State, province, _viewer.Tag));
		SetText("text_leadership", CProvinceViewText.Leadership(State, province, _viewer.Tag));
		foreach ((Sim.Economy.EGoods goods, string text, string _, string _) in CProvinceViewText.Resources)
		{
			SetText(text, CProvinceViewText.Resource(State, province, _viewer.Tag, goods));
		}
		if (window.TryFindIcon("strategic_resource") is { } strategic)
		{
			string sprite = CProvinceViewText.StrategicSprite(province);
			strategic.Visible = sprite.Length > 0 && strategic.SetSprite(sprite);
		}

		if (window.FindListBox("adj") is { } ways)
		{
			// Emptying the list frees its rows, so they are made again for each province.
			ways.Clear();
			_ways.Clear();
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (CProvinceWayEntry.Create(_context, () => State) is { } way)
				{
					way.Show(State, edge);
					_ways.Add(way);
					ways.Add(way.Window);
				}
			}
			ways.Visible = true;
		}

		if (window.FindBox("claims") is { } claims)
		{
			claims.Clear();
			foreach (CCountryTag core in CProvinceViewText.Claims(province))
			{
				if (State.Country(core) is { } country && CGuiWindow.CreateEntry(_context, "prov_claims_entry") is { } entry)
				{
					if (entry.TryFindIcon("flag") is { } flag)
					{
						flag.SetCountry(country);
						flag.Tooltip = () => CProvinceViewTooltips.Flag(Localise, country, "flag");
					}
					claims.Add(entry);
				}
			}
			claims.Visible = true;
		}
		_askedCount = -1;
	}

	/// <summary>The building rows, made once: the file's buildings in two columns, each with its key.</summary>
	private void MakeBuildingRows()
	{
		if (_buildings.Count > 0 || _window is null)
		{
			return;
		}
		(List<CBuilding> left, List<CBuilding> right) = CProvinceBuildingRows.Columns(State.Buildings);
		var names = new List<string>();
		foreach (CBuilding building in left)
		{
			names.Add(Localise(building.Key));
		}
		foreach (CBuilding building in right)
		{
			names.Add(Localise(building.Key));
		}
		List<(string Shortcut, string Label)> keys = CProvinceBuildingRows.Shortcuts(names);
		int row = 0;
		foreach ((CGuiListBox? list, List<CBuilding> buildings) in new[]
			{ (_window.FindListBox("buildings_left"), left), (_window.FindListBox("buildings_right"), right) })
		{
			foreach (CBuilding building in buildings)
			{
				(string shortcut, string label) = keys[row++];
				if (CBuildingMemberEntry.Create(_context, building, shortcut, label) is not { } entry)
				{
					continue;
				}
				entry.SetTooltips(
					() => _province is null ? CTooltip.Empty
						: CProvinceViewTooltips.Building(Localise, State, _viewer, _province, entry.Building, entry.Label),
					() => _province is null ? CTooltip.Empty
						: CProvinceViewTooltips.Pip(Localise, State, _viewer, _province, entry.Building));
				entry.Pressed += Build;
				_buildings.Add(entry);
				list?.Add(entry.Window);
			}
		}
	}

	/// <summary>What is made again every frame: the weather, the rows, the underground, the points and the asked allies.</summary>
	private void UpdateFrame()
	{
		if (_window is null || _province is not { } province)
		{
			return;
		}
		Sim.Weather.CWeather weather = province.Weather;
		_window.TryFindIcon("weather_icon")?.SetFrame(CProvinceViewText.WeatherFrame(State, weather));
		_window.TryFindIcon("ground_status_icon")?.SetFrame(CProvinceViewText.GroundFrame(weather));
		_window.TryFindIcon("day_night")?.SetFrame(CProvinceViewText.DayNightFrame(State, province));
		_window.TryFindIcon("wind_status")?.SetProgress(CProvinceViewText.WindPercent(weather));
		SetText("temperature_txt", CProvinceViewText.Temperature(weather));

		foreach (CBuildingMemberEntry row in _buildings)
		{
			row.Show(!LooksOn && CProvinceBuildingRows.Enabled(State, _viewer, row.Building, province, _selected),
				CProvinceBuildingRows.Pips(State, _viewer, row.Building, province));
		}
		UpdateUnderground(province);
		SetText("cov_ops_points", CProvinceCovertOpsText.Points(State, _viewer, _controller)
			.ToString(System.Globalization.CultureInfo.InvariantCulture));
		if (_covertOps is { Visible: true })
		{
			foreach (CCovertOpsEntry row in _operations)
			{
				row.Show(!LooksOn && CProvinceCovertOpsText.Enabled(State, _viewer, province, row.Operation));
			}
		}
		List<CCountryTag> asked = State.AlliedObjectives.AskedBy(_viewer.Tag, province.Id);
		if (asked.Count != _askedCount && _window.FindBox("allied_cooperation") is { } box)
		{
			_askedCount = asked.Count;
			box.Clear();
			foreach (CCountryTag tag in asked)
			{
				if (State.Country(tag) is { } ally && CGuiWindow.CreateEntry(_context, "prov_claims_entry") is { } entry)
				{
					if (entry.TryFindIcon("flag") is { } flag)
					{
						flag.SetCountry(ally);
						flag.Tooltip = () => CProvinceViewTooltips.Flag(Localise, ally, "flag");
					}
					box.Add(entry);
				}
			}
		}
	}

	private void UpdateUnderground(CProvince province)
	{
		if (_underground is null)
		{
			return;
		}
		if (CProvinceUndergroundText.About(State, _viewer, province, _selected) is not { } about)
		{
			_underground.Visible = false;
			return;
		}
		_underground.Visible = true;
		if (_underground.TryFindButton("spawn_partisan") is { } partisans)
		{
			partisans.Disabled = LooksOn || !CProvinceUndergroundText.MaySpawn(State, _viewer, about, _selected, partisans: true);
		}
		if (_underground.TryFindButton("spawn_underground") is { } spread)
		{
			spread.Disabled = LooksOn || !CProvinceUndergroundText.MaySpawn(State, _viewer, about, _selected, partisans: false);
		}
		_underground.TryFindIcon("underground_status")?.SetProgress(CProvinceUndergroundText.Status(State, about));
		if (_underground.FindTextBox("text_supression") is { } suppression)
		{
			suppression.Text = CProvinceUndergroundText.Suppression(State, about);
		}
		if (_underground.TryFindButton("underground_mission") is { } mission)
		{
			mission.SetFrame(CProvinceUndergroundText.MissionFrame(State, about));
			mission.Disabled = LooksOn || about.UndergroundOwner != _viewer.Tag;
		}
	}

	private CTooltip Tooltip(string name)
	{
		return _province is null ? CTooltip.Empty : CProvinceViewTooltips.Element(Localise, State, _viewer, _province, name);
	}

	private CTooltip FlagTooltip(CCountryTag? tag, string element)
	{
		return State.Country(tag) is { } country ? CProvinceViewTooltips.Flag(Localise, country, element) : CTooltip.Empty;
	}

	private void Flag(bool occupier)
	{
		if ((occupier ? _controller : _owner) is { } tag)
		{
			FlagPressed?.Invoke(tag, occupier);
		}
	}

	/// <summary>A building row pressed: a level ordered in every selected province the player holds where it may go.</summary>
	private void Build(CBuildingMemberEntry row)
	{
		foreach (CProvince province in Targets())
		{
			if (province.Controller == _viewer.Tag && Sim.Construction.CBuildingLines.IsAllowed(State, _viewer, row.Building, province))
			{
				_simulation.Post(new CConstructBuildingCommand(_viewer.Tag, row.Building.Key, province.Id, 1));
			}
		}
	}

	/// <summary>A spawn button pressed: for the window's province, or every selected one with the player's underground that may.</summary>
	private void Spawn(bool partisans)
	{
		foreach (CProvince province in Targets())
		{
			bool may = partisans
				? CUndergrounds.MayRaisePartisans(State, _viewer.Tag, province)
				: CUndergrounds.MaySpread(State, _viewer.Tag, province);
			if (may)
			{
				_simulation.Post(partisans ? new CSpawnPartisanCommand(province.Id) : new CSpawnUndergroundCommand(province.Id));
			}
		}
	}

	/// <summary>The mission's button stepped - forward by a left click, back by a right - and the new action sent.</summary>
	private void StepMission(CGuiButton mission, bool forward)
	{
		if (_province is null || CProvinceUndergroundText.About(State, _viewer, _province, _selected) is not { } about)
		{
			return;
		}
		int frame = CProvinceUndergroundText.Step(CProvinceUndergroundText.MissionFrame(State, about), forward);
		mission.SetFrame(frame);
		foreach (CProvince province in Targets())
		{
			if (province.UndergroundOwner == _viewer.Tag && CUndergrounds.Has(State, province))
			{
				_simulation.Post(new CChangeUndergroundMissionCommand(province.Id, frame - 1));
			}
		}
	}

	/// <summary>The provinces an order goes to: every one selected, or the view's alone.</summary>
	private IEnumerable<CProvince> Targets()
	{
		if (_selected.Count >= 2)
		{
			return _selected;
		}
		return _province is null ? Array.Empty<CProvince>() : new[] { _province };
	}

	private void ToggleCovertOps()
	{
		if (_covertOps is null || _province is null)
		{
			return;
		}
		if (_covertOps.Visible)
		{
			SetPanel(_covertOps, false);
			return;
		}
		SetAllies(false);
		CGuiListBox? list = _covertOps.FindListBox("cov_ops_list");
		list?.Clear();
		_operations.Clear();
		CProvince province = _province;
		foreach (CCovertOperation operation in CProvinceCovertOpsText.Listed(State, _viewer, province))
		{
			if (CCovertOpsEntry.Create(_context, operation,
				disabled => CProvinceViewTooltips.CovertOperation(Words(), _viewer, province, operation, disabled)) is { } row)
			{
				row.Pressed += entry => _simulation.Post(new CSendCovertOpsMissionCommand(_viewer.Tag, province.Id, entry.Operation.Name));
				row.Show(!LooksOn && CProvinceCovertOpsText.Enabled(State, _viewer, province, operation));
				_operations.Add(row);
				list?.Add(row.Window);
			}
		}
		SetPanel(_covertOps, true);
	}

	private void ToggleAllies()
	{
		if (_window?.FindListBox("cooperation_list") is not { } list || _province is null)
		{
			return;
		}
		if (list.Visible)
		{
			SetAllies(false);
			return;
		}
		SetPanel(_covertOps, false);
		// Emptying the list frees its rows, so they are made again each time it opens.
		list.Clear();
		_allies.Clear();
		List<CCountryTag> asked = State.AlliedObjectives.AskedBy(_viewer.Tag, _province.Id);
		foreach (CCountry ally in CProvinceCovertOpsText.Allies(State, _viewer))
		{
			if (CAlliedObjectiveEntry.Create(_context) is not { } entry)
			{
				continue;
			}
			entry.Changed += (country, ticked) =>
			{
				if (_province is { } here)
				{
					_simulation.Post(new CSetAlliedObjectiveCommand(country.Tag, _viewer.Tag, here.Id, ticked));
				}
			};
			entry.Show(ally, asked.Contains(ally.Tag), enabled: !LooksOn);
			_allies.Add(entry);
			list.Add(entry.Window);
		}
		SetAllies(true);
	}

	private void SetAllies(bool shown)
	{
		if (_window is null)
		{
			return;
		}
		SetVisible(_window.FindListBox("cooperation_list"), shown);
		SetVisible("cooperation_bg", shown);
		SetVisible("coop_label", shown);
	}

	private static void SetPanel(CGuiWindow? panel, bool shown)
	{
		if (panel is not null)
		{
			panel.Visible = shown;
		}
	}

	private void SetText(string name, string text)
	{
		if (_window?.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	private void SetVisible(string name, bool visible)
	{
		SetVisible(_window?.TryFindElement(name), visible);
	}

	private static void SetVisible(Control? element, bool visible)
	{
		if (element is not null)
		{
			element.Visible = visible;
		}
	}

	private CScriptWords Words() => new(Localise, State, _viewer.Tag);
}
