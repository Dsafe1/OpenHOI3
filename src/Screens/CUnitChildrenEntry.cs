using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The units directly under a selected one: the <c>sup_children</c> window, with how
/// many there are of each level, the men of all of them, and a list of one row each.
/// Like the brigades' it is as high as its rows. See
/// <c>docs/mechanics/unitpanel.md</c>, section 8.
/// </summary>
public sealed class CUnitChildrenEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _unit;

	private readonly CGuiTextBox? _header;
	private readonly CGuiTextBox? _men;
	private readonly CGuiListBox? _list;
	private readonly int _label;
	private readonly int _fileHeight;
	private readonly List<CUnitChildRow> _rows = new();

	/// <summary>Raised with the unit of a row that was clicked.</summary>
	public event Action<CUnit>? Selected;

	/// <summary>Raised by the ribbon, <c>select_all_childunits</c>.</summary>
	public event Action? SelectAllPressed;

	/// <summary>Raised with the unit of a row whose button went down: a drag of it may follow.</summary>
	public event Action<CUnit>? DragStarted;

	private CUnitChildrenEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CUnit unit)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		_unit = unit;

		_header = window.FindTextBox("header");
		_men = window.FindTextBox("str");
		_list = window.FindListBox("list");
		_label = (int)(window.FindIcon("children_label_bg")?.Size.Y ?? 0f);
		_fileHeight = (int)window.Size.Y;
		// The ribbon: every land unit under this one joins the selection, this one staying - a land unit's only
		// (selection.md, section 5.3).
		if (window.FindButton("select_all_childunits") is { } all)
		{
			all.Disabled = !CMovementRules.IsLandUnit(unit);
			all.Pressed += () => SelectAllPressed?.Invoke();
		}
		Fill();
	}

	public static CUnitChildrenEntry? Create(CGuiContext context, CGameState state, CCountry owner, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_children");
		return window is null ? null : new CUnitChildrenEntry(window, context, state, owner, unit);
	}

	public override void Update()
	{
		Resized = false;
		if (!_rows.Select(row => row.Unit).SequenceEqual(CUnitPanelText.Children(_unit)))
		{
			Fill();
			return;
		}
		Set(_men, CUnitPanelText.ChildrenMen(_unit));
		foreach (CUnitChildRow row in _rows)
		{
			row.Update();
		}
	}

	private void Fill()
	{
		Set(_header, CUnitPanelText.ChildrenHeader(_context.Localisation.Localise, _unit));
		Set(_men, CUnitPanelText.ChildrenMen(_unit));
		_list?.Clear();
		_rows.Clear();
		int rowHeight = 0;
		foreach (CUnit child in CUnitPanelText.Children(_unit))
		{
			if (CUnitChildRow.Create(_context, _state, _owner, child) is not { } row)
			{
				continue;
			}
			row.Selected += () => Selected?.Invoke(child);
			row.DragStarted += () => DragStarted?.Invoke(child);
			_rows.Add(row);
			_list?.Add(row.Window);
			rowHeight = (int)row.Window.Size.Y;
		}
		Resized = SetHeight(CUnitPanelText.RowsHeight(_label, _rows.Count, rowHeight, _fileHeight));
	}
}
