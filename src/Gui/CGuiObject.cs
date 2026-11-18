using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Gui;

/// <summary>
/// The base of what is on screen. A <see cref="CGuiType"/> is what an interface file
/// said; one of these is an instance of it, placed.
/// </summary>
public partial class CGuiObject : Control
{
	public CGuiType GuiType { get; private set; } = null!;

	private Func<CTooltip>? _tooltip;

	/// <summary>
	/// What the element says in a tooltip, asked each frame while the mouse is over it. Null
	/// where nothing answers for it, which is an empty tooltip - as in the original, where an
	/// element's screen answers for it or nothing does. Setting one makes an element that let
	/// the mouse through take it, so that it can be under the mouse.
	/// </summary>
	public Func<CTooltip>? Tooltip
	{
		get => _tooltip;
		set
		{
			_tooltip = value;
			if (value is not null && MouseFilter == MouseFilterEnum.Ignore)
			{
				MouseFilter = MouseFilterEnum.Pass;
			}
		}
	}

	/// <summary>
	/// What an element that lets the mouse through says in a tooltip: it keeps letting the
	/// mouse through, so what lies under it keeps its clicks, and the tooltip layer asks it
	/// before the element under it wherever the mouse is over it. In the original the topmost
	/// element under the mouse answers whatever it is; a text box over a button is the case.
	/// </summary>
	public Func<CTooltip>? PassiveTooltip { get; set; }

	private EGreying _greying;

	/// <summary>
	/// Greys the element and everything in it, or enables them, where the original sets on an element whether it takes
	/// the mouse and hands that down to all it holds - a screen run by the AI stopped whole and then its arrows let take
	/// it again. The original's looks as before; this greys as well, by choice
	/// (<c>docs/mechanics/interface.md</c>, section 7; <c>docs/compatibility.md</c>, entry 19). The nearest element up the
	/// tree that says either decides, so a row added to a greyed list later is greyed too. A button greyed draws so and
	/// cannot be pressed; a scrollbar draws grey and cannot be moved.
	/// </summary>
	public EGreying Greying
	{
		get => _greying;
		set
		{
			if (_greying == value)
			{
				return;
			}
			_greying = value;
			RefreshGreying(this);
		}
	}

	/// <summary>Whether the element is greyed by itself or by what holds it.</summary>
	public bool IsGreyed
	{
		get
		{
			for (Node? node = this; node is not null; node = node.GetParent())
			{
				if (node is CGuiObject { _greying: not EGreying.Inherit } decides)
				{
					return decides._greying == EGreying.Greyed;
				}
			}
			return false;
		}
	}

	/// <summary>Drawn again for a change of greying: what an element that looks greyed does about it.</summary>
	internal virtual void ShowGreying()
	{
	}

	/// <summary>Every element in a part of the tree drawn again for a change of greying above it.</summary>
	private static void RefreshGreying(Node node)
	{
		if (node is CGuiObject element)
		{
			element.ShowGreying();
		}
		foreach (Node child in node.GetChildren())
		{
			RefreshGreying(child);
		}
	}

	/// <summary>
	/// Puts the control where its type says: its upper left corner at the type's
	/// position, measured from the point of the parent its orientation names. The point
	/// is an anchor and not a fixed place, so the element stays with that corner or
	/// centre when the screen is a different shape.
	/// </summary>
	protected void Place(CGuiType type, Vector2 size)
	{
		GuiType = type;
		Name = type.Name.Length == 0 ? type.Keyword : type.Name;

		(float x, float y) = CGuiLayout.Anchor(type.Orientation);
		AnchorLeft = x;
		AnchorRight = x;
		AnchorTop = y;
		AnchorBottom = y;

		OffsetLeft = type.Position.X;
		OffsetTop = type.Position.Y;
		OffsetRight = type.Position.X + size.X;
		OffsetBottom = type.Position.Y + size.Y;
	}

	/// <summary>The size of one frame of a sprite's texture.</summary>
	protected static Vector2 FrameSize(Texture2D texture, int frameCount)
	{
		return new Vector2(texture.GetWidth() / (float)Mathf.Max(frameCount, 1), texture.GetHeight());
	}

	/// <summary>One frame of a sprite sheet, counted from one, as a texture of its own.</summary>
	protected static Texture2D Frame(Texture2D texture, int frame, int frameCount)
	{
		if (frameCount <= 1)
		{
			return texture;
		}
		(float start, float width) = CGuiLayout.FrameRegion(frame, frameCount);
		return new AtlasTexture
		{
			Atlas = texture,
			Region = new Rect2(start * texture.GetWidth(), 0f, width * texture.GetWidth(), texture.GetHeight()),
		};
	}
}
