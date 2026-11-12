namespace OpenHOI3.Content.Sound;

/// <summary>
/// One sound of <c>interface/sound.sfx</c>: its name, the key it is declared under, and its five keys as the original reads
/// them (<c>reversing/findings/FINDINGS-soundeffects.md</c>, section 2; <c>docs/mechanics/sound.md</c>).
/// </summary>
/// <param name="Name">The key it is declared under; looked up without case.</param>
/// <param name="Path">The wav, game-relative: <c>sound/</c> and its <c>file</c>.</param>
/// <param name="Pitch">A playback rate in Hz, 100 to 100000, or -1 where none is given.</param>
/// <param name="Pan">-10000 to 10000, 0 where none is given.</param>
/// <param name="Single">One voice, restarted by every play, where <c>voicetype = single</c>; else 64.</param>
/// <param name="Volume">A hundredth of the sound's base volume: 100 where none is given.</param>
public sealed record CSoundEffect(string Name, string Path, int Pitch, int Pan, bool Single, int Volume)
{
	/// <summary>The voices a sound has where its <c>voicetype</c> is not <c>single</c> [exe].</summary>
	public const int Voices = 64;
}
