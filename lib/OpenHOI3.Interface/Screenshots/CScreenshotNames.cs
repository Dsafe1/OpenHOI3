using System;
using System.Globalization;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Screenshots;

/// <summary>
/// The names of the two pictures a game saves (<c>docs/mechanics/screenshots.md</c>, sections 2 and 3), below the
/// player's folder: <c>Screenshots/HoI3_&lt;n&gt;.bmp</c> for the screen and
/// <c>Screenshots/HoI3_MAP_&lt;tag&gt;[mp]_&lt;year&gt;.&lt;month&gt;.&lt;day&gt;.&lt;hour&gt;_&lt;n&gt;.bmp</c> for the map, n the first from 1
/// whose file is not there, and none past 9999.
/// </summary>
public static class CScreenshotNames
{
	public const string Folder = "Screenshots";
	public const string ScreenStem = "HoI3_";
	public const string Extension = ".bmp";

	/// <summary>One past the last number tried.</summary>
	public const int Numbers = 10000;

	/// <summary>The map picture's name up to its number: the player's tag, <c>mp</c> between machines, and the date with no leading noughts.</summary>
	public static string MapStem(string tag, bool multiplayer, CDate date, int hour)
	{
		return "HoI3_MAP_" + tag + (multiplayer ? "mp" : "") + "_"
			+ string.Create(CultureInfo.InvariantCulture, $"{date.Year}.{date.Month}.{date.Day}.{hour}") + "_";
	}

	/// <summary>The first name of a stem that is not taken, or null where all 9999 are.</summary>
	public static string? First(string stem, Func<string, bool> taken)
	{
		for (int number = 1; number < Numbers; number++)
		{
			string name = stem + number.ToString(CultureInfo.InvariantCulture) + Extension;
			if (!taken(name))
			{
				return name;
			}
		}
		return null;
	}
}
