using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Gui;
using OpenHOI3.Interface.BattlePlans;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Screens;

/// <summary>
/// The battle plans window, <c>battleplans_window</c>: the three lists of plans, what is done with them, the strip of tools
/// and the mouse on the map while one of them is held. It pauses nothing. See <c>docs/mechanics/battleplans.md</c>.
/// </summary>
public partial class CBattlePlanWindow : Control
{
	private const int CurrentTab = 0, HistoricalTab = 1, SavedTab = 2;

	private CGuiContext _context = null!;
	private CGameState _state = null!;
	private CCountry _player = null!;
	private CDirectorySettings _directories = null!;
	private string _savedRoot = "";
	private CMapView _map = null!;

	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private CGuiWindow? _actionsCurrent, _actionsHistorical, _actionsSaved, _tools, _options, _textPrompt;
	private CGuiButton? _tabCurrent, _tabHistorical, _tabSaved, _saveConfirm, _saveCancel;
	private CGuiTextBox? _saveBox, _textBox;
	private CGuiIcon? _toolMarker, _optionMarker;
	private readonly List<CGuiButton> _toolButtons = new();
	private readonly List<CGuiButton> _optionButtons = new();
	private readonly List<CBattlePlanRow> _rows = new();
	private List<CBattlePlan> _historical = new(), _saved = new();
	private CBattlePlan? _selected, _toSave;
	private int _tab;
	private bool _saving, _renaming, _leftHeld;
	private Vector2 _mouse;
	private CConfirmDialog? _dialog;
	private CPlanTool? _optionsDrawn;
	private CPlanObjectTypes _types = null!;
	private double _time;

	public CBattlePlanBook Book { get; } = new();

	public CPlanEditor Editor { get; private set; } = null!;

	/// <summary>Whether plans are drawn with the window shut: the menu bar's toggle.</summary>
	public bool ShowOnMap { get; set; } = true;

	public bool IsOpen => _window is { Visible: true };

	public void Setup(CGuiContext context, CGameState state, CCountry player, CDirectorySettings directories, string savedRoot, CMapView map,
		CBattlePlanSettings settings, CPlanObjectTypes types)
	{
		_context = context;
		_state = state;
		_player = player;
		_directories = directories;
		_savedRoot = savedRoot;
		_map = map;
		_types = types;
		Editor = new CPlanEditor(settings, types);
	}

	private string L(string key) => _context.Localisation.Localise(key);

	public override void _Ready()
	{
		Name = CBattlePlanText.WindowName;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, CBattlePlanText.WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_window.Visible = false;
		_list = _window.FindListBox("listbox_plans");
		_actionsCurrent = _window.FindWindow("actions_list_current");
		_actionsHistorical = _window.FindWindow("actions_list_historical");
		_actionsSaved = _window.FindWindow("actions_list_saved");
		_tools = _window.FindWindow("tools");
		_options = _tools?.FindWindow("options_window");
		_textPrompt = _window.FindWindow("text_prompt_window");
		_saveConfirm = _window.FindButton("save_confirm");
		_saveCancel = _window.FindButton("save_cancel");
		_saveBox = _window.FindTextBox("save_editbox");
		_saveBox?.MakeEditable();
		_textBox = _textPrompt?.FindTextBox("text_editbox");
		_textBox?.MakeEditable();
		if (_window.FindButton("close_button") is { } close)
		{
			close.Pressed += Hide;
		}
		_tabCurrent = Tab("tab_current", CurrentTab, "BP_TOOLTIP_TAB_CURRENT");
		_tabHistorical = Tab("tab_historical", HistoricalTab, "BP_TOOLTIP_TAB_HISTORICAL");
		_tabSaved = Tab("tab_saved", SavedTab, "BP_TOOLTIP_TAB_SAVED");
		Bind(_actionsCurrent, "new_plan", "BP_TOOLTIP_NEW_PLAN", NewPlan);
		Bind(_actionsCurrent, "remove_plan", "BP_TOOLTIP_REMOVE_PLAN", RemovePlan);
		Bind(_actionsCurrent, "save_plan", "BP_TOOLTIP_SAVE_PLAN", SavePlan);
		Bind(_actionsCurrent, "rename_plan", "BP_TOOLTIP_RENAME_PLAN", RenamePlan);
		Bind(_actionsHistorical, "load_plan", "BP_TOOLTIP_IMPORT_PLAN", LoadPlan);
		Bind(_actionsSaved, "load_plan", "BP_TOOLTIP_IMPORT_PLAN", LoadPlan);
		Bind(_actionsSaved, "delete_plan", "BP_TOOLTIP_REMOVE_PLAN", DeletePlan);
		if (_saveConfirm is not null)
		{
			_saveConfirm.Pressed += SaveConfirm;
		}
		if (_saveCancel is not null)
		{
			_saveCancel.Pressed += SaveCancel;
		}
		if (_textPrompt?.TryFindButton("apply") is { } apply)
		{
			apply.Pressed += () => Editor.Apply(Typed(_textBox));
		}
		MakeTools();
		MakeOptions();
		Rescan();
		_map.PlanInput = MapInput;
	}

