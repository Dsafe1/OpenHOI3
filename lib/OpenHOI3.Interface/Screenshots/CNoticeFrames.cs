namespace OpenHOI3.Interface.Screenshots;

/// <summary>
/// How long a notice of a picture stands (<c>docs/mechanics/loadingscreen.md</c>, section 6): fifteen frames, the
/// frame the picture is taken the first - by frames, so how long hangs on the frame rate.
/// </summary>
public sealed class CNoticeFrames
{
	public const int Frames = 15;

	private int _left;

	public void Start()
	{
		_left = Frames;
	}

	/// <summary>The frame's look: whether the notice is drawn into it.</summary>
	public bool Frame()
	{
		if (_left == 0)
		{
			return false;
		}
		_left--;
		return true;
	}
}
