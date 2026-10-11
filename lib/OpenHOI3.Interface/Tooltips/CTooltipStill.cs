namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// Whether the mouse has been still long enough for a tooltip's second half. The moment it
/// last moved is kept as a whole second of the clock, so the wait is anywhere from nothing
/// to a second. See <c>docs/mechanics/tooltips.md</c>, section 4.
/// </summary>
public sealed class CTooltipStill
{
	private float _x;
	private float _y;
	private int _since;

	/// <summary>
	/// Takes this frame's mouse and the clock, in seconds since the game was entered, and
	/// answers whether the mouse is still.
	/// </summary>
	public bool Update(float mouseX, float mouseY, double seconds)
	{
		if (mouseX != _x || mouseY != _y)
		{
			_since = (int)seconds;
			_x = mouseX;
			_y = mouseY;
		}
		return seconds - _since > 1.0;
	}
}
