using System.Linq;
using System;
using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// What a fleet or an air unit carries: the <c>sup_loaded</c> window on every fleet's and air unit's panel, with what it
/// can carry, a row for each passenger and, for a fleet, each air unit aboard its carriers, and the two buttons that
/// select all of either. **A fleet's list is made when the panel is and not again while it stays selected** - a unit put
/// ashore stays in it - and an air unit's is made again when its count changes, as the original's. See
/// <c>docs/mechanics/transport.md</c>, section 12.
/// </summary>
public sealed class CLoadedUnitsEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _carrier;

	private readonly CGuiTextBox? _header;
	private readonly CGuiListBox? _list;
	private readonly CGuiButton? _selectLand;
	private readonly CGuiButton? _selectAir;
	private readonly List<CLoadedUnitRow> _rows = new();
	private readonly int _label;
	private readonly int _fileHeight;
	private int _count;
	private List<CUnit> _listed = new();

	/// <summary>Raised with the unit of a row that was clicked.</summary>
	public event Action<CUnit>? Selected;

	/// <summary>Raised with a change of the selection, by <c>select_land</c> and <c>select_air</c>.</summary>
	public event Action<Action<CSelection>>? SelectionChanging;

	private CLoadedUnitsEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit carrier)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		_carrier = carrier;

		_header = window.FindTextBox("header");
		_list = window.FindListBox("list");
		// As high as its rows, as the brigades' and the children's are: what is above the list, and the rows.
		_label = (int)(_list?.Position.Y ?? 0f);
		_fileHeight = (int)window.Size.Y;
		// The two buttons select every passenger, or every aircraft aboard the carriers, in place of the fleet; an air
		// unit's select_land may be pressed and does nothing, as the original's (transport.md, section 12).
		_selectLand = window.FindButton("select_land");
		_selectAir = window.FindButton("select_air");
		if (_selectLand is not null)
		{
			_selectLand.Pressed += () => SelectOnly(CTransportText.SelectedByLand(_carrier));
		}
		if (_selectAir is not null)
		{
			_selectAir.Pressed += () => SelectOnly(CTransportText.SelectedByAir(_carrier));
		}
		AnswerFor(name => name switch
		{
			"select_land" => new CTooltip { Text = context.Localisation.Localise("UW_SELECT_LAND") },
			"select_air" => new CTooltip { Text = context.Localisation.Localise("UW_SELECT_AIR") },
			_ => CTooltip.Empty,
		}, "select_land", "select_air");
		Fill();
	}

	public static CLoadedUnitsEntry? Create(CGuiContext context, CGameState state, CCountry owner, CUnit carrier)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_loaded");
		return window is null ? null : new CLoadedUnitsEntry(window, context, state, owner, carrier);
	}

	public override void Update()
	{
		Resized = false;
		// Only an air unit's list is made again, when its count changes.
		// A fleet's stays as it was made, a unit put ashore with it - the original's; with the fix it is made again
		// whenever what is aboard is another list (transport.md, "What looks wrong", 2).
		if (_carrier.Kind == EUnitKind.Air && _carrier.Passengers.Count != _count
			|| _state.Rules.Has(Sim.Rules.EFix.FreshScreens) && !_carrier.Passengers.SequenceEqual(_listed))
		{
			Fill();
		}
		Set(_header, CTransportText.Header(_context.Localisation.Localise, _carrier));
		foreach (CLoadedUnitRow row in _rows)
		{
			row.Update();
		}
		if (_selectLand is not null)
		{
			_selectLand.Disabled = !CTransportText.MaySelectLand(_carrier);
		}
		if (_selectAir is not null)
		{
			_selectAir.Visible = CTransportText.ShowsSelectAir(_carrier);
			_selectAir.Disabled = !CTransportText.MaySelectAir(_carrier);
		}
	}

	/// <summary>A press: the units in place of everything selected, or nothing where the press does nothing.</summary>
	private void SelectOnly(List<CUnit>? units)
	{
		if (units is not null)
		{
			SelectionChanging?.Invoke(selection => CTransportText.SelectOnly(selection, units));
		}
	}

	private void Fill()
	{
		_count = _carrier.Passengers.Count;
		_listed = _carrier.Passengers.ToList();
		Set(_header, CTransportText.Header(_context.Localisation.Localise, _carrier));
		_list?.Clear();
		_rows.Clear();
		int rowHeight = 0;
		foreach (CUnit unit in CTransportText.Rows(_carrier))
		{
			if (CLoadedUnitRow.Create(_context, _state, _owner, unit) is not { } row)
			{
				continue;
			}
			row.Selected += () => Selected?.Invoke(unit);
			_rows.Add(row);
			_list?.Add(row.Window);
			rowHeight = (int)row.Window.Size.Y;
		}
		Resized = SetHeight(CUnitPanelText.RowsHeight(_label, _rows.Count, rowHeight, _fileHeight));
	}
}
