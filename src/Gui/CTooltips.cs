using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Gui;

/// <summary>
/// Each frame, the tooltip of what is under the mouse into the tooltip box: an element's own,
/// with a button's shortcut, or over the map the province's; its second half once the mouse
/// has been still; the box placed and kept inside the screen. See
/// <c>docs/mechanics/tooltips.md</c>.
///
/// A layer of its own above every other, so that the box is drawn over whatever it is about.
/// </summary>
public partial class CTooltips : CanvasLayer
{
	/// <summary>Above every other layer of the interface.</summary>
	private const int TopLayer = 100;

	private CTooltipBox? _box;
	private readonly CTooltipStill _still = new();
	private string _shortcutLabel = "";
	private ulong _entered;
	private Vector2 _mouse;

	/// <summary>
	/// The tooltip of the province under a point of the map, or null for none. Not given
	/// yet: what a province's tooltip says is a reading of its own.
	/// </summary>
	public Func<Vector2, CTooltip?>? MapTooltip { get; set; }

	/// <summary>The minimap's upper left corner on the screen, which a province's tooltip stands over; null where there is none.</summary>
	public Func<Vector2?>? MapTooltipAnchor { get; set; }

	public void Setup(CGuiContext context)
	{
		Name = "Tooltips";
		Layer = TopLayer;
		_box = CTooltipBox.Create(context);
		if (_box is not null)
		{
			AddChild(_box);
		}
		// Rendered once and kept, as the original keeps it.
		_shortcutLabel = context.Localisation.Localise("SHORTCUT");
		// The clock counts from the game being entered.
		_entered = Time.GetTicksUsec();
	}

	/// <summary>
	/// The mouse is followed by its own events, so that a pointer moved by the developer
	/// switches counts as one moved by hand.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouse mouse)
		{
			_mouse = mouse.Position;
		}
	}

	public override void _Process(double delta)
	{
		if (_box is null)
		{
			return;
		}
		double seconds = (Time.GetTicksUsec() - _entered) / 1_000_000.0;
		bool still = _still.Update(_mouse.X, _mouse.Y, seconds);
		Vector2 screen = GetViewport().GetVisibleRect().Size;

		CTooltip tooltip = Ask((int)screen.Y);
		string text = CTooltipText.Compose(tooltip, still);
		_box.Show(text);
		if (text.Length == 0)
		{
			return;
		}
		(int x, int y) = CTooltipPlacement.Place(tooltip, (int)_box.Size.X, (int)_box.Size.Y, (int)screen.X, (int)screen.Y);
		_box.Position = new Vector2(x, y);
	}

	/// <summary>
	/// The tooltip of what is under the mouse, with its point: an element's, from its
	/// corner; else the map's, from the mouse.
	/// </summary>
	/// <summary>The console's <c>debug tooltips</c>: none at all where off.</summary>
	public bool Enabled { get; set; } = true;

	private CTooltip Ask(int screenHeight)
	{
		if (!Enabled)
		{
			return CTooltip.Empty;
		}
		if (GetViewport().GuiGetHoveredControl() is { } hovered)
		{
			if (Element(hovered) is not { } element)
			{
				return CTooltip.Empty;
			}
			// A node above it may answer for it first.
			for (Node? node = element; node is not null; node = node.GetParent())
			{
				if (node is ITooltipOwner owner && owner.Answer(element) is { } answer)
				{
					Vector2 at = element.GetGlobalRect().Position;
					return CTooltipPlacement.AtElement(answer, (int)at.X, (int)at.Y, screenHeight);
				}
			}
			CTooltip tooltip;
			if (Above(element) is { } passive)
			{
				element = passive;
				tooltip = passive.PassiveTooltip!.Invoke();
			}
			else
			{
				tooltip = element.Tooltip?.Invoke() ?? CTooltip.Empty;
			}
			if (element is CGuiButton button)
			{
				tooltip = CTooltipText.WithShortcut(tooltip, button.ShortcutText, _shortcutLabel);
			}
			Vector2 corner = element.GetGlobalRect().Position;
			return CTooltipPlacement.AtElement(tooltip, (int)corner.X, (int)corner.Y, screenHeight);
		}
		CTooltip province = MapTooltip?.Invoke(_mouse) ?? CTooltip.Empty;
		// It stands over the minimap's corner wherever the mouse is; with no minimap - the front end - over the mouse.
		Vector2 anchor = MapTooltipAnchor?.Invoke() ?? _mouse;
		return CTooltipPlacement.AtMinimap(province, (int)anchor.X, (int)anchor.Y);
	}

	/// <summary>
	/// The topmost element beside the hovered one, drawn over it, that lets the mouse through
	/// and still has something to say where the mouse is - a text box over a button.
	/// </summary>
	private CGuiObject? Above(CGuiObject hovered)
	{
		// Its own elements are drawn over it, and then those beside it that come after it.
		if (Topmost(hovered, 0) is { } inside)
		{
			return inside;
		}
		return hovered.GetParent() is { } parent ? Topmost(parent, hovered.GetIndex() + 1) : null;
	}

	/// <summary>
	/// Among a node's children from one on, at any depth - a list's rows are windows in a
	/// window - the topmost that answers passively where the mouse is: a later child before an
	/// earlier, and what is in a child before the child.
	/// </summary>
	private CGuiObject? Topmost(Node parent, int from)
	{
		for (int index = parent.GetChildCount() - 1; index >= from; index--)
		{
			if (parent.GetChild(index) is not Control { Visible: true } child)
			{
				continue;
			}
			if (Topmost(child, 0) is { } inside)
			{
				return inside;
			}
			if (child is CGuiObject { PassiveTooltip: not null } element && Contains(element, _mouse))
			{
				return element;
			}
		}
		return null;
	}

	/// <summary>
	/// Whether a point of the screen is on an element, turned as it is drawn: a turned bar's
	/// rectangle on the screen is not the box it was laid out in.
	/// </summary>
	private static bool Contains(Control element, Vector2 point)
	{
		Vector2 local = element.GetGlobalTransform().AffineInverse() * point;
		return new Rect2(Vector2.Zero, element.Size).HasPoint(local);
	}

	/// <summary>
	/// The element a control under the mouse belongs to: itself, or the nearest it is part
	/// of - a window's background is its window's.
	/// </summary>
	private static CGuiObject? Element(Control control)
	{
		for (Node? node = control; node is not null; node = node.GetParent())
		{
			if (node is CGuiObject element)
			{
				return element;
			}
		}
		return null;
	}
}