	private CGuiButton? Tab(string name, int tab, string tooltip)
	{
		CGuiButton? button = _window?.TryFindButton(name);
		if (button is not null)
		{
			button.Pressed += () =>
			{
				_tab = tab;
				if (tab != SavedTab)
				{
					_saving = false;
				}
				Refill();
			};
			button.Tooltip = () => new CTooltip { Text = L(tooltip) };
		}
		return button;
	}

	private void Bind(CGuiWindow? window, string name, string tooltip, Action action)
	{
		if (window?.TryFindButton(name) is { } button)
		{
			button.Pressed += action;
			button.Tooltip = () => new CTooltip { Text = L(tooltip) };
		}
	}

	private void MakeTools()
	{
		if (_tools is null)
		{
			return;
		}
		_toolMarker = _tools.TryFindIcon("marker");
		for (int tool = 0; tool < CBattlePlanText.ToolButtons.Length; tool++)
		{
			if (_tools.TryFindButton(CBattlePlanText.ToolButtons[tool]) is not { } button)
			{
				continue;
			}
			int chosen = tool;
			button.Pressed += () => PressTool((EPlanTool)chosen);
			button.Tooltip = () => new CTooltip { Text = L(CBattlePlanText.ToolTip(chosen)) };
			_toolButtons.Add(button);
		}
	}

	private void PressTool(EPlanTool tool)
	{
		string? box = Editor.PressTool(tool);
		if (box is not null && _textBox?.Editor is { } editor)
		{
			editor.Text = box;
			_textBox.Text = box;
			_textBox.TakeKeyboard();
		}
	}

	/// <summary>As many entries as the longest list of options, right-aligned, seven to a column (battleplans.md, section 9).</summary>
	private void MakeOptions()
	{
		if (_options is null)
		{
			return;
		}
		_optionMarker = _options.TryFindIcon("marker");
		float width = _options.Size.X;
		for (int index = 0; index < Editor.LongestOptions; index++)
		{
			if (_context.Types.Find("battleplan_tool_option_entry") is not Content.Gui.CGuiButtonType type)
			{
				continue;
			}
			var button = new CGuiButton();
			button.Initialise(type, _context);
			int chosen = index;
			button.Position = OptionPlace(index, width);
			button.Pressed += () => Editor.ChooseOption(chosen);
			button.Tooltip = () => Editor.OptionsOf(Editor.Tool) is { } tool && chosen < tool.Options.Count
				? new CTooltip { Text = L(tool.Options[chosen].Name) }
				: CTooltip.Empty;
			_options.AddChild(button);
			_optionButtons.Add(button);
		}
		if (_optionMarker is not null)
		{
			_options.MoveChild(_optionMarker, _options.GetChildCount() - 1);
		}
	}

	/// <summary>Where an option stands in its grid: seven down a column, the columns going left from the window's right edge.</summary>
	private static Vector2 OptionPlace(int index, float width) => new(width + (index / 7 + 1) * -33f, index % 7 * 33f);

	public void Toggle()
	{
		if (IsOpen)
		{
			Hide();
		}
		else
		{
			Show();
		}
	}

	private new void Show()
	{
		if (_window is null)
		{
			return;
		}
		_window.Visible = true;
		_window.MoveToFront();
		_tab = CurrentTab;
		_saving = false;
		Refill();
	}

