using System.Numerics;

namespace OpenHOI3.Interface.Loading;

/// <summary>
/// Where the loading screen's three things stand on a screen of a size, in pixels from its top left
/// (<c>docs/mechanics/loadingscreen.md</c>, section 2; <c>reversing/findings/FINDINGS-loadingscreen.md</c>, 1 and 2).
/// </summary>
public static class CLoadingLayout
{
	/// <summary>The strip's size: <c>gfx/load_textbg.dds</c>'s own, set in the original's code.</summary>
	public const float StripWidth = 600f;
	public const float StripHeight = 104f;

	/// <summary>The strip's top, above the screen's bottom edge; its bottom stands 4 above it.</summary>
	public const float StripTopFromBottom = 108f;

	/// <summary>The text's box: from 108 above the bottom edge to 8 above it, the screen's whole width.</summary>
	public const float TextTopFromBottom = 108f;
	public const float TextHeight = 100f;

	/// <summary>
	/// The picture: a square whose side is the screen's width - the original takes its width's half, in whole pixels, for
	/// both half extents - centred, so that a wide screen cuts the picture equally top and bottom.
	/// </summary>
	public static (Vector2 Position, Vector2 Size) Picture(float width, float height)
	{
		float half = (int)width / 2;
		return (new Vector2(width / 2f - half, height / 2f - half), new Vector2(2f * half, 2f * half));
	}

	/// <summary>The strip, centred across.</summary>
	public static (Vector2 Position, Vector2 Size) Strip(float width, float height)
	{
		return (new Vector2(width / 2f - StripWidth / 2f, height - StripTopFromBottom), new Vector2(StripWidth, StripHeight));
	}

	/// <summary>The box the line is centred in, both ways.</summary>
	public static (Vector2 Position, Vector2 Size) Text(float width, float height)
	{
		return (new Vector2(0f, height - TextTopFromBottom), new Vector2(width, TextHeight));
	}
}
