using System;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// An enhanced game's scrolling of a tooltip taller than the screen (the fix <c>tooltip_scroll</c>, after BiceLib's
/// <c>activateTriggerScroll</c>; <c>docs/mechanics/tooltips.md</c>, section 8): which of its lines are shown, moved by
/// the wheel three lines a notch and by the arrow keys a line a press, both with Alt held - the wheel alone belongs
/// to the list or the map under the tooltip. Any tooltip, not a requirement's alone. The place is kept while the text
/// stays the same and starts again at the top when it changes.
/// </summary>
public sealed class CTooltipScroll
{
	/// <summary>How many lines a notch of the wheel moves.</summary>
	public const int LinesANotch = 3;

	/// <summary>How long an arrow key is held before it moves another line, in seconds.</summary>
	public const double KeyRepeat = 0.13;

	/// <summary>The fewest lines a box has room for before it is scrolled: one above, one below and one of the text.</summary>
	public const int LeastRoom = 3;

	/// <summary>The line that stands for the lines left out above, <c>$COUNT$</c> of them; the mod's.</summary>
	public const string AboveKey = "OPENHOI_TOOLTIP_MORE_ABOVE";

	/// <summary>The line that stands for the lines left out below, <c>$COUNT$</c> of them; the mod's.</summary>
	public const string BelowKey = "OPENHOI_TOOLTIP_MORE_BELOW";

	/// <summary>What either line is where the localisation has none.</summary>
	public const string NoWords = "\u00A7Y...\u00A7W";

	private string _text = "";
	private int _position;
	private int _pending;
	private bool _wasUp;
	private bool _wasDown;
	private double _held;

	/// <summary>Whether the text last asked about had more lines than fitted: when the wheel is the tooltip's.</summary>
	public bool Scrollable { get; private set; }

	/// <summary>The wheel turned with Alt held: so many notches, towards the user counted as down the text.</summary>
	public void Wheel(int notchesDown)
	{
		_pending += notchesDown * LinesANotch;
	}

	/// <summary>
	/// The arrow keys as they stand this frame, each with Alt: a fresh press moves a line at once and a held key another
	/// every <see cref="KeyRepeat"/>. Down wins where both are held.
	/// </summary>
	public void Keys(bool up, bool down, double seconds)
	{
		up = up && !down;
		if ((down && !_wasDown) || (up && !_wasUp))
		{
			_pending += down ? 1 : -1;
			_held = 0;
		}
		else if (down || up)
		{
			_held += seconds;
			while (_held >= KeyRepeat)
			{
				_held -= KeyRepeat;
				_pending += down ? 1 : -1;
			}
		}
		_wasDown = down;
		_wasUp = up;
	}

	/// <summary>
	/// The lines to show of a text of so many where the box has room for so many, the wheel's and the keys' steps since
	/// the last asking taken. Everything where it fits, or where there is no room to scroll in. Else the lines from the
	/// place held, a line of the room given to each of "more above" and "more below" that is needed; the place is held
	/// so that the text's last line can always be reached and never passed.
	/// </summary>
	public CTooltipWindow Window(string text, int lines, int room)
	{
		if (text != _text)
		{
			_text = text;
			_position = 0;
			_pending = 0;
		}
		Scrollable = room >= LeastRoom && lines > room;
		if (!Scrollable)
		{
			_position = 0;
			_pending = 0;
			return CTooltipWindow.Whole(lines);
		}
		// At the end only "more above" takes a line, so the last place shows the room less one.
		_position = Math.Clamp(_position + _pending, 0, lines - (room - 1));
		_pending = 0;
		int count = room - (_position > 0 ? 1 : 0);
		if (_position + count < lines)
		{
			count--;
		}
		return new CTooltipWindow(_position, count, _position, lines - _position - count);
	}
}