	private new void Hide()
	{
		if (_window is null)
		{
			return;
		}
		Editor.ClearSelection();
		_window.Visible = false;
	}

	private string TagFolder(string root) => Path.Combine(root, _player.Tag.Value);

	/// <summary>Files listed by their headers: a historical one that fails goes, a saved one stays a row.</summary>
	private void Rescan()
	{
		_historical = new List<CBattlePlan>();
		// Every file of the folder over the whole search path, by the original's lister - names sorted by byte with case.
		foreach (string file in _directories.ListFiles("battleplans/historical/" + _player.Tag.Value, "*"))
		{
			CBattlePlan plan = CBattlePlanFile.ReadHeader(file);
			if (plan.HeaderGood)
			{
				_historical.Add(plan);
			}
		}
		_saved = Files(TagFolder(_savedRoot)).Select(CBattlePlanFile.ReadHeader).ToList();
	}

	private static IEnumerable<string> Files(string? folder)
	{
		return folder is not null && Directory.Exists(folder) ? Directory.GetFiles(folder) : Enumerable.Empty<string>();
	}

	private List<CBattlePlan> Listed() => _tab switch
	{
		HistoricalTab => _historical,
		SavedTab => _saved,
		_ => Book.Shown(_state, _player),
	};

	private void Refill()
	{
		if (_list is null)
		{
			return;
		}
		_list.Clear();
		_rows.Clear();
		List<CBattlePlan> plans = Listed();
		if (_selected is null || !plans.Contains(_selected))
		{
			_selected = plans.FirstOrDefault(plan => plan.Country == _player.Tag.Value);
		}
		foreach (CBattlePlan plan in plans)
		{
			if (CBattlePlanRow.Create(_context, _state, _player, plan) is { } row)
			{
				row.Chosen += () => Choose(plan);
				_rows.Add(row);
				_list.Add(row.Window);
			}
		}
	}

	private void Choose(CBattlePlan plan)
	{
		Editor.ClearSelection();
		_selected = plan;
		if (_saving && _tab == SavedTab && _saveBox?.Editor is { } editor && plan.FilePath.Length > 0)
		{
			string name = Path.GetFileName(plan.FilePath);
			editor.Text = name;
			_saveBox.Text = name;
		}
	}

	/// <summary>The chosen plan, where it is whole and the player's.</summary>
	private CBattlePlan? Own => _selected is { IsLive: true } plan && plan.Country == _player.Tag.Value ? plan : null;

	private void NewPlan()
	{
		var plan = new CBattlePlan { Title = L("BP_NEW_PLAN"), Country = _player.Tag.Value, IsLive = true, IsVisible = true, IsDirty = true };
		Book.Add(plan);
		_tab = CurrentTab;
		_selected = plan;
		Refill();
		RenamePlan();
	}

	private void RemovePlan()
	{
		if (Own is not { } plan)
		{
			return;
		}
		Confirm("DefaultConfirm", L("BP_DELETE_CURRENT_PLAN_PROMPT"), () =>
		{
			Book.Remove(plan);
			_selected = null;
			Refill();
		});
	}

	private void Confirm(string window, string text, Action agreed)
	{
		_dialog = CConfirmDialog.Open(this, _context, window, "", text);
		if (_dialog is not null)
		{
			_dialog.Agreed += agreed;
		}
	}

	private void SavePlan()
	{
		if (Own is not { } plan)
		{
			return;
		}
		_toSave = plan;
		_saving = true;
		_tab = SavedTab;
		Refill();
		FillNameBox(plan.Title);
	}

	private void RenamePlan()
	{
		if (Own is not { } plan)
		{
			return;
		}
		_renaming = true;
		FillNameBox(plan.Title);
	}

	private void FillNameBox(string text)
	{
		if (_saveBox?.Editor is { } editor)
		{
			editor.Text = text;
			_saveBox.Text = text;
			_saveBox.TakeKeyboard();
		}
	}

	private static string Typed(CGuiTextBox? box) => box?.Editor?.Text ?? "";

