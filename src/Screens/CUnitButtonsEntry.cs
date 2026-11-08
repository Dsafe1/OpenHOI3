using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The strip of buttons of a selected unit: the <c>sup_buttons_window</c> window. Its
/// three checkboxes - come first, be reinforced, be upgraded - are the unit's own and
/// work, and so do the load and unload buttons (<c>docs/mechanics/transport.md</c>,
/// section 12), and the new-unit button; the buttons that make a headquarters, attach, detach
/// and disband are shown as the original shows them and do nothing yet. See
/// <c>docs/mechanics/unitpanel.md</c>, section 7.
/// </summary>
public sealed class CUnitButtonsEntry : CUnitPanelEntry
{
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _unit;
	private readonly bool _mayOrder;
	private readonly CGuiButton? _priority;
	private readonly CGuiButton? _reinforce;
	private readonly CGuiButton? _upgrade;
	private readonly CGuiButton? _attach;
	private readonly CGuiButton? _detach;
	private readonly CGuiButton? _load;
	private readonly CGuiButton? _unload;
	private readonly CGuiButton? _loadPlane;
	private readonly CGuiButton? _unloadPlane;
	private readonly CGuiButton? _newUnit;
	private readonly CGuiButton? _create;
	private CCarryButtons _carry = CCarryButtons.None;

	/// <summary>Raised when the attach button is pressed.</summary>
	public event Action? AttachPressed;

	/// <summary>Raised when the detach button is pressed.</summary>
	public event Action? DetachPressed;

	/// <summary>Raised when the button that makes a headquarters over the unit is pressed.</summary>
	public event Action? CreateHigherPressed;

	/// <summary>Raised when the new-unit button is pressed, with whether Ctrl was held - a split - or not.</summary>
	public event Action<bool>? NewUnitPressed;

	/// <summary>Raised when one of the three checkboxes is clicked, with what it now says.</summary>
	public event Action<EUnitSupport, bool>? SupportChanged;

	/// <summary>Raised when the load button is pressed, with the number of the fleet it found.</summary>
	public event Action<int>? LoadPressed;

	/// <summary>Raised when the unload button is pressed.</summary>
	public event Action? UnloadPressed;

	/// <summary>Raised when the planes' load button is pressed, with the number of the air unit it found.</summary>
	public event Action<int>? PlaneLoadPressed;

	/// <summary>Raised when the planes' unload button is pressed.</summary>
	public event Action? PlaneUnloadPressed;

	private CUnitButtonsEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit,
		bool mayOrder) : base(window)
	{
		_state = state;
		_owner = owner;
		_unit = unit;
		_mayOrder = mayOrder;
		_priority = Checkbox(window, "prio_checkbox", EUnitSupport.Priority, mayOrder);
		_reinforce = Checkbox(window, "reinf_checkbox", EUnitSupport.Reinforcement, mayOrder);
		_upgrade = Checkbox(window, "upgrade_checkbox", EUnitSupport.Upgrade, mayOrder);
		_attach = window.FindButton("attach_button");
		_detach = window.FindButton("detach_button");

		// One of the five by the unit's level; a theatre's edit_theatre is not built (attaching.md, section 1).
		string level = CUnitPanelText.LevelButton(unit);
		foreach (string name in CUnitPanelText.LevelButtons)
		{
			if (window.FindButton(name) is not { } button)
			{
				continue;
			}
			button.Visible = name == level;
			button.Disabled = true;
			if (name == level && name != "edit_theatre")
			{
				_create = button;
				button.Pressed += () => CreateHigherPressed?.Invoke();
			}
		}

		// The load and unload buttons, the ships' and the planes', are set from the unit every frame
		// (transport.md, sections 12 and 13.6).
		_load = window.FindButton("load_button");
		_unload = window.FindButton("unload_button");
		_loadPlane = window.FindButton("load_plane_button");
		_unloadPlane = window.FindButton("unload_plane_button");
		if (_load is not null)
		{
			_load.Pressed += () => LoadPressed?.Invoke(_carry.Fleet);
		}
		if (_unload is not null)
		{
			_unload.Pressed += () => UnloadPressed?.Invoke();
		}
		if (_loadPlane is not null)
		{
			_loadPlane.Pressed += () => PlaneLoadPressed?.Invoke(_carry.Plane);
		}
		if (_unloadPlane is not null)
		{
			_unloadPlane.Pressed += () => PlaneUnloadPressed?.Invoke();
		}
		Unbuilt(window, "disbandbutton", shown: true);
		// A new empty unit beside this one, or with Ctrl the unit split, greyed by the original's rule (unitpanel.md, 7.1).
		_newUnit = window.FindButton("newunitbutton");
		if (_newUnit is not null)
		{
			_newUnit.Pressed += () => NewUnitPressed?.Invoke(Godot.Input.IsKeyPressed(Godot.Key.Ctrl));
		}
		if (_attach is not null)
		{
			_attach.Pressed += () => AttachPressed?.Invoke();
		}
		if (_detach is not null)
		{
			_detach.Pressed += () => DetachPressed?.Invoke();
		}
		AnswerFor(name => CUnitOrderTooltips.Buttons(context.Localisation.Localise, unit, _attach is { Disabled: false }, name,
			_carry));
		Update();
	}

	public static CUnitButtonsEntry? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit, bool mayOrder)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_buttons_window");
		return window is null ? null : new CUnitButtonsEntry(window, context, state, owner, unit, mayOrder);
	}

	public override void Update()
	{
		Tick(_priority, _unit.IsPrioritised);
		Tick(_reinforce, _unit.MayReinforce);
		Tick(_upgrade, _unit.MayUpgrade);

		// One of each pair shows: aboard, unload - in port, or at the air unit's base; aboard nothing, load - where a
		// fleet or an air unit is found.
		_carry = CTransportText.Buttons(_state, _owner, _unit);
		Carry(_load, _carry.LoadShown, _carry.LoadEnabled);
		Carry(_unload, _carry.UnloadShown, _carry.UnloadEnabled);
		Carry(_loadPlane, _carry.PlaneLoadShown, _carry.PlaneLoadEnabled);
		Carry(_unloadPlane, _carry.PlaneUnloadShown, _carry.PlaneUnloadEnabled);

		// A unit with none above it may be attached, and one with may be detached (attaching.md, section 1).
		Show(_attach, _unit.Parent is null);
		Show(_detach, CAttachText.ShowsDetach(_unit));
		if (_attach is not null)
		{
			_attach.Disabled = !_mayOrder || !CAttachText.MayAttach(_owner, _unit);
		}
		if (_detach is not null)
		{
			_detach.Disabled = !_mayOrder;
		}
		if (_create is not null)
		{
			_create.Disabled = !_mayOrder || !CAttachText.MayCreateHigher(_state, _owner, _unit);
		}

		if (_newUnit is not null)
		{
			_newUnit.Disabled = !_mayOrder || !CUnitSplit.MayPress(_unit);
		}
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

	private void Carry(CGuiButton? button, bool shown, bool enabled)
	{
		if (button is not null)
		{
			button.Visible = shown;
			button.Disabled = !enabled || !_mayOrder;
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
