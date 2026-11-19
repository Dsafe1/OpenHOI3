using OpenHOI3.Content.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Text;

/// <summary>
/// The colour an escape letter gives what follows, as the original's font draws it: <c>§!</c> back to the font's own
/// colour; any other letter the font's <c>color_override</c> colour for it, else the shared <c>colorcodes</c> table's,
/// else **no change** - the letter is swallowed and the colour in force stays (<c>docs/mechanics/interface.md</c>,
/// section 6; <c>reversing/findings/FINDINGS-fontterrain.md</c>, section 1).
/// </summary>
public static class CTextColours
{
	/// <summary>The letter that goes back to the font's own colour.</summary>
	public const char Reset = '!';

	/// <summary>Whether a run is in the font's own colour: before any escape, or after <c>§!</c>.</summary>
	public static bool IsPlain(char letter)
	{
		return letter == CColouredText.NoColour || letter == Reset;
	}

	/// <summary>The colour a letter stands for, or false where neither table has it and the colour does not change.</summary>
	public static bool TryGet(char letter, CFontColourTable? own, CFontColourTable? shared, out CColor colour)
	{
		if (own is not null && own.TryGet(letter, out colour))
		{
			return true;
		}
		if (shared is not null && shared.TryGet(letter, out colour))
		{
			return true;
		}
		colour = CColor.White;
		return false;
	}
}
