using System;
using System.Collections.Generic;
using OpenHOI3.Gui;
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

	/// <summary>Raised with the unit of a row that was clicked.</summary>
	public event Action<CUnit>? Selected;

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
		// The two buttons select every passenger or every aircraft aboard; selecting several
		// units at once is not built, so neither may be pressed.
		_selectLand = window.FindButton("select_land");
		_selectAir = window.FindButton("select_air");
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
		if (_carrier.Kind == EUnitKind.Air && _carrier.Passengers.Count != _count)
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
			_selectLand.Disabled = true;
		}
		if (_selectAir is not null)
		{
			_selectAir.Visible = _carrier.Kind == EUnitKind.Navy;
			_selectAir.Disabled = true;
		}
	}

	private void Fill()
	{
		_count = _carrier.Passengers.Count;
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
