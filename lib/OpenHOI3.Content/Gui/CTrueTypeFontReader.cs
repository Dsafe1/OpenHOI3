using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// Reads the <c>font = { }</c> entries of the <c>fonts</c> blocks of every <c>interface/*.gfx</c>, by name, a later
/// declaration winning as with the bitmap fonts. Only <c>name</c>, <c>fontname</c>, <c>height</c> and <c>color</c> are taken;
/// whether the original reads <c>charset</c> was not established (<c>reversing/findings/FINDINGS-loadingscreen.md</c>, 6).
/// </summary>
public static class CTrueTypeFontReader
{
	public static Dictionary<string, CTrueTypeFont> Read(CDirectorySettings directories)
	{
		var fonts = new Dictionary<string, CTrueTypeFont>(StringComparer.Ordinal);
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception)
			{
				continue;                             // reported by the sprite pass
			}
			Read(root, fonts);
		}
		return fonts;
	}

	/// <summary>One file's entries, added over what is there.</summary>
	public static void Read(ClausewitzNode root, Dictionary<string, CTrueTypeFont> into)
	{
		foreach (ClausewitzNode container in root.All("fonts"))
		{
			foreach (ClausewitzNode entry in container.All("font"))
			{
				string name = entry.GetString("name");
				if (name.Length == 0)
				{
					continue;
				}
				into[name] = new CTrueTypeFont(name, entry.GetString("fontname"), entry.GetInt("height"),
					entry.GetColor("color", CColor.White));
			}
		}
	}
}
