using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// An enhanced game's custom map modes on screen: the list a right click on a map mode's button opens, and the legend
/// that stands while a source is shown. Both are windows of <c>interface/openhoi</c>, laid out with built-in elements.
/// See <c>docs/mechanics/custommapmodes.md</c>, sections 2 and 6.
/// </summary>
public partial class CCustomMapModes : Control
{
	public const string ListWindowName = "openhoi_mapmode_list";
	public const string ListEntryName = "openhoi_mapmode_entry";
	public const string LegendWindowName = "openhoi_mapmode_legend";

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private CMapView _mapView = null!;
	private Func<bool> _busy = () => false;

	private CGuiWindow? _list;
	private CGuiWindow? _legend;
	private CCustomMapSource? _shown;
	private CCustomMapScale? _shownScale;
	private string _shownPalette = "";

	/// <summary>The palette in use, one of <see cref="CCustomMapColours.Palettes"/>.</summary>
	public string Palette { get; private set; } = CCustomMapColours.Green;

	/// <summary>Raised when the legend's button changes the palette, with the new one: the player's setting.</summary>
	public event Action<string>? PaletteChanged;

	/// <summary>Raised when the list chose: the host mode is to be shown, with the source set or cleared already.</summary>
	public event Action<CMapView.MapMode>? Chosen;

	public void Setup(CGuiContext context, CSimulation simulation, CMapView mapView, string palette, Func<bool> busy)
	{
		_context = context;
		_simulation = simulation;
		_mapView = mapView;
		_busy = busy;
		Palette = CCustomMapColours.Palette(palette);
	}

	public override void _Ready()
	{
		Name = "CustomMapModes";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Which kind of source a map mode's button lists, or none for a button with no list.</summary>
	public static ECustomMapKind? KindOf(CMapView.MapMode mode) => mode switch
	{
		CMapView.MapMode.Infrastructure => ECustomMapKind.Buildings,
		CMapView.MapMode.Resources => ECustomMapKind.Resources,
		CMapView.MapMode.Supply => ECustomMapKind.Supply,
		_ => null,
	};

	/// <summary>
	/// The list of a map mode's sources, its foot at the button pressed: the game's own mode first, then a row a
	/// source. A second right click on the same button shuts it.
	/// </summary>
	public void ToggleList(CMapView.MapMode host, Rect2 button)
	{
		bool same = _list is not null && _listHost == host;
		CloseList();
		if (same || KindOf(host) is not { } kind || CGuiWindow.Create(_context, ListWindowName) is not { } window)
		{
			return;
		}
		_list = window;
		_listHost = host;
		AddChild(window);
		Func<string, string> localise = _context.Localisation.Localise;
		if (window.FindListBox("list") is { } list)
		{
			AddRow(list, localise(CCustomMapLegend.GameOwn), host, null);
			foreach (CCustomMapSource source in CCustomMapSources.Of(_simulation.State, kind))
			{
				AddRow(list, localise(source.Label), host, source);
			}
		}
		// Above the button, its right edge on the button's, and kept on the screen.
		Rect2 screen = GetViewport().GetVisibleRect();
		Vector2 size = window.Size;
		Vector2 corner = new(button.End.X - size.X, button.Position.Y - size.Y);
		window.GlobalPosition = new Vector2(
			Mathf.Clamp(corner.X, screen.Position.X, Mathf.Max(screen.Position.X, screen.End.X - size.X)),
			Mathf.Clamp(corner.Y, screen.Position.Y, Mathf.Max(screen.Position.Y, screen.End.Y - size.Y)));
	}

	private CMapView.MapMode _listHost;

	private void AddRow(CGuiListBox list, string text, CMapView.MapMode host, CCustomMapSource? source)
	{
		if (CGuiWindow.CreateEntry(_context, ListEntryName) is not { } entry || entry.TryFindButton("pick") is not { } pick)
		{
			return;
		}
		pick.SetText(text);
		pick.Pressed += () =>
		{
			CloseList();
			if (source is null)
			{
				_mapView.ClearCustom();
			}
			else
			{
				_mapView.SetCustom(source, host, Palette);
			}
			Chosen?.Invoke(host);
		};
		list.Add(entry);
	}

	public void CloseList()
	{
		_list?.QueueFree();
		_list = null;
	}

	/// <summary>The legend as the map stands: made when a source comes on, written again when its scale or the palette changes, and gone with the source.</summary>
	public override void _Process(double delta)
	{
		// What the map shows is read between the game's hours only.
		if (_busy())
		{
			return;
		}
		CCustomMapSource? source = _mapView.CustomSource is { } custom && _mapView.Mode == _mapView.CustomHost ? custom : null;
		if (source is null)
		{
			if (_legend is not null)
			{
				_legend.QueueFree();
				_legend = null;
				_shown = null;
				_shownScale = null;
			}
			return;
		}
		if (_legend is null)
		{
			if (CGuiWindow.Create(_context, LegendWindowName) is not { } window)
			{
				return;
			}
			_legend = window;
			AddChild(window);
			if (window.TryFindButton("palette") is { } palette)
			{
				palette.Pressed += TogglePalette;
			}
			if (window.TryFindButton("close") is { } close)
			{
				close.Pressed += () => _mapView.ClearCustom();
			}
			_shown = null;
		}
		CCustomMapScale? scale = _mapView.CustomScale;
		if (scale is null || (ReferenceEquals(_shown, source) && ReferenceEquals(_shownScale, scale) && _shownPalette == Palette))
		{
			return;
		}
		_shown = source;
		_shownScale = scale;
		_shownPalette = Palette;
		Write(_legend, source, scale);
	}

	private void Write(CGuiWindow window, CCustomMapSource source, CCustomMapScale scale)
	{
		Func<string, string> localise = _context.Localisation.Localise;
		if (window.FindTextBox("title") is { } title)
		{
			title.Text = localise(source.Label);
		}
		for (int shade = 1; shade <= CCustomMapScale.Shades; shade++)
		{
			// A swatch is a white built-in picture, given its shade's colour here.
			if (window.TryFindIcon("swatch_" + shade) is { } swatch)
			{
				CColor colour = CCustomMapColours.Shade(Palette, shade);
				swatch.Modulate = new Color(colour.R, colour.G, colour.B);
			}
			if (window.FindTextBox("under_" + shade) is { } under)
			{
				under.Text = CCustomMapLegend.Under(localise, source, scale, shade);
			}
		}
		if (window.TryFindButton("palette") is { } palette)
		{
			palette.SetText(localise(CCustomMapLegend.PaletteKey(Palette)));
		}
		string notes = CCustomMapLegend.Notes(localise, source, scale);
		foreach (string name in new[] { "background", "help" })
		{
			if (window.TryFindIcon(name) is { } icon)
			{
				icon.Tooltip = () => new CTooltip { Text = notes };
			}
		}
	}

	private void TogglePalette()
	{
		Palette = Palette == CCustomMapColours.Green ? CCustomMapColours.Heat : CCustomMapColours.Green;
		if (_mapView.CustomSource is { } source)
		{
			_mapView.SetCustom(source, _mapView.CustomHost, Palette);
		}
		PaletteChanged?.Invoke(Palette);
	}
}
