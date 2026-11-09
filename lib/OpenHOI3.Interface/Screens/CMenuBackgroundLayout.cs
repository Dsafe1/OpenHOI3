using System;

namespace OpenHOI3.Interface.Screens;

/// <summary>How large the picture behind a country screen is drawn. See <c>docs/mechanics/countryviews.md</c>, section 6.</summary>
public static class CMenuBackgroundLayout
{
	/// <summary>The larger of the screen's two ratios to the picture, so that it covers the screen, and never below its own size.</summary>
	public static float Scale(float screenWidth, float screenHeight, float pictureWidth, float pictureHeight)
	{
		if (pictureWidth <= 0f || pictureHeight <= 0f)
		{
			return 1f;
		}
		return Math.Max(1f, Math.Max(screenWidth / pictureWidth, screenHeight / pictureHeight));
	}
}