	private void SaveConfirm()
	{
		string typed = Typed(_saveBox);
		if (_renaming)
		{
			if (typed.Length > 0 && Own is { } plan)
			{
				plan.Title = typed;
				plan.IsDirty = true;
			}
			_renaming = false;
			return;
		}
		string name = typed.Trim();
		if (!_saving || name.Length == 0 || _toSave is null)
		{
			return;
		}
		if (!CBattlePlanText.IsLegalFileName(name))
		{
			CConfirmDialog.Open(this, _context, "DefaultInfoDialog", L("BP_FAILED_SAVE_FILE"), L("BP_ILLEGAL_FILE_NAME"));
			return;
		}
		string path = Path.Combine(TagFolder(_savedRoot), CBattlePlanText.WithExtension(name));
		if (File.Exists(path))
		{
			Confirm("DefaultConfirm", L("BP_SAVE_PLAN_PROMPT"), () => WriteTo(path));
		}
		else
		{
			WriteTo(path);
		}
	}

	private void WriteTo(string path)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, System.Text.Encoding.Latin1.GetBytes(CBattlePlanFile.Write(_toSave!)));
		}
		catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
		{
			CDiagnostics.Warn($"battle plans: could not write {path}: {failed.Message}");
		}
		_saving = false;
		Rescan();
		_tab = SavedTab;
		Refill();
	}

	private void SaveCancel()
	{
		if (_renaming)
		{
			_renaming = false;
			_tab = CurrentTab;
			Refill();
			return;
		}
		_saving = false;
	}

	/// <summary>The file row chosen read whole and given to the country its file names, drawn and shown.</summary>
	private void LoadPlan()
	{
		if (_selected is not { IsLive: false, FilePath.Length: > 0, HeaderGood: true } row)
		{
			return;
		}
		CBattlePlan plan = CBattlePlanFile.ReadFile(row.FilePath);
		plan.IsVisible = true;
		Book.Add(plan);
	}

	private void DeletePlan()
	{
		if (_selected is not { FilePath.Length: > 0, IsLive: false } row)
		{
			return;
		}
		Confirm("DefaultConfirm", L("BP_DELETE_SAVED_PLAN_PROMPT"), () =>
		{
			try
			{
				File.Delete(row.FilePath);
			}
			catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
			{
				CDiagnostics.Warn($"battle plans: could not delete {row.FilePath}: {failed.Message}");
			}
			Rescan();
			_selected = null;
			_tab = SavedTab;
			Refill();
		});
	}

	public override void _Input(InputEvent input)
	{
		if (input is InputEventMouse mouse)
		{
			_mouse = mouse.Position;
		}
		if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release && _leftHeld)
		{
			_leftHeld = false;
			(float x, float y) = _map.WorldAt(release.Position);
			Editor.Release(x, y);
		}
	}

	/// <summary>What the map hands over first; true where the plan has it and the map is to do nothing.</summary>
	private bool MapInput(InputEvent input)
	{
		if (!IsOpen || Editor.Plan is null)
		{
			return false;
		}
		bool ctrl = Input.IsKeyPressed(Key.Ctrl), shift = Input.IsKeyPressed(Key.Shift);
		switch (input)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
				bool takes = Editor.TakesLeftPress(ctrl, shift);
				(float x, float y) = _map.WorldAt(press.Position);
				_leftHeld = true;
				Editor.Press(x, y, shift);
				return takes;
			case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel when Editor.TakesWheel(ctrl, shift):
				Editor.Wheel(wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1, ctrl, shift);
				return true;
			default:
				return false;
		}
	}

	/// <summary>What is drawn on the map each frame, the window up or not: the plans and whatever is being drawn.</summary>
	private void DrawOnMap(double delta)
	{
		_time += delta;
		var strokes = new List<CPlanStroke>();
		var icons = new List<CPlanIcon>();
		var texts = new List<CPlanText>();
		var arrows = new List<CPlanStroke>();
		if (ShowOnMap || IsOpen)
		{
			foreach (CBattlePlan plan in Book.Shown(_state, _player).Where(plan => plan.IsVisible))
			{
				strokes.AddRange(plan.Arrows);
				strokes.AddRange(plan.Borders);
				arrows.AddRange(plan.Arrows);
				icons.AddRange(plan.Icons);
				texts.AddRange(plan.Texts);
			}
			if (Editor.Drawing is { } drawing)
			{
				strokes.Add(drawing);
			}
			if (Editor.HeldIcon is { } held)
			{
				icons.Add(held);
			}
			if (Editor.Preview is { } preview)
			{
				texts.Add(preview);
			}
		}
		_map.Surface3D?.DrawBattlePlans(strokes, icons, _types, Editor.Colour, _time);
		(float r, float g, float b) = Editor.Colour(0);
		_map.PlanSigns?.Show(texts, arrows, new Color(r, g, b), _time);
	}

	/// <summary>Raised each frame with whether plans are drawn and whether the window is up, for the bar's buttons.</summary>
	public event Action<bool, bool>? ButtonsShown;

	public override void _Process(double delta)
	{
		DrawOnMap(delta);
		ButtonsShown?.Invoke(ShowOnMap, IsOpen);
		if (!IsOpen)
		{
			return;
		}
		SetFrames();
		ShowActions();
		ShowTools();
		foreach (CBattlePlanRow row in _rows)
		{
			row.Update(ReferenceEquals(row.Plan, _selected));
		}
		(float x, float y) = _map.WorldAt(_mouse);
		Editor.Move(x, y);
	}

	private void SetFrames()
	{
		_tabCurrent?.SetFrame(_tab == CurrentTab ? 1 : 2);
		_tabHistorical?.SetFrame(_tab == HistoricalTab ? 1 : 2);
		_tabSaved?.SetFrame(_tab == SavedTab ? 1 : 2);
	}

	private void ShowActions()
	{
		bool naming = _saving || _renaming;
		if (_actionsCurrent is not null)
		{
			_actionsCurrent.Visible = !naming && _tab == CurrentTab;
		}
		if (_actionsHistorical is not null)
		{
			_actionsHistorical.Visible = !naming && _tab == HistoricalTab;
		}
		if (_actionsSaved is not null)
		{
			_actionsSaved.Visible = !naming && _tab == SavedTab;
		}
		if (_saveBox is not null)
		{
			_saveBox.Visible = naming;
		}
		if (_saveCancel is not null)
		{
			_saveCancel.Visible = naming;
		}
		if (_saveConfirm is not null)
		{
			_saveConfirm.Visible = naming;
			_saveConfirm.SetText(L(_renaming ? "SM_APPLY" : "BP_SAVE_PLAN_CONFIRM"));
		}
	}

	private void ShowTools()
	{
		Editor.Plan = _tab == CurrentTab && !_renaming && !_saving ? Own : null;
		bool up = Editor.Plan is not null;
		if (_tools is null)
		{
			return;
		}
		_tools.Visible = up;
		int toolIndex = (int)Editor.Tool;
		if (_toolMarker is not null)
		{
			_toolMarker.Visible = toolIndex < _toolButtons.Count;
			if (_toolMarker.Visible)
			{
				_toolMarker.Position = _toolButtons[toolIndex].Position;
			}
		}
		if (_textPrompt is not null)
		{
			_textPrompt.Visible = up && Editor.Tool == EPlanTool.Text;
		}
		CPlanTool? tool = Editor.OptionsOf(Editor.Tool);
		if (_options is null)
		{
			return;
		}
		_options.Visible = up && tool is not null;
		if (tool is null)
		{
			return;
		}
		for (int index = 0; index < _optionButtons.Count; index++)
		{
			CGuiButton button = _optionButtons[index];
			bool shown = index < tool.Options.Count;
			button.Visible = shown;
			if (shown)
			{
				// Setting a picture puts a button back where its type says, so the grid's place goes on after it.
				if (!ReferenceEquals(tool, _optionsDrawn))
				{
					button.SetSprite(tool.Icon);
					button.Position = OptionPlace(index, _options.Size.X);
				}
				button.SetFrame(index + 1);
			}
		}
		_optionsDrawn = tool;
		int chosen = Editor.OptionOf(Editor.Tool);
		if (_optionMarker is not null)
		{
			_optionMarker.Visible = chosen < tool.Options.Count;
			if (_optionMarker.Visible)
			{
				_optionMarker.Position = _optionButtons[chosen].Position;
			}
		}
	}
}
