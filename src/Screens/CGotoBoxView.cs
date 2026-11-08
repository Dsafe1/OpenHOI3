using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Screens;

/// <summary>
/// The go-to box beside the minimap, <c>goto_box</c>: a province's name typed, or its number, and the map moved there. The list is made again as the text changes; a click on a row goes at once. Nothing is selected,
/// and the game goes on. See <c>docs/mechanics/convoywindows.md</c>, section 8.
/// </summary>
public sealed partial class CGotoBoxView : Control
{
	private CGuiContext _context = null!;
	private CGameState _state = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private CGuiTextBox? _edit;
	private CGuiButton? _goTo;
	private string _shown = "";

	public void Setup(CGuiContext context, CGameState state)
	{
		_context = context;
		_state = state;
	}

	/// <summary>The map shown at this province.</summary>
	public event Action<int>? ProvinceChosen;

	public bool IsOpen => _window is { Visible: true };

	public override void _Ready()
	{
		Name = "GotoBox";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, CGotoBoxText.WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("provinces");
		_edit = _window.FindTextBox("goto_edit");
		_edit?.MakeEditable();
		if (_edit is not null)
		{
			_edit.Entered += () => Go(exact: false);
		}
		_goTo = _window.TryFindButton("goto");
		if (_goTo is not null)
		{
			_goTo.Pressed += () => Go(exact: false);
		}
		if (_window.TryFindButton("cancel") is { } cancel)
		{
			cancel.Pressed += () => _window.Visible = false;
		}
		_window.Visible = false;
	}

	public void Show()
	{
		if (_window is null)
		{
			return;
		}
		_window.Visible = true;
		_edit?.TakeKeyboard();
		ShowList();
	}

	private string Typed => _edit?.Editor?.Text ?? "";

	public override void _Process(double delta)
	{
		if (IsOpen && Typed != _shown)
		{
			ShowList();
		}
	}

	public override void _Input(InputEvent input)
	{
		if (IsOpen && input is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter })
		{
			Go(exact: false);
			GetViewport().SetInputAsHandled();
		}
	}

	private void ShowList()
	{
		if (_list is null)
		{
			return;
		}
		_shown = Typed;
		_list.Clear();
		(var rows, bool mayGo) = CGotoBoxText.Filter(_state, _shown);
		foreach (CProvince province in rows)
		{
			if (CGuiWindow.CreateEntry(_context, CGotoBoxText.RowName) is not { } row || row.TryFindButton("game") is not { } button)
			{
				continue;
			}
			int id = province.Id;
			button.SetText(province.Name);
			button.Pressed += () => Leave(id);
			_list.Add(row);
		}
		if (_goTo is not null)
		{
			_goTo.Disabled = !mayGo;
		}
	}

	private void Go(bool exact)
	{
		int province = CGotoBoxText.Target(_state, Typed, exact);
		if (province != 0)
		{
			Leave(province);
		}
		else
		{
			Leave(0);
		}
	}

	/// <summary>The map moved, and the box emptied and put away.</summary>
	private void Leave(int province)
	{
		if (province != 0)
		{
			ProvinceChosen?.Invoke(province);
		}
		if (_edit is not null)
		{
			_edit.Text = "";
			if (_edit.Editor is { } editor)
			{
				editor.Text = "";
			}
		}
		_shown = "";
		_list?.Clear();
		if (_window is not null)
		{
			_window.Visible = false;
		}
	}
}
