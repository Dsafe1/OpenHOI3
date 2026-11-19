using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Reads <c>map/trees.txt</c> (default.map's <c>tree_definition</c>): one tree type for each block, in the file's order, its
/// <c>texture</c> and its <c>tree_density</c> read as the original reads them - a number in thousandths, over a thousand,
/// 1.0 where it is not given (<c>reversing/findings/FINDINGS-mapdraw.md</c>, C.8). A key that is neither is reported.
/// </summary>
public static class CTreeReader
{
	/// <summary>The file's tree types; none, and nothing said, where there is no file - a map without trees is a map.</summary>
	public static List<CTreeType> Read(string path, Action<string> warn)
	{
		var types = new List<CTreeType>();
		if (!File.Exists(path))
		{
			return types;
		}
		foreach (ClausewitzNode entry in ClausewitzParser.ParseFile(path).Children)
		{
			if (entry.Key is null || !entry.IsBlock)
			{
				continue;
			}
			float density = 1f;
			string texture = "";
			foreach (ClausewitzNode key in entry.Children)
			{
				switch (key.Key)
				{
					case "texture":
						texture = key.Value ?? "";
						break;
					case "tree_density":
						if (CFixedPoint.TryParse(key.Value, out CFixedPoint value))
						{
							density = (float)(value.Raw / 1000.0);
						}
						break;
					default:
						warn(CDiagnostics.Located(path, key.Line,
							$"'{key.Key ?? key.Value}' is a key no tree type has, which is not read"));
						break;
				}
			}
			types.Add(new CTreeType(entry.Key, texture, density));
		}
		return types;
	}
}
