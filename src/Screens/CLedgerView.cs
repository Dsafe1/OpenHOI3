using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Ledger;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The Statistics screen, <c>ledger</c>: 26 pages of tables and pies made from the world when each is shown. It pauses a game for as long as it is up, and opens on
/// the page it was last left on. The file's graph section is drawn by nothing, in the original as here. See <c>docs/mechanics/statistics.md</c>.
/// </summary>
public sealed partial class CLedgerView : Control
{
	private const string WindowName = "ledger";
	private const string TopColourName = "topcolor_statistics";
	private const string SkinFolder = "gfx/interface/skins/country_bg_";

	/// <summary>Where the table starts in the window, the header's row and the rows' first, 30 apart, as the original's rectangles have them.</summary>
	private const float TableLeft = 66f;
	private const float TableTop = 139f;
	private const float RowsTop = 35f;
	private const float RowHeight = 30f;
	private const int VisibleRows = 16;
	private const float ScrollbarLeft = 1033f;
	private const float ScrollbarTop = 167f;

	private const float PieSize = 70f;

	private static readonly Vector2 PieOrigin = new(500f, 330f);
	private static readonly string[] PieOffsets = { "pie_one_offset", "pie_two_offset", "pie_three_offset", "pie_four_offset" };

	private readonly Dictionary<int, (int Column, bool Ascending)> _sorts = new();
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CCountry _player = null!;
	private CGuiWindow? _window;
	private CGuiWindow? _topColour;
	private CGuiTextBox? _header;
	private CGuiTextBox? _pageNumber;
	private CGuiIcon? _overlay;
	private CGuiScrollbar? _scrollbar;
	private Control? _content;
	private CLedgerPage? _shown;
	private int _page;
	private int _firstRow;
	private bool _pausedBefore;

	public void Setup(CGuiContext context, CSimulation simulation, CCountry player)
	{
		_context = context;
		_simulation = simulation;
		_player = player;
	}

	public bool IsOpen => _window is { Visible: true };

	/// <summary>Raised as it opens, which shuts any country screen up.</summary>
	public event Action? Opening;

	/// <summary>A leader's row chose the unit they command.</summary>
	public event Action<CUnit>? UnitChosen;

	public override void _Ready()
	{
		Name = "Statistics";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	private bool _made;

	/// <summary>The windows made, once, as the screen first opens rather than as the game is entered (CCountryView.Make).</summary>
	private void Make()
	{
		if (_made)
		{
			return;
		}
		_made = true;
		_topColour = CGuiWindow.Create(_context, TopColourName);
		if (_topColour is not null)
		{
			// At its declared size and place, as a country screen's: its tile sprite spreads itself (CTileSprite).
			AddChild(_topColour);
			_topColour.Visible = false;
		}
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_header = _window.FindTextBox("header");
		_pageNumber = _window.FindTextBox("page_number");
		_overlay = _window.TryFindIcon("ledger_overlay");
		foreach (string hidden in new[] { "graph_background", "no_graph_data" })
		{
			if (_window.FindWindow(hidden) is { } dead)
			{
				dead.Visible = false;
			}
		}
		foreach (string hidden in new[] { "checkbox_autosend", "textbox_autosend" })
		{
			if (_window.TryFindElement(hidden) is { } dead)
			{
				dead.Visible = false;
			}
		}
		Bind("close_button", SetClosed);
		Bind("next_button", () => ShowPage(CLedgerPages.Step(_page, forward: true)));
		Bind("previous_button", () => ShowPage(CLedgerPages.Step(_page, forward: false)));
		Bind("ledger_overview", () => ShowPage(CLedgerPages.Overview));
		Bind("ledger_economy", () => ShowPage(CLedgerPages.Economy));
		Bind("ledger_misc", () => ShowPage(CLedgerPages.Misc));
		Bind("ledger_military", () => ShowPage(CLedgerPages.Military));
		_window.Visible = false;
	}

	private void Bind(string name, Action pressed)
	{
		if (_window?.TryFindButton(name) is { } button)
		{
			button.Pressed += pressed;
		}
	}

	public void Toggle()
	{
		if (IsOpen)
		{
			SetClosed();
		}
		else
		{
			SetOpen();
		}
	}

	/// <summary>Shut where it is up, as a country screen opening does.</summary>
	public void Shut()
	{
		if (IsOpen)
		{
			SetClosed();
		}
	}

	private void SetOpen()
	{
		Make();
		if (_window is null)
		{
			return;
		}
		Opening?.Invoke();
		_pausedBefore = _simulation.Clock.Paused;
		_simulation.Clock.Paused = true;
		ShowSkin();
		_window.Visible = true;
		if (_topColour is not null)
		{
			_topColour.Visible = true;
		}
		ShowPage(_page);
	}

	private void SetClosed()
	{
		if (_window is null)
		{
			return;
		}
		_window.Visible = false;
		if (_topColour is not null)
		{
			_topColour.Visible = false;
		}
		if (!_pausedBefore)
		{
			_simulation.Clock.Paused = false;
		}
	}

	/// <summary>The wheel over the window moves the table, and goes no further: the map under it stays where it is.</summary>
	public override void _Input(InputEvent input)
	{
		if (!IsOpen || _window is null || input is not InputEventMouseButton { Pressed: true } button
			|| button.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)
			|| !_window.GetGlobalRect().HasPoint(button.Position))
		{
			return;
		}
		Scroll(_firstRow + (button.ButtonIndex == MouseButton.WheelUp ? -1 : 1));
		GetViewport().SetInputAsHandled();
	}

