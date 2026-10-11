using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// A <c>listboxType</c>, laid out the way the original lays out all but two of its
/// lists: in rows. The box shows as many lines as go whole into its height, one
/// straight under the other with nothing between them, and a line that does not fit is
/// not drawn at all - nothing is cut off. When there are more lines than rows the
/// scrollbar the box names appears, and it and the wheel move the list a line at a
/// time. The box's <c>spacing</c> has no part in it.
///
/// See <c>docs/mechanics/interface.md</c>, section 2, and
/// <c>reversing/findings/FINDINGS-listbox.md</c>.
/// </summary>
public partial class CGuiListBox : CGuiObject
{
	private readonly List<Control> _entries = new();
	private Control _content = null!;
	private CGuiScrollbar? _scrollbar;
	private CGuiListBoxType _type = null!;
	private int _rows;
	private int _first;
	private bool _byHeights;

	public void Initialise(CGuiListBoxType type, CGuiContext context)
	{
		_type = type;
		Place(type, new Vector2(type.Size.X, type.Size.Y));
		// The box takes no click itself, and neither does what holds its entries: only
		// the entries do. A box is often far larger than what is in it and lies over
		// other things - the builders' `stats_pos` is one line in a box 620 high that
		// covers the buttons that order and close - and those have to stay reachable.
		MouseFilter = MouseFilterEnum.Ignore;

		CSpriteType? background = context.Sprites.Find(type.Background);
		if (background is not null && context.Textures.Load(background) is { } texture)
		{
			var behind = new TextureRect
			{
				Name = "Background",
				Texture = texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = background.IsTiled ? TextureRect.StretchModeEnum.Tile : TextureRect.StretchModeEnum.Scale,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			behind.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(behind);
		}

		// Nothing cuts the entries off: one that is not among the rows is hidden whole,
		// and one that is may be as wide as it likes. The files give lists 533 wide lines
		// whose columns run to 850 and beyond.
		_content = new Control { Name = "Entries", MouseFilter = MouseFilterEnum.Ignore };
		AddChild(_content);

		if (context.Types.FindScrollbar(type.Scrollbar) is { } scrollbarType)
		{
			_scrollbar = new CGuiScrollbar();
			_scrollbar.Initialise(scrollbarType, context, type.Size.Y);
			_scrollbar.Position = new Vector2(type.Size.X, 0f);
			_scrollbar.Visible = false;
			// A bar that runs up and down has its greatest value at the top, so the top
			// of the list is the bar's whole and the bottom its nothing.
			_scrollbar.Moved += value =>
				ShowFrom(Mathf.RoundToInt((1f - value.Raw / (float)CFixedPoint.Scale) * Unseen));
			AddChild(_scrollbar);
		}
	}

	/// <summary>How many entries the list holds.</summary>
	public int Count => _entries.Count;

	public IReadOnlyList<Control> Entries => _entries;

	/// <summary>How many lines the box shows at once.</summary>
	public int Rows => _rows;

	/// <summary>
	/// Puts an entry at the end of the list. The rows the box has are counted again by
	/// this entry's height: as many as go whole into the box's.
	/// </summary>
	public void Add(Control entry)
	{
		_content.AddChild(entry);
		_entries.Add(entry);
		_rows = CGuiLayout.ListRows(_type.Size.Y, entry.Size.Y);
		Settle();
	}

	/// <summary>Empties the list.</summary>
	public void Clear()
	{
		foreach (Control entry in _entries)
		{
			_content.RemoveChild(entry);
			entry.QueueFree();
		}
		_entries.Clear();
		Settle();
	}

	/// <summary>
	/// Switches the list from rows to its entries' own heights: each entry goes straight
	/// under the one before it, touching it, and all of them are shown. The original has
	/// this for two lists, the panel of a selected unit one of them. It scrolls when they
	/// do not all fit; that part is not built.
	///
	/// That the entries touch is measured on a screenshot of the original's unit panel,
	/// whose list declares a <c>spacing</c> of 2 and shows none: the routine was read as
	/// putting the spacing between them, and only as "likely".
	/// </summary>
	public void UseEntryHeights()
	{
		_byHeights = true;
		Settle();
	}

	/// <summary>Lays the entries out again, after one of them has changed its height.</summary>
	public void Relayout()
	{
		Settle();
	}

	/// <summary>How many of the entries have no row.</summary>
	private int Unseen => _byHeights ? 0 : Math.Max(_entries.Count - _rows, 0);

	private void Settle()
	{
		if (_scrollbar is not null)
		{
			_scrollbar.Visible = Unseen > 0;
			// An end button moves a line, as the original's does: its bar counts in lines.
			if (Unseen > 0)
			{
				_scrollbar.PressStep = new CFixedPoint(CFixedPoint.Scale / Unseen);
			}
		}
		if (_byHeights)
		{
			float down = _type.BorderSize.X;
			foreach (Control entry in _entries)
			{
				entry.Visible = true;
				entry.Position = new Vector2(_type.BorderSize.X, down);
				down += entry.Size.Y;
			}
			return;
		}
		ShowFrom(_first);
	}

	/// <summary>
	/// Shows a row's worth of entries from one of them on, each a first entry's height
	/// under the one before, and hides the rest.
	/// </summary>
	private void ShowFrom(int first)
	{
		_first = Math.Clamp(first, 0, Unseen);
		float pitch = _entries.Count > 0 ? _entries[0].Size.Y : 0f;
		// The original starts both ways from the first of borderSize's two numbers.
		var place = new Vector2(_type.BorderSize.X, _type.BorderSize.X);
		for (int index = 0; index < _entries.Count; index++)
		{
			Control entry = _entries[index];
			bool shown = index >= _first && index < _first + _rows;
			entry.Visible = shown;
			if (shown)
			{
				entry.Position = place;
				place.Y += pitch;
			}
		}
		if (_scrollbar is not null && Unseen > 0 && !_scrollbar.Held)
		{
			_scrollbar.Value = new CFixedPoint((long)((1f - _first / (float)Unseen) * CFixedPoint.Scale));
		}
	}

	/// <summary>
	/// The wheel scrolls the list the mouse is over, a line a notch, whatever of the
	/// list it is over - a line's button would otherwise keep the turn to itself.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { Pressed: true } wheel || Unseen <= 0
			|| wheel.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)
			|| !IsVisibleInTree() || !new Rect2(Vector2.Zero, Size).HasPoint(GetLocalMousePosition()))
		{
			return;
		}
		GetViewport().SetInputAsHandled();
		ShowFrom(_first + (wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1));
	}
}
