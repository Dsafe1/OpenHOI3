using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The status of a selected unit: the <c>sup_unit_status</c> window, with where it is,
/// where it is supplied from, its speed, its supply and fuel, its width and weight, and
/// the small marks for what holds for it now. See <c>docs/mechanics/unitpanel.md</c>,
/// section 4.
/// </summary>
public sealed class CUnitStatusEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _unit;

	private readonly CGuiTextBox? _location;
	private readonly CGuiTextBox? _attrition;
	private readonly CGuiTextBox? _base;
	private readonly CGuiTextBox? _speed;
	private readonly CGuiTextBox? _weight;
	private readonly CGuiIcon? _width;
	private readonly CGuiIcon? _supply;
	private readonly CGuiIcon? _fuel;
	private readonly CGuiIcon? _moving;
	private readonly CGuiIcon? _distance;
	private readonly CGuiIcon? _combinedArms;
	private readonly CGuiIcon? _dugIn;
	private readonly CGuiIcon? _delay;

	/// <summary>Raised when the button that shows where the unit is, is pressed.</summary>
	public event Action? LocationPressed;

	private CUnitStatusEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		_unit = unit;

		_location = window.FindTextBox("unitlocation");
		_attrition = window.FindTextBox("unitattrition");
		_base = window.FindTextBox("unitbase");
		_speed = window.FindTextBox("speed");
		_weight = window.FindTextBox("unit_weight");
		_width = window.FindIcon("unit_cw_strip");
		_supply = window.FindIcon("supply_status");
		_fuel = window.FindIcon("fuel_status");
		_moving = window.FindIcon("unitstatus_moving");
		_distance = window.FindIcon("unitstatus_distance");

		_combinedArms = window.FindIcon("unitstatus_combinedarms");
		_dugIn = window.FindIcon("unitstatus_dugin");
		_delay = window.FindIcon("unitstatus_delay");

		// A mark for what is not built: no nuclear order.
		Show(window.FindIcon("unitstatus_nuke"), false);
		if (window.FindButton("unit_location_button") is { } button)
		{
			button.Pressed += () => LocationPressed?.Invoke();
		}
		Update();
	}

	public static CUnitStatusEntry? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_unit_status");
		return window is null ? null : new CUnitStatusEntry(window, context, state, owner, unit);
	}

	public override void Update()
	{
		Func<string, string> localise = _context.Localisation.Localise;
		bool isLand = CMovementSpeed.IsLand(_unit);

		Set(_location, CUnitPanelText.Location(_state, _unit));
		Set(_attrition, CUnitPanelText.Attrition(_unit));
		Set(_base, CUnitPanelText.SuppliedFrom(localise, _state, _owner, _unit));
		Set(_speed, CUnitPanelText.Speed(localise, _state, _owner, _unit));
		Set(_weight, CUnitPanelText.Weight(_unit));

		Show(_width, isLand);
		if (isLand)
		{
			_width?.SetFrame(CUnitPanelText.WidthFrame(CUnitPanelText.Width(_unit)));
		}
		_supply?.SetProgress(CUnitPanelText.SupplyBar(_state, _owner, _unit, EGoods.Supplies));
		_fuel?.SetProgress(CUnitPanelText.SupplyBar(_state, _owner, _unit, EGoods.Fuel));

		Show(_moving, _unit.IsMoving);
		Show(_combinedArms, CUnitPanelText.HasCombinedArms(_state, _unit));
		Show(_dugIn, CUnitPanelText.IsDugIn(_unit));
		Show(_delay, CUnitPanelText.IsDelayed(_unit));
		// The second frame is for a unit out of its headquarters' radio range.
		Show(_distance, isLand);
		if (isLand)
		{
			_distance?.SetFrame(CUnitPanelText.DistanceFrame(_state, _unit));
		}
	}
}
