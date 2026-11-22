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
/// gives it - twice that for a fleet with little aboard - and makes its list as high; past that the list scrolls. See <c>docs/mechanics/unitpanel.md</c>,
/// section 6.
/// </summary>
public sealed class CUnitBrigadesEntry : CUnitPanelEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _owner;
	private readonly CUnit _unit;
	private readonly CCountry? _player;

	private readonly CGuiTextBox? _header;
	private readonly CGuiListBox? _list;
	private readonly int _label;
	private readonly int _fileHeight;
	private int _rowHeight;
	private readonly List<CUnitBrigadeRow> _rows = new();

	/// <summary>The ribbon under the mouse where it opens the whole unit's card: brighter by a third.</summary>
	private static readonly Godot.Color RibbonLit = new(1.35f, 1.35f, 1.35f);

	/// <summary>A brigade's row was clicked.</summary>
	public event System.Action<CSubUnit>? BrigadeChosen;

	/// <summary>The left button went down on a brigade's row.</summary>
	public event System.Action<CSubUnit>? BrigadeDragStarted;

	/// <summary>The ribbon over the brigades was clicked, in an enhanced game: the whole unit's card is wanted.</summary>
	public event System.Action<CUnit>? UnitCardWanted;

	/// <summary>A brigade's upgrade button was pressed.</summary>
	public event System.Action<CUnitBrigadeRow>? UpgradeWanted;

	private CUnitBrigadesEntry(CGuiWindow window, CGuiContext context, CGameState state, CCountry owner, CCountry? player, CUnit unit)
		: base(window)
	{
		_context = context;
		_state = state;
		_owner = owner;
		_player = player;
		_unit = unit;

		_header = window.FindTextBox("header");
		_list = window.FindListBox("list");
		_label = (int)(window.FindIcon("bg_label_brigade")?.Size.Y ?? 0f);
		_fileHeight = (int)window.Size.Y;
		// Only a fleet says anything here: how its ships get in each other's way.
		Set(window.FindTextBox("positioning"), "");
		// Before the rows are made: each row answers for its own elements.
		AnswerFor(name => CBrigadeTooltips.Window(context.Localisation.Localise, state, unit, name), "positioning");
		// An enhanced game's: the ribbon opens the card of the whole unit (brigadecard.md, section 7). Its tooltip is
		// set after the entry's own answers, which would otherwise answer for it with nothing; and it brightens under
		// the mouse, as a button does, to say it may be pressed.
		if (state.Rules.Enhanced && CUnitCardText.HasCard(unit) && window.FindIcon("bg_label_brigade") is { } ribbon)
		{
			ribbon.Clicked += () => UnitCardWanted?.Invoke(unit);
			ribbon.Tooltip = () => new CTooltip { Text = context.Localisation.Localise(CUnitCardText.RibbonTip) };
			ribbon.MouseEntered += () => ribbon.Modulate = RibbonLit;
			ribbon.MouseExited += () => ribbon.Modulate = Godot.Colors.White;
		}
		Fill();
	}

	public static CUnitBrigadesEntry? Create(CGuiContext context, CGameState state, CCountry owner, CCountry? player, CUnit unit)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "sup_subunits");
		return window is null ? null : new CUnitBrigadesEntry(window, context, state, owner, player, unit);
	}

	public override void Update()
	{
		Resized = false;
		// Filled again when the unit's brigades are no longer the rows'.
		if (!_rows.Where(row => row.Line is not null).Select(row => row.Line).SequenceEqual(_unit.UpgradeLines)
			|| !_rows.Where(row => row.SubUnit is not null).Select(row => row.SubUnit).SequenceEqual(_unit.SubUnits))
		{
			Fill();
			return;
		}
		Set(_header, CUnitPanelText.BrigadesHeader(_context.Localisation.Localise, _unit));
		foreach (CUnitBrigadeRow row in _rows)
		{
			row.Update();
		}
		// The room changes as a fleet takes units aboard or puts them off.
		Resized = Resize();
	}

	/// <summary>
	/// The window as high as its label and its rows within the room it has, and its list made as high as those rows -
	/// which is what says how many the list shows before it scrolls, not the height the file gives the list.
	/// </summary>
	private bool Resize()
	{
		bool isFleet = _unit.Kind == EUnitKind.Navy;
		int room = CUnitPanelText.BrigadesRoom(_label, _fileHeight, isFleet, isFleet ? CTransportText.Rows(_unit).Count : 0);
		int height = CUnitPanelText.BrigadesListHeight(_rows.Count, _rowHeight, room);
		// The original leaves the list as it is where there is no row.
		if (_rows.Count > 0)
		{
			_list?.SetHeight(height);
		}
		return SetHeight(_label + height);
	}

	private void Fill()
	{
		Set(_header, CUnitPanelText.BrigadesHeader(_context.Localisation.Localise, _unit));
		_list?.Clear();
		_rows.Clear();
		int rowHeight = 0;
		foreach (Sim.Construction.CConstruction line in _unit.UpgradeLines)
		{
			if (CUnitBrigadeRow.CreateForLine(_context, _state, _owner, line) is { } row)
			{
				_rows.Add(row);
				_list?.Add(row.Window);
				rowHeight = (int)row.Window.Size.Y;
			}
		}
		foreach (CSubUnit subUnit in _unit.SubUnits)
		{
			if (CUnitBrigadeRow.Create(_context, _state, _owner, _player, subUnit) is not { } row)
			{
				continue;
			}
			row.Chosen += chosen => BrigadeChosen?.Invoke(chosen);
			row.DragStarted += dragged => BrigadeDragStarted?.Invoke(dragged);
			row.UpgradeWanted += wanted => UpgradeWanted?.Invoke(wanted);
			_rows.Add(row);
			_list?.Add(row.Window);
			rowHeight = (int)row.Window.Size.Y;
		}
		_rowHeight = rowHeight;
		Resized = Resize();
	}
}
