using OpenHOI3.Sim;

namespace OpenHOI3.Interface;

/// <summary>
/// The colour each escape letter stands for.
///
/// These are ours, and fixed. The original takes each font's from a <c>colorcodes</c>
/// block in the font's own declaration, which a mod can change; that block is not read
/// here. The maintainer looked at the red, green and yellow beside the top bar and
/// kept them, and kept them again on learning where the original's are - for now. See
/// <c>docs/compatibility.md</c>, entry 5.
/// </summary>
public static class CTextColours
{
	public static bool TryGet(char letter, out CColor colour)
	{
		switch (letter)
		{
			case 'R':
				colour = new CColor(1f, 0.2f, 0.2f);
				return true;
			case 'G':
				colour = new CColor(0.1f, 0.8f, 0.1f);
				return true;
			case 'Y':
				colour = new CColor(1f, 0.85f, 0f);
				return true;
			case 'B':
				colour = new CColor(0.25f, 0.5f, 1f);
				return true;
			case 'W':
				colour = CColor.White;
				return true;
			case 'b':
				colour = new CColor(0f, 0f, 0f);
				return true;
			case 'g':
				colour = new CColor(0.69f, 0.69f, 0.69f);
				return true;
			default:
				colour = CColor.White;
				return false;
		}
	}
}
