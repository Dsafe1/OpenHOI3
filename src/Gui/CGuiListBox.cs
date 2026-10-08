using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// A <c>listboxType</c>: entries stacked one under the other with the box's spacing
/// between them, cut off at the box's edge, and scrolled by the wheel or by the
/// scrollbar the box names when they do not all fit.
///
/// The original's stacking and scrolling were not read; this is the plain reading of
/// the declaration. See <c>docs/mechanics/interface.md</c>, section 2.
/// </summary>
public partial class CGuiListBox : CGuiObject
{
	/// <summary>How far one notch of the wheel moves the list, in the file's units.</summary>
	private const float WheelStep = 40f;

	private readonly List<Control> _entries = new();
	private Control _content = null!;
	private CGuiScrollbar? _scrollbar;
	private CGuiListBoxType _type = null!;
	private float _contentHeight;
	private float _offset;

	public void Initialise(CGuiListBoxType type, CGuiContext context)
	{
		_type = type;
		Place(type, new Vector2(type.Size.X, type.Size.Y));
		MouseFilter = MouseFilterEnum.Pass;

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

		// The entries are cut off at the box; the scrollbar stands outside it, to the right.
		var clip = new Control { Name = "Clip", ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
		clip.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(clip);
		_content = new Control { Name = "Entries", MouseFilter = MouseFilterEnum.Pass };
		clip.AddChild(_content);

		if (context.Types.FindScrollbar(type.Scrollbar) is { } scrollbarType)
		{
			_scrollbar = new CGuiScrollbar();
			_scrollbar.Initialise(scrollbarType, context, type.Size.Y);
			_scrollbar.Position = new Vector2(type.Size.X, 0f);
			_scrollbar.Visible = false;
			// A bar that runs up and down has its greatest value at the top, so the top
			// of the list is the bar's whole and the bottom its nothing.
			_scrollbar.Moved += value => ScrollTo((1f - value.Raw / (float)CFixedPoint.Scale) * Overflow);
			AddChild(_scrollbar);
		}
	}

	/// <summary>How many entries the list holds.</summary>
	public int Count => _entries.Count;

	public IReadOnlyList<Control> Entries => _entries;

	/// <summary>Puts an entry at the end of the list. The entry's own height is what it takes.</summary>
	public void Add(Control entry)
	{
		entry.Position = new Vector2(_type.BorderSize.X, _type.BorderSize.Y + _contentHeight);
		_content.AddChild(entry);
		_entries.Add(entry);
		_contentHeight += entry.Size.Y + _type.Spacing;
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
		_contentHeight = 0f;
		Settle();
	}

	/// <summary>How much of the entries does not fit in the box.</summary>
	private float Overflow => Mathf.Max(_contentHeight - _type.Spacing + 2f * _type.BorderSize.Y - Size.Y, 0f);

	private void Settle()
	{
		if (_scrollbar is not null)
		{
			_scrollbar.Visible = Overflow > 0f;
		}
		ScrollTo(_offset);
	}

	private void ScrollTo(float offset)
	{
		_offset = Mathf.Clamp(offset, 0f, Overflow);
		_content.Position = new Vector2(0f, -Mathf.Round(_offset));
		if (_scrollbar is not null && Overflow > 0f && !_scrollbar.Held)
		{
			_scrollbar.Value = new CFixedPoint((long)((1f - _offset / Overflow) * CFixedPoint.Scale));
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { Pressed: true } wheel || Overflow <= 0f)
		{
			return;
		}
		if (wheel.ButtonIndex == MouseButton.WheelUp)
		{
			AcceptEvent();
			ScrollTo(_offset - WheelStep);
		}
		else if (wheel.ButtonIndex == MouseButton.WheelDown)
		{
			AcceptEvent();
			ScrollTo(_offset + WheelStep);
		}
	}
}
