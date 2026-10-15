using System;
using Godot;

namespace OpenHOI3.Debug;

/// <summary>
/// Ours: a small menu of developer switches, drawn with the engine's own controls and not from the original's interface
/// files. Shift and 4 opens and shuts it, beside the original's console on Shift and 3 - the maintainer's choice,
/// 2026-10-15. Each switch is a box that is ticked or not.
/// </summary>
public sealed partial class CDebugMenu : PanelContainer
{
	private VBoxContainer _rows = null!;

	public override void _Ready()
	{
		Name = "DebugMenu";
		Position = new Vector2(1600, 100);
		Visible = false;
		var column = new VBoxContainer();
		AddChild(column);
		var title = new Label { Text = "Debug  (Shift+4)" };
		column.AddChild(title);
		_rows = new VBoxContainer();
		column.AddChild(_rows);
	}

	/// <summary>Opens it where it is shut and shuts it where it is open.</summary>
	public void Toggle()
	{
		Visible = !Visible;
	}

	/// <summary>A switch, ticked or not to begin with; <paramref name="changed"/> is told each time it is turned.</summary>
	public void AddSwitch(string label, bool on, Action<bool> changed)
	{
		var box = new CheckBox { Text = label, ButtonPressed = on };
		box.Toggled += pressed => changed(pressed);
		_rows.AddChild(box);
	}

	/// <summary>Whether a key is the menu's: the key 4, wherever the layout puts it, with Shift.</summary>
	public static bool IsMenuKey(InputEventKey key)
	{
		return key.ShiftPressed && key.PhysicalKeycode == Key.Key4;
	}
}