	/// <summary>The window's picture is the country's own where the install has one.</summary>
	private void ShowSkin()
	{
		string file = SkinFolder + _player.Tag + ".dds";
		if (_window?.TryFindIcon("bg_countryskin") is { } icon && _context.Textures.Exists(file) && _context.Textures.LoadFromGamePath(file) is { } skin)
		{
			icon.ShowPicture(skin);
		}
	}

	private void ShowPage(int page)
	{
		_page = page;
		_firstRow = 0;
		_shown = new CLedgerPages(_simulation.State, _player, _context.Localisation.Localise).Build(page);
		if (_sorts.TryGetValue(page, out (int Column, bool Ascending) sort))
		{
			_shown.Sort(sort.Column, sort.Ascending);
		}
		if (_header is not null)
		{
			_header.Text = _shown.Title;
		}
		if (_pageNumber is not null)
		{
			_pageNumber.Text = (page + 1).ToString(CultureInfo.InvariantCulture);
		}
		if (_overlay is not null)
		{
			_overlay.Visible = _shown.Pies is null;
		}
		MakeScrollbar();
		Redraw();
	}

	private void Redraw()
	{
		_content?.QueueFree();
		_content = null;
		if (_window is null || _shown is null)
		{
			return;
		}
		_content = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window.AddChild(_content);
		if (_scrollbar is not null)
		{
			_window.MoveChild(_scrollbar, -1);
		}
		if (_shown.Pies is { } pies)
		{
			DrawPies(pies);
		}
		else
		{
			DrawTable(_shown);
		}
	}

	private void DrawTable(CLedgerPage page)
	{
		for (int column = 0; column < page.Columns.Count; column++)
		{
			DrawHeader(page.Columns[column], column, page.Spans[column]);
		}
		int last = Math.Min(page.Rows.Count, _firstRow + VisibleRows);
		for (int row = _firstRow; row < last; row++)
		{
			DrawRow(page, page.Rows[row], TableTop + RowsTop + RowHeight * (row - _firstRow));
		}
	}

