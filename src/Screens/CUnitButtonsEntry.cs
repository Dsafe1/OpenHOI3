using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The strip of buttons of a selected unit: the <c>sup_buttons_window</c> window. Its
/// three checkboxes - come first, be reinforced, be upgraded - are the unit's own and
/// work; the buttons that make a headquarters, attach, detach, load, disband and
/// reorganise are shown as the original shows them and do nothing yet. See
/// <c>docs/mechanics/unitpanel.md</c>, section 7.
/// </summary>
public sealed class CUnitButtonsEntry : CUnitPanelEntry
{
	private readonly CUnit _unit;
	private readonly CGuiButton? _priority;
	private readonly CGuiButton? _reinforce;
	private readonly CGuiButton? _upgrade;
	private readonly CGuiButton? _attach;
	private readonly CGuiButton? _detach;

	/// <summary>Raised when one of the three checkboxes is clicked, with what it now says.</summary>
	public event Action<EUnitSupport, bool>? SupportChanged;

	private CUnitButtonsEntry(CGuiWindow window, CUnit unit, bool mayOrder) : base(window)
	{
		_unit = unit;
		_priority = Checkbox(window, "prio_checkbox", EUnitSupport.Priority, mayOrder);
		_reinforce = Checkbox(window, "reinf_checkbox", EUnitSupport.Reinforcement, mayOrder);
		_upgrade = Checkbox(window, "upgrade_checkbox", EUnitSupport.Upgrade, mayOrder);
		_attach = window.FindButton("attach_button");
		_detach = window.FindButton("detach_button");

		// One of the five by the unit's level, and that one not to be pressed: making a
		// headquarters is not built.
		string level = CUnitPanelText.LevelButton(unit);
		foreach (string name in CUnitPanelText.LevelButtons)
		{
			if (window.FindButton(name) is { } button)
			{
				button.Visible = name == level;
				button.Disabled = true;
			}
		}

		// A land unit that is aboard nothing may be loaded and not unloaded; nothing
		// carries a unit yet. The original's rule for the two aeroplane buttons was not
		// read, and they are shown the same way.
		Unbuilt(window, "load_button", shown: true);
		Unbuilt(window, "unload_button", shown: false);
		Unbuilt(window, "load_plane_button", shown: true);
		Unbuilt(window, "unload_plane_button", shown: false);
		Unbuilt(window, "disbandbutton", shown: true);
		Unbuilt(window, "newunitbutton", shown: true);
		if (_attach is not null)
		{
			_attach.Disabled = true;
		}
		if (_detach is not null)
		{
			_detach.Disabled = true;
		}
		Update();
	}

	public static CUnitButtonsEntry? Create(CGuiContext context, CUnit unit, bool mayOrder)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_buttons_window");
		return window is null ? null : new CUnitButtonsEntry(window, unit, mayOrder);
	}

	public override void Update()
	{
		Tick(_priority, _unit.IsPrioritised);
		Tick(_reinforce, _unit.MayReinforce);
		Tick(_upgrade, _unit.MayUpgrade);

		// A unit with none above it may be attached, and one with may be detached.
		Show(_attach, _unit.Parent is null);
		Show(_detach, _unit.Parent is not null);
	}

	private CGuiButton? Checkbox(CGuiWindow window, string name, EUnitSupport what, bool mayOrder)
	{
		CGuiButton? box = window.FindButton(name);
		if (box is not null)
		{
			box.Disabled = !mayOrder;
			box.Pressed += () => SupportChanged?.Invoke(what, box.Checked);
		}
		return box;
	}

	/// <summary>The box is set from the unit every frame, as the original sets it.</summary>
	private static void Tick(CGuiButton? box, bool on)
	{
		if (box is not null && box.Checked != on && !box.IsDown)
		{
			box.Checked = on;
		}
	}

	private static void Unbuilt(CGuiWindow window, string name, bool shown)
	{
		if (window.FindButton(name) is { } button)
		{
			button.Visible = shown;
			button.Disabled = true;
		}
	}
}
