namespace OpenHOI3.Interface.Map;

/// <summary>
/// The life of a message's text on the map, in the original's single-precision arithmetic: its time grows each frame by
/// the game's speed and one, its height is its type's speed times the most times that, and it is done the frame its
/// height passes the most - 201 frames at the slowest speed for the install's 0.005, 41 at the fastest, the clock
/// running or not. It is at full strength for three quarters of the rise, then fades to nothing over the last while it
/// grows to twice its size. See <c>docs/mechanics/messages.md</c>, section 6.
/// </summary>
public sealed class CMapTextRise
{
	/// <summary>The most a message's text rises, in the units its height is counted in [exe].</summary>
	public const float MaxHeight = 2f;

	/// <summary>How far up the world a unit of height is, in the map's world units [exe].</summary>
	public const float RiseForHeight = 3f;

	private readonly float _speed;

	public CMapTextRise(float speed)
	{
		_speed = speed;
	}

	public float Time { get; private set; }

	public float Height { get; private set; }

	/// <summary>Its height has passed the most: it is taken away, and not drawn that frame.</summary>
	public bool Done { get; private set; }

	/// <summary>The frames a second the original's count is taken to be of, where the fix counts by time.</summary>
	public const float FramesASecond = 60f;

	/// <summary>A frame: the game's speed, nought to four, and one more of its time.</summary>
	/// <param name="frames">
	/// How many frames this one counts for: one in the original, whatever the machine. An enhanced game's fix
	/// <c>map_text_by_time</c> passes the frame's seconds by <see cref="FramesASecond"/>, so that a text lives as long on
	/// every machine as the original's does at sixty frames a second - 3.3 seconds at the slowest speed, 0.67 at the
	/// fastest.
	/// </param>
	public void Advance(int gameSpeed, float frames = 1f)
	{
		Time += (gameSpeed + 1f) * frames;
		Height = _speed * MaxHeight * Time;
		if (Height > MaxHeight)
		{
			Height = MaxHeight;
			Done = true;
		}
	}

	/// <summary>How strongly it is drawn: whole until three quarters of the rise, then falling evenly to nothing.</summary>
	public float Alpha => Height > 0.75f * MaxHeight ? (MaxHeight - Height) / (0.25f * MaxHeight) : 1f;

	/// <summary>How large it is drawn: one until it fades, two as it goes.</summary>
	public float Scale => MaxHeight - 0.5f * MaxHeight * Alpha;

	/// <summary>How far above its province's point it stands, in world units.</summary>
	public float Rise => RiseForHeight * Height;
}