	/// <summary>A column's head: its icon, then its words after the icon, either of which may be missing.</summary>
	private void DrawHeader(CLedgerColumn column, int index, (int Left, int Right) span)
	{
		float width = span.Right - span.Left;
		var area = new Control { Position = new Vector2(TableLeft + span.Left, TableTop), Size = new Vector2(width, RowHeight), MouseFilter = MouseFilterEnum.Stop };
		_content!.AddChild(area);
		float after = 0f;
		if (column.Icon is { } icon && _context.Types.Find(icon) is CGuiIconType type)
		{
			var picture = new CGuiIcon();
			picture.Initialise(type, _context);
			area.AddChild(picture);
			picture.Position = Vector2.Zero;
			after = picture.Size.X;
		}
		if (column.Header.Length > 0 && Text("TableHeaderText", column.Header, width) is { } text)
		{
			area.AddChild(text);
			text.Position = new Vector2(after, 0f);
		}
		area.GuiInput += input =>
		{
			if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				SortBy(index);
			}
		};
	}

	private void DrawRow(CLedgerPage page, CLedgerRow row, float y)
	{
		float right = TableLeft;
		for (int column = 0; column < page.Columns.Count && column < row.Cells.Count; column++)
		{
			CLedgerCell cell = row.Cells[column];
			(int left, int end) = page.Spans[column];
			right = Math.Max(right, TableLeft + end);
			if (Text("TableItemText", cell.Text, end - left) is not { } text)
			{
				continue;
			}
			_content!.AddChild(text);
			text.Position = new Vector2(TableLeft + left, y);
			if (cell.Tooltip is { } tooltip)
			{
				text.MouseFilter = MouseFilterEnum.Pass;
				text.Tooltip = () => new CTooltip { Text = tooltip };
			}
		}
		if (row.Unit is { } unit)
		{
			var area = new Control { Position = new Vector2(TableLeft, y), Size = new Vector2(right - TableLeft, RowHeight), MouseFilter = MouseFilterEnum.Stop };
			_content!.AddChild(area);
			area.GuiInput += input =>
			{
				if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
				{
					UnitChosen?.Invoke(unit);
					SetClosed();
				}
			};
		}
	}

	private CGuiTextBox? Text(string typeName, string text, float width)
	{
		if (_context.Types.Find(typeName) is not CGuiTextBoxType type)
		{
			return null;
		}
		var box = new CGuiTextBox();
		box.Initialise(type, _context);
		box.Size = new Vector2(width, RowHeight);
		box.LaidOutWidth = width;
		box.Text = text;
		return box;
	}

	/// <summary>A click on a header sorts by that column, the second time the other way; each page keeps its own.</summary>
	private void SortBy(int column)
	{
		bool ascending = !_sorts.TryGetValue(_page, out (int Column, bool Ascending) sort) || sort.Column != column || !sort.Ascending;
		_sorts[_page] = (column, ascending);
		_shown?.Sort(column, ascending);
		_firstRow = 0;
		ShowScrollbarAt();
		Redraw();
	}

	private int Unseen => _shown is null ? 0 : Math.Max(0, _shown.Rows.Count - VisibleRows);

	private void Scroll(int first)
	{
		int to = Math.Clamp(first, 0, Unseen);
		if (to != _firstRow)
		{
			_firstRow = to;
			ShowScrollbarAt();
			Redraw();
		}
	}

	/// <summary>The bar is made once for a page, so that dragging its knob outlives the rows being drawn again.</summary>
	private void MakeScrollbar()
	{
		_scrollbar?.QueueFree();
		_scrollbar = null;
		if (Unseen == 0 || _window is null || _context.Types.FindScrollbar("standardlistbox_slider") is not { } type)
		{
			return;
		}
		_scrollbar = new CGuiScrollbar();
		_scrollbar.Initialise(type, _context, RowHeight * VisibleRows);
		_window.AddChild(_scrollbar);
		_scrollbar.Position = new Vector2(ScrollbarLeft, ScrollbarTop);
		_scrollbar.Moved += value => Scroll(Mathf.RoundToInt((1f - value.Raw / (float)CFixedPoint.Scale) * Unseen));
		ShowScrollbarAt();
	}

	/// <summary>A bar that runs up and down has its greatest value at the top, as a list's has.</summary>
	private void ShowScrollbarAt()
	{
		if (_scrollbar is { Held: false } && Unseen > 0)
		{
			_scrollbar.Value = new CFixedPoint((long)((1f - _firstRow / (float)Unseen) * CFixedPoint.Scale));
		}
	}

	private void DrawPies(IReadOnlyList<CLedgerPie> pies)
	{
		for (int index = 0; index < pies.Count && index < PieOffsets.Length; index++)
		{
			Vector2 offset = _context.Types.FindPosition(PieOffsets[index]) is { } position ? new Vector2(position.Position.X, position.Position.Y) : Vector2.Zero;
			DrawPie(pies[index], PieOrigin + offset);
		}
	}

	private void DrawPie(CLedgerPie pie, Vector2 at)
	{
		if (Text("pie_heading", pie.Heading, 300f) is { } heading)
		{
			_content!.AddChild(heading);
			heading.Position = at + new Vector2(-42f, -24f);
		}
		if (pie.Slices.Count == 0)
		{
			if (CGuiWindow.CreateEntry(_context, "no_pie_data_window") is { } none)
			{
				_content!.AddChild(none);
				none.Position = at;
			}
			return;
		}
		var chart = new CLedgerPieChart { Position = at, Size = new Vector2(PieSize, PieSize), MouseFilter = MouseFilterEnum.Ignore };
		_content!.AddChild(chart);
		chart.Show(pie.Slices);
		float y = at.Y;
		foreach (CLedgerSlice slice in pie.Slices)
		{
			string label = slice.Label + " " + slice.Value.ToString("0.#", CultureInfo.InvariantCulture);
			if (Text("pie_text", label, 240f) is { } text)
			{
				_content!.AddChild(text);
				text.Position = new Vector2(at.X + PieSize + 90f, y);
				text.Colour = slice.Colour.ToGodot();
			}
			y += 14f;
		}
	}
}
