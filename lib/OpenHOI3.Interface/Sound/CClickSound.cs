namespace OpenHOI3.Interface.Sound;

/// <summary>
/// Which sound a button or a checkbox makes: its own key, else its sprite's <c>clicksound</c>, else the screen's default,
/// <c>click</c> - by whether a name is given, not whether it is a sound, so a name <c>sound.sfx</c> has not got leaves the
/// button silent, as <c>close_window</c> and <c>start_game</c> leave ten of the original's
/// (<c>reversing/findings/FINDINGS-soundeffects.md</c>, section 5).
/// </summary>
public static class CClickSound
{
	/// <summary>The default the in-game screen and the front end give every button [exe].</summary>
	public const string Default = "click";

	public static string Choose(string own, string sprite)
	{
		return own.Length > 0 ? own : sprite.Length > 0 ? sprite : Default;
	}
}
