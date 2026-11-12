using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.StrategicWarfare;

namespace OpenHOI3.Screens;

/// <summary>
/// The Strategic Warfare Manager, <c>swm_window</c>: every enemy the played country has fought, and for those chosen what the war has cost each side by sea, in the air and on land,
/// as charts over the last month, year or the whole conflict. The game goes on under it. See <c>docs/mechanics/strategicwarfare.md</c>, section 4.
/// </summary>
public sealed partial class CWarfareView : Control
{
	private static readonly string[] Tabs = { "tab_naval", "tab_air", "tab_land" };
	private static readonly string[] Subs = { "sub_naval", "sub_air", "sub_land" };

	private readonly List<CWarfareEntry> _rows = new();
	private readonly HashSet<CCountryTag> _selected = new();
	private readonly Dictionary<string, CLineChart> _charts = new();
	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CGuiWindow? _window;
	private CGuiListBox? _list;
	private int _tab;
	private int _duration;
	private bool _opened;
	private long _drawnHour = -1;
	private string _rowsFor = "";

	public void Setup(CGuiContext context, CSimulation simulation)
	{
		_context = context;
		_simulation = simulation;
	}

	public bool IsOpen => _window is { Visible: true };

	private CWarfareLedger Ledger => _simulation.State.Warfare;

