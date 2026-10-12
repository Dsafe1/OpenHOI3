using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// The popups of messages, none queued: each stands at its window's place, 10 further right and down
/// for each before it and 40 down, the count going back to nought after ten - and a place whose window
/// would cross the screen's right or bottom edge is moved to the screen's size less 10, as the
/// original's is. See <c>docs/mechanics/messages.md</c>, section 4.
/// </summary>
public sealed partial class CMessagePopups : Control
{
	private const int Places = 11;
	private const float Step = 10f;
	private const float Down = 40f;
	private const float EdgeMargin = 10f;

	private CGuiContext _context = null!;
	private CSimulation _simulation = null!;
	private int _count;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CSimulation simulation)
	{
		_context = context;
		_simulation = simulation;
	}

	public override void _Ready()
	{
		Name = "MessagePopups";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public void Open(CMessage message)
	{
		var popup = new CMessagePopup();
		popup.Setup(_context, _simulation, message);
		AddChild(popup);
		if (popup.Window is not { } window)
		{
			return;
		}
		_count = (_count + 1) % Places;
		var moved = window.Position + new Vector2(Step * _count, Step * _count + Down);
		Vector2 screen = GetViewportRect().Size;
		if (moved.X + window.Size.X > screen.X)
		{
			moved.X = screen.X - EdgeMargin;
		}
		if (moved.Y + window.Size.Y > screen.Y)
		{
			moved.Y = screen.Y - EdgeMargin;
		}
		window.Position = moved;
	}
}
