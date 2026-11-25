namespace OpenHOI3.Interface.Screenshots;

/// <summary>
/// When a picture asked for by its key is taken (<c>docs/mechanics/screenshots.md</c>, section 1): asked once a
/// frame, it says yes where the key was pressed and no picture of its kind was taken in the ten frames before. A
/// press inside those ten frames is dropped, not kept.
/// </summary>
public sealed class CPictureGate
{
	/// <summary>The frames from one picture to the next that may be taken.</summary>
	public const int Frames = 10;

	private bool _asked;
	private int _wait;

	public void Ask()
	{
		_asked = true;
	}

	public bool Asked => _asked;

	/// <summary>The frame's look: whether the picture is to be taken now.</summary>
	public bool Frame()
	{
		bool take = _asked && _wait == 0;
		if (take)
		{
			_wait = Frames;
		}
		_asked = false;
		if (_wait > 0)
		{
			_wait--;
		}
		return take;
	}
}