	public override void _Ready()
	{
		Name = "StrategicWarfare";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	private bool _made;

	/// <summary>The window made, once, as the screen first opens rather than as the game is entered (CCountryView.Make).</summary>
	private void Make()
	{
		if (_made)
		{
			return;
		}
		_made = true;
		_window = CGuiWindow.Create(_context, CWarfareText.WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_list = _window.FindListBox("listbox_countries");
		Bind("close_button", () => _window.Visible = false);
		Bind("select_all", () => Select(_ => true));
		Bind("select_present", SelectPresent);
		Bind("select_none", () => Select(_ => false));
		for (int tab = 0; tab < Tabs.Length; tab++)
		{
			int chosen = tab;
			Bind(Tabs[tab], () => SetTab(chosen));
			if (_window.TryFindButton(Tabs[tab]) is { } button)
			{
				string name = Tabs[tab];
				button.Tooltip = () => CWarfareText.Tooltip(_context.Localisation.Localise, name);
			}
		}
		for (int duration = 0; duration < 3; duration++)
		{
			int chosen = duration;
			Bind("duration_" + (duration + 1), () => SetDuration(chosen));
		}
		foreach (string sub in Subs)
		{
			if (_window.FindWindow(sub) is { } window)
			{
				MakeCharts(window);
			}
		}
		SetTabFrames();
		SetDurationFrames();
		_window.Visible = false;
	}

	private void Bind(string name, Action pressed)
	{
		if (_window?.TryFindButton(name) is { } button)
		{
			button.Pressed += pressed;
		}
	}

	/// <summary>A chart for each <c>linechart_</c> element of a tab, standing in its place, its tooltip its own.</summary>
	private void MakeCharts(CGuiWindow sub)
	{
		foreach (Node child in sub.GetChildren())
		{
			if (child is not CGuiIcon icon || !icon.Name.ToString().StartsWith("linechart_", StringComparison.Ordinal))
			{
				continue;
			}
			string name = icon.Name;
			if (_context.Sprites.Find("GFX_swm_linechart") is not { } sprite)
			{
				continue;
			}
			var chart = new CLineChart();
			chart.Setup(_context, sprite);
			icon.AddChild(chart);
			icon.CustomMinimumSize = sprite.TileSize;
			icon.Size = sprite.TileSize;
			icon.Tooltip = () => CWarfareText.Tooltip(_context.Localisation.Localise, name);
			_charts[name] = chart;
		}
	}

	public void Toggle()
	{
		Make();
		if (_window is null)
		{
			return;
		}
		_window.Visible = !_window.Visible;
		if (!_window.Visible)
		{
			return;
		}
		RebuildRows();
		if (!_opened)
		{
			_opened = true;
			SelectPresent();
		}
		ShowTab();
		Refresh();
	}

	public override void _Process(double delta)
	{
		if (!IsOpen)
		{
			return;
		}
		RebuildRows();
		if (_drawnHour != _simulation.State.TotalHours)
		{
			Refresh();
		}
	}

	/// <summary>The rows made again only where the records have changed; those chosen stay chosen.</summary>
	private void RebuildRows()
	{
		if (_list is null)
		{
			return;
		}
		List<CWarfareRecord> records = CWarfareText.Rows(Ledger);
		string key = string.Join(",", records.Select(record => record.Enemy.Value + (record.Ended ? "-" : "+")));
		if (key == _rowsFor)
		{
			return;
		}
		_rowsFor = key;
		_list.Clear();
		_rows.Clear();
		foreach (CWarfareRecord record in records)
		{
			if (CWarfareEntry.Create(_context, _simulation.State, record, Toggled) is not { } row)
			{
				continue;
			}
			row.SetSelected(_selected.Contains(record.Enemy));
			_rows.Add(row);
			_list.Add(row.Window);
		}
	}

	private void Toggled(CWarfareRecord record, bool selected)
	{
		if (selected)
		{
			_selected.Add(record.Enemy);
		}
		else
		{
			_selected.Remove(record.Enemy);
		}
		Refresh();
	}

	private void SelectPresent() => Select(record => !record.Ended);

	private void Select(Func<CWarfareRecord, bool> chosen)
	{
		_selected.Clear();
		foreach (CWarfareEntry row in _rows)
		{
			bool selected = chosen(row.Record);
			row.SetSelected(selected);
			if (selected)
			{
				_selected.Add(row.Record.Enemy);
			}
		}
		Refresh();
	}

	private void SetTab(int tab)
	{
		_tab = tab;
		ShowTab();
		Refresh();
	}

	private void ShowTab()
	{
		SetTabFrames();
		for (int sub = 0; sub < Subs.Length; sub++)
		{
			if (_window?.FindWindow(Subs[sub]) is { } window)
			{
				window.Visible = sub == _tab;
			}
		}
	}

	private void SetTabFrames()
	{
		for (int tab = 0; tab < Tabs.Length; tab++)
		{
			_window?.TryFindButton(Tabs[tab])?.SetFrame(tab == _tab ? 2 : 1);
		}
	}

	private void SetDuration(int duration)
	{
		_duration = duration;
		SetDurationFrames();
		Refresh();
	}

	private void SetDurationFrames()
	{
		for (int duration = 0; duration < 3; duration++)
		{
			_window?.TryFindButton("duration_" + (duration + 1))?.SetFrame(duration == _duration ? 1 : 2);
		}
	}

	private List<CWarfareSeries> Lines(int category, int column)
	{
		IEnumerable<CWarfareRecord> chosen = CWarfareText.Rows(Ledger).Where(record => _selected.Contains(record.Enemy));
		return CWarfareText.Series(_simulation.State, chosen, category, column, _duration);
	}

	private void Refresh()
	{
		if (_window is null || !IsOpen)
		{
			return;
		}
		_drawnHour = _simulation.State.TotalHours;
		switch (_tab)
		{
			case 0:
				RefreshNaval();
				break;
			case 1:
				RefreshAir();
				break;
			default:
				RefreshLand();
				break;
		}
	}

	private CGuiWindow? Sub => _window?.FindWindow(Subs[_tab]);

	private void Text(string box, string text)
	{
		if (Sub?.FindTextBox(box) is { } found)
		{
			found.Text = text;
		}
	}

	private string L(string key) => _context.Localisation.Localise(key);

	private string Filled(string key, string name, long value, int decimals) => CTextTemplate.Fill(L(key), (name, CWarfareText.Figure(value, decimals)));

	private void Draw(string chart, IReadOnlyList<CWarfareSeries> lines, long scale)
	{
		if (!_charts.TryGetValue(chart, out CLineChart? drawn))
		{
			return;
		}
		if (lines.Count == 0)
		{
			drawn.Show(new (IReadOnlyList<int>, Color)[] { (new[] { 0, 0, 0 }, new Color(0f, 1f, 0f)) });
			return;
		}
		drawn.Show(lines.Select(line => ((IReadOnlyList<int>)line.Values.Select(value => CWarfareText.Height(value, scale)).ToList(), line.Colour.ToGodot())).ToList());
	}

	private void Pair(string first, int firstColumn, string second, int secondColumn, int category, string maxBox, string maxKey,
		out List<CWarfareSeries> a, out List<CWarfareSeries> b)
	{
		a = Lines(category, firstColumn);
		b = Lines(category, secondColumn);
		long scale = CWarfareText.Scale(a, b);
		Draw(first, a, scale);
		Draw(second, b, scale);
		Text(maxBox, Filled(maxKey, "MAX", scale, 2));
	}

	private void Lone(string chart, int column, string maxBox, string maxKey, out List<CWarfareSeries> a)
	{
		a = Lines(CWarfareRecord.Naval, column);
		long scale = CWarfareText.Scale(a);
		Draw(chart, a, scale);
		Text(maxBox, Filled(maxKey, "MAX", scale, 2));
	}

	private void Frame(string icon, int frame) => Sub?.TryFindIcon(icon)?.SetFrame(frame);

	private void RefreshNaval()
	{
		Pair("linechart_our_convoy_ic", 0, "linechart_our_enemy_ic", 2, CWarfareRecord.Naval, "our_graph_ic_max", "SWM_GRAPH_MAX_IC_VAL", out var ourConvoys, out var ourEnemy);
		Lone("linechart_our_convoy_cargo", 1, "our_cargo_max", "SWM_GRAPH_MAX_TONS_VAL", out var ourCargo);
		Pair("linechart_their_convoy_ic", 3, "linechart_their_enemy_ic", 5, CWarfareRecord.Naval, "their_graph_ic_max", "SWM_GRAPH_MAX_IC_VAL", out var theirConvoys, out var theirEnemy);
		Lone("linechart_their_convoy_cargo", 4, "their_cargo_max", "SWM_GRAPH_MAX_TONS_VAL", out var theirCargo);
		long oc = CWarfareText.Total(ourConvoys);
		long oe = CWarfareText.Total(ourEnemy);
		long tc = CWarfareText.Total(theirConvoys);
		long te = CWarfareText.Total(theirEnemy);
		Text("our_defender_avg_ic", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", oc, 2));
		Text("our_attacker_avg_ic", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", oe, 2));
		Text("their_defender_avg_ic", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", tc, 2));
		Text("their_attacker_avg_ic", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", te, 2));
		Frame("comparator_our", CWarfareText.NavalFrame(oc, oe));
		Frame("comparator_their", CWarfareText.NavalFrame(tc, te));
		string suffix = (_duration + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
		Text("cargo_desc_our", Filled("SWM_CARGO_LOST_DESC_" + suffix, "NUM", CWarfareText.Total(ourCargo), 2));
		Text("cargo_desc_their", Filled("SWM_CARGO_SUNK_DESC_" + suffix, "NUM", CWarfareText.Total(theirCargo), 2));
	}

	private void RefreshAir()
	{
		Pair("linechart_our_province", 1, "linechart_our_bombers", 0, CWarfareRecord.Air, "our_graph_ic_max", "SWM_GRAPH_MAX_IC_VAL", out var ourProvince, out var ourBombers);
		Pair("linechart_their_bombers", 4, "linechart_their_province", 5, CWarfareRecord.Air, "their_graph_ic_max", "SWM_GRAPH_MAX_IC_VAL", out var theirBombers, out var theirProvince);
		Text("our_province_total_dam", Filled("SWM_GRAPH_TOTAL_DAMAGE_VAL", "TOT", CWarfareText.Total(Lines(CWarfareRecord.Air, 3)), 2));
		Text("our_bombers_total_dam", Filled("SWM_GRAPH_TOTAL_DAMAGE_VAL", "TOT", CWarfareText.Total(Lines(CWarfareRecord.Air, 2)), 2));
		Text("their_bombers_total_dam", Filled("SWM_GRAPH_TOTAL_DAMAGE_VAL", "TOT", CWarfareText.Total(Lines(CWarfareRecord.Air, 6)), 2));
		Text("their_province_total_dam", Filled("SWM_GRAPH_TOTAL_DAMAGE_VAL", "TOT", CWarfareText.Total(Lines(CWarfareRecord.Air, 7)), 2));
		string suffix = (_duration + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
		Text("our_province_desc", Filled("SWM_OUR_PROV_IC_LOSS_DESC_" + suffix, "IC", CWarfareText.Total(ourProvince), 1));
		Text("their_province_desc", Filled("SWM_THEIR_PROV_IC_LOSS_DESC_" + suffix, "IC", CWarfareText.Total(theirProvince), 1));
		Text("our_bombers_desc", Filled("SWM_THEIR_BOMBERS_IC_LOSS_DESC_" + suffix, "IC", CWarfareText.Total(ourBombers), 1));
		Text("their_bombers_desc", Filled("SWM_OUR_BOMBERS_IC_LOSS_DESC_" + suffix, "IC", CWarfareText.Total(theirBombers), 1));
	}

	private void RefreshLand()
	{
		Pair("linechart_ic_loss_to_us", 0, "linechart_ic_loss_to_them", 1, CWarfareRecord.Land, "ic_loss_max_val", "SWM_GRAPH_MAX_IC_VAL", out var icUs, out var icThem);
		Pair("linechart_man_loss_to_us", 2, "linechart_man_loss_to_them", 3, CWarfareRecord.Land, "man_loss_max_val", "SWM_GRAPH_MAX_MAN_VAL", out var menUs, out var menThem);
		long iu = CWarfareText.Total(icUs);
		long it = CWarfareText.Total(icThem);
		long mu = CWarfareText.Total(menUs);
		long mt = CWarfareText.Total(menThem);
		Text("ic_loss_avg_us", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", iu, 2));
		Text("ic_loss_avg_them", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", it, 2));
		Text("man_loss_avg_us", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", mu, 2));
		Text("man_loss_avg_them", Filled("SWM_GRAPH_TOTAL_VAL", "TOT", mt, 2));
		Frame("comparator_ic", CWarfareText.LandIcFrame(iu, it));
		Frame("comparator_man", CWarfareText.LandMenFrame(mu, mt));
	}
}
