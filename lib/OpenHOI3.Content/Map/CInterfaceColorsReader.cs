using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Map;

/// <summary>
/// <c>interface/colors.txt</c>: the colours the region map mode hands out to the regions and the continents, every
/// <c>color = { r g b }</c> in the file's order, read as an interface colour is - each part over one divided by 255 on its
/// own - and anything else passed over (<c>reversing/findings/FINDINGS-mapmodesrest.md</c>, section 2). Empty where the
/// install has no such file.
/// </summary>
public static class CInterfaceColorsReader
{
	public const string RelativePath = "interface/colors.txt";

	public static List<CColor> Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		var colours = new List<CColor>();
		string path = directories.Resolve(RelativePath);
		if (!File.Exists(path))
		{
			return colours;
		}
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			(warn ?? CDiagnostics.Problem)($"Could not parse '{path}': {e.Message}");
			return colours;
		}
		var missing = new CColor(-1f, -1f, -1f, -1f);
		foreach (ClausewitzNode child in root.Children)
		{
			if (!string.Equals(child.Key, "color", StringComparison.Ordinal) || !child.IsBlock)
			{
				continue;
			}
			// The child is the colour's own block: read it through a wrapper holding just that key.
			var holder = new ClausewitzNode();
			holder.Children.Add(child);
			CColor colour = holder.GetInterfaceColor("color", missing);
			if (colour != missing)
			{
				colours.Add(colour);
			}
		}
		return colours;
	}
}
