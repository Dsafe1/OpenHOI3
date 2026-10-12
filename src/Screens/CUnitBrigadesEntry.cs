using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The brigades of a selected unit: the <c>sup_subunits</c> window, a heading and a
/// list of one row each. The window is as high as its rows, up to the height the file
/// gives it; past that its list scrolls. See <c>docs/mechanics/unitpanel.md</c>,
/// section 6.
/// </summary>
public sealed class CUnitBrigadesEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _unit;

	private readonly CGuiTextBox? _header;
	private readonly CGuiListBox? _list;
	private readonly int _label;
	private readonly int _fileHeight;
	private readonly List<CUnitBrigadeRow> _rows = new();

	private CUnitBrigadesEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		_unit = unit;

		_header = window.FindTextBox("header");
		_list = window.FindListBox("list");
		_label = (int)(window.FindIcon("bg_label_brigade")?.Size.Y ?? 0f);
		_fileHeight = (int)window.Size.Y;
		// Only a fleet says anything here: how its ships get in each other's way.
		Set(window.FindTextBox("positioning"), "");
		// Before the rows are made: each row answers for its own elements.
		AnswerFor(name => CBrigadeTooltips.Window(context.Localisation.Localise, state, unit, name), "positioning");
		Fill();
	}

	public static CUnitBrigadesEntry? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_subunits");
		return window is null ? null : new CUnitBrigadesEntry(window, context, state, owner, unit);
	}

	public override void Update()
	{
		Resized = false;
		// Filled again when the unit's brigades are no longer the rows'.
		if (!_rows.Select(row => row.SubUnit).SequenceEqual(_unit.SubUnits))
		{
			Fill();
			return;
		}
		Set(_header, CUnitPanelText.BrigadesHeader(_context.Localisation.Localise, _unit));
		foreach (CUnitBrigadeRow row in _rows)
		{
			row.Update();
		}
	}

	private void Fill()
	{
		Set(_header, CUnitPanelText.BrigadesHeader(_context.Localisation.Localise, _unit));
		_list?.Clear();
		_rows.Clear();
		int rowHeight = 0;
		foreach (CSubUnit subUnit in _unit.SubUnits)
		{
			if (CUnitBrigadeRow.Create(_context, _state, _owner, subUnit) is not { } row)
			{
				continue;
			}
			_rows.Add(row);
			_list?.Add(row.Window);
			rowHeight = (int)row.Window.Size.Y;
		}
		Resized = SetHeight(CUnitPanelText.RowsHeight(_label, _rows.Count, rowHeight, _fileHeight));
	}
}
