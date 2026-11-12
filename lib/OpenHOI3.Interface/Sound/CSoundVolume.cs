using System;

namespace OpenHOI3.Interface.Sound;

/// <summary>
/// How loud the original plays a sound effect: linear in decibels, unlike its music's fourth-root curve
/// (<c>reversing/findings/FINDINGS-soundeffects.md</c>, section 4; <c>docs/mechanics/sound.md</c>).
/// </summary>
public static class CSoundVolume
{
	/// <summary>
	/// The scale the settings give every sound: v = master/100 × effects/100, then 0.5·v + 0.5 held at 1 where v is above
	/// nought, and nought - silence - where it is not [exe; the two settings are the player's settings.txt].
	/// </summary>
	public static float Scale(float masterVolume, float effectsVolume)
	{
		float v = masterVolume * 0.01f * effectsVolume * 0.01f;
		return v <= 0f ? 0f : Math.Min((float)(v * 0.5 + 0.5), 1f);
	}

	/// <summary>
	/// The decibels a sound plays at: its base volume (<c>volume</c> × 0.01) times the scale, as the original's
	/// <c>(int)(effective × 10000) − 10000</c> hundredths of a decibel - 0 at full, -25 at half the settings, -50 just above
	/// nought, -100 at nought. A figure above nought, from a <c>volume</c> over 100, is what DirectSound refuses: held at 0.
	/// </summary>
	public static float Decibels(int volume, float scale)
	{
		float effective = volume * 0.01f * scale;
		int hundredths = (int)(effective * 10000f) - 10000;
		return Math.Min(hundredths, 0) / 100f;
	}
}
