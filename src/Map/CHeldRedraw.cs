using Godot;

namespace OpenHOI3.Map;

/// <summary>
/// A map layer's redraw, put off while the game's hours run beside the frame: such a layer reads the game as it draws,
/// and the game is the hours' until they are collected. A redraw asked for meanwhile - the camera moving, a pulse - is
/// made the moment the hold is let go, and until then the layer shows what it last drew.
/// </summary>
public sealed class CHeldRedraw
{
	private readonly CanvasItem _item;
	private bool _held;
	private bool _wanted;

	public CHeldRedraw(CanvasItem item)
	{
		_item = item;
	}

	/// <summary>Whether redraws wait; letting go makes the one that waited.</summary>
	public bool Held
	{
		get => _held;
		set
		{
			_held = value;
			if (!value && _wanted)
			{
				_wanted = false;
				_item.QueueRedraw();
			}
		}
	}

	/// <summary>A redraw now, or once the hold is let go.</summary>
	public void Request()
	{
		if (_held)
		{
			_wanted = true;
			return;
		}
		_item.QueueRedraw();
	}
}
