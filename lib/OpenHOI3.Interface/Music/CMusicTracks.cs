using System;

namespace OpenHOI3.Interface.Music;

/// <summary>
/// The three tracks the original names in its code, and its loudness for the player's two volumes
/// (<c>docs/mechanics/music.md</c>; <c>reversing/findings/FINDINGS-music.md</c>, sections 2.5, 4, 5 and 7).
/// </summary>
public static class CMusicTracks
{
	/// <summary>The main menu's, from the startup's load until the first hour of a game is drawn.</summary>
	public const string Title = "music/titletheme_heartsofiron.mp3";

	/// <summary>The end-of-game screen's where the player's faction won.</summary>
	public const string Victory = "music/victorymarchverdi.mp3";

	/// <summary>The end-of-game screen's otherwise, a stalemate among it.</summary>
	public const string Defeat = "music/diesiraeintro.mp3";

	/// <summary>
	/// Whether a track of <c>music/</c> may be played from the list used where no song is declared: every <c>*.mp3</c> but
	/// these three, compared whole (section 2.5).
	/// </summary>
	public static bool IsFallbackTrack(string path)
	{
		return !string.Equals(path, Title, StringComparison.Ordinal) && !string.Equals(path, Victory, StringComparison.Ordinal)
			&& !string.Equals(path, Defeat, StringComparison.Ordinal);
	}

	/// <summary>
	/// The music's loudness in decibels for the settings' <c>master_volume</c> and <c>music_volume</c>, each 0 to 100: their
	/// product as a share, v, set as the original sets it - (v to the fourth root × 10000 − 10000) hundredths of a decibel,
	/// so a share of nothing is −100 dB (section 7).
	/// </summary>
	public static float Decibels(float masterVolume, float musicVolume)
	{
		double share = Math.Clamp(masterVolume / 100.0 * (musicVolume / 100.0), 0.0, 1.0);
		int hundredths = (int)(Math.Sqrt(Math.Sqrt(share)) * 10000.0 - 10000.0);
		return hundredths / 100f;
	}
}
