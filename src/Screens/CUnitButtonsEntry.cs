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
/// section 12); the buttons that make a headquarters, attach, detach, load onto planes,
/// disband and reorganise are shown as the original shows them and do nothing yet. See
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
	private CCarryButtons _carry = CCarryButtons.None;

	/// <summary>Raised when one of the three checkboxes is clicked, with what it now says.</summary>
	public event Action<EUnitSupport, bool>? SupportChanged;

	/// <summary>Raised when the load button is pressed, with the number of the fleet it found.</summary>
	public event Action<int>? LoadPressed;

	/// <summary>Raised when the unload button is pressed.</summary>
	public event Action? UnloadPressed;

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

		// The load and unload buttons are set from the unit every frame; the planes' are
		// shown as the original shows them, and nothing carries by air yet.
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

		// One of each pair shows: aboard, unload - in port; aboard nothing, load - where a fleet is found.
		_carry = CTransportText.Buttons(_state, _owner, _unit);
		Carry(_load, _carry.LoadShown, _carry.LoadEnabled);
		Carry(_unload, _carry.UnloadShown, _carry.UnloadEnabled);
		Carry(_loadPlane, _carry.PlaneLoadShown, false);
		Carry(_unloadPlane, _carry.PlaneUnloadShown, false);

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
