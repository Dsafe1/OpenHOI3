using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Works out where each province "is": the centre of its bounding box, unless
/// <c>positions.txt</c> places it by hand. The rule is the original's, in
/// <c>docs/mechanics/map.md</c>, section 4:
///
///     the default is the centre of the bounding box;
///     unit = { x y } overwrites it, cut to whole pixels, on any province;
///     text_position = { x y } overwrites it, cut, on a sea zone only;
///     the last one in file order wins.
///
/// The file is six megabytes of which those two keys are a small part, so it is
/// walked once by a reader of its own that keeps nothing else: the general parser
/// would build a million nodes to throw away.
/// </summary>
public static class CPositionsReader
{
	private const string Unit = "unit";
	private const string TextPosition = "text_position";

	public static CMapPosition[] Read(string path, IReadOnlyList<CProvinceShape> shapes,
		Func<int, bool> isSea, Action<string> warn)
	{
		var positions = new CMapPosition[shapes.Count];
		for (int id = 0; id < shapes.Count; id++)
		{
			positions[id] = shapes[id].IsEmpty ? default : new CMapPosition(shapes[id].CentreX, shapes[id].CentreY);
		}
		if (!File.Exists(path))
		{
			warn($"no positions: '{path}' is missing, and every province is at the centre of its bounding box");
			return positions;
		}

		byte[] bytes = File.ReadAllBytes(path);
		int at = 0;
		int depth = 0;
		int province = 0;
		string key = "";
		string name = "";
		bool afterEquals = false;
		float x = 0f;
		float y = 0f;
		bool hasX = false;
		bool hasY = false;
		int unknown = 0;

		while (NextToken(bytes, ref at, out int start, out int length))
		{
			byte first = bytes[start];
			if (length == 1 && first == (byte)'{')
			{
				depth++;
				afterEquals = false;
				if (depth == 2)
				{
					hasX = hasY = false;
				}
				continue;
			}
			if (length == 1 && first == (byte)'}')
			{
				if (depth == 2 && hasX && hasY && province > 0)
				{
					bool applies = key == Unit || (key == TextPosition && isSea(province));
					if (applies)
					{
						positions[province] = new CMapPosition((int)x, (int)y);
					}
				}
				depth = Math.Max(0, depth - 1);
				afterEquals = false;
				continue;
			}
			if (length == 1 && first == (byte)'=')
			{
				afterEquals = true;
				continue;
			}

			string text = System.Text.Encoding.Latin1.GetString(bytes, start, length);
			if (depth == 0)
			{
				if (!afterEquals)
				{
					bool known = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out province)
						&& province > 0 && province < positions.Length;
					if (!known)
					{
						province = 0;
						unknown++;
					}
				}
			}
			else if (depth == 1)
			{
				if (!afterEquals)
				{
					key = text;
				}
			}
			else if (depth == 2 && (key == Unit || key == TextPosition))
			{
				if (!afterEquals)
				{
					name = text;
				}
				else if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
				{
					if (name == "x")
					{
						x = value;
						hasX = true;
					}
					else if (name == "y")
					{
						y = value;
						hasY = true;
					}
				}
			}
			afterEquals = false;
		}

		if (unknown > 0)
		{
			warn($"'{path}': {unknown} blocks are not for a province of the map");
		}
		return positions;
	}

	/// <summary>
	/// The next word of the file: a brace, an equals sign, or a run of anything else. A
	/// <c>#</c> starts a comment that runs to the end of its line.
	/// </summary>
	private static bool NextToken(byte[] bytes, ref int at, out int start, out int length)
	{
		while (at < bytes.Length)
		{
			byte c = bytes[at];
			if (c == (byte)'#')
			{
				while (at < bytes.Length && bytes[at] != (byte)'\n')
				{
					at++;
				}
			}
			else if (c <= (byte)' ')
			{
				at++;
			}
			else
			{
				break;
			}
		}
		start = at;
		length = 0;
		if (at >= bytes.Length)
		{
			return false;
		}
		if (IsPunctuation(bytes[at]))
		{
			at++;
			length = 1;
			return true;
		}
		while (at < bytes.Length && bytes[at] > (byte)' ' && !IsPunctuation(bytes[at]) && bytes[at] != (byte)'#')
		{
			at++;
		}
		length = at - start;
		return true;
	}

	private static bool IsPunctuation(byte c)
	{
		return c == (byte)'{' || c == (byte)'}' || c == (byte)'=';
	}
}
