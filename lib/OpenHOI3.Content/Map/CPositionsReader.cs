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
/// Two more points are kept beside that one: where the province's name is written,
/// which is <c>text_position</c> on any province, and where its port is drawn, which
/// is <c>naval_base</c> inside <c>building_position</c>. Both start at the centre too.
/// And every building <c>building_position</c> places, as the file gives it, with the
/// ports once more in the order the file names them.
///
/// The file is six megabytes of which those keys are a small part, so it is walked
/// once by a reader of its own that keeps nothing else: the general parser would build
/// a million nodes to throw away.
/// </summary>
public static class CPositionsReader
{
	private const string Unit = "unit";
	private const string TextPosition = "text_position";
	private const string TextRotation = "text_rotation";
	private const string TextScale = "text_scale";
	private const string BuildingPosition = "building_position";
	private const string NavalBase = "naval_base";

	public static CMapPositions Read(string path, IReadOnlyList<CProvinceShape> shapes,
		Func<int, bool> isSea, Action<string> warn)
	{
		var unit = new CMapPosition[shapes.Count];
		for (int id = 0; id < shapes.Count; id++)
		{
			unit[id] = shapes[id].IsEmpty ? default : new CMapPosition(shapes[id].CentreX, shapes[id].CentreY);
		}
		var text = (CMapPosition[])unit.Clone();
		var navalBase = (CMapPosition[])unit.Clone();
		var buildings = new CBuildingPosition[shapes.Count][];
		Array.Fill(buildings, Array.Empty<CBuildingPosition>());
		// A name stands two map pixels south of the centre of the bounding box where the file gives no text_position: the
		// original sets that point before it reads the file (CProvinceLabel.FallbackSouth).
		var labels = new CProvinceLabel[shapes.Count];
		for (int id = 0; id < shapes.Count; id++)
		{
			labels[id] = CProvinceLabel.Default with { X = unit[id].X, Y = unit[id].Y - CProvinceLabel.FallbackSouth };
		}
		if (!File.Exists(path))
		{
			warn($"no positions: '{path}' is missing, and every province is at the centre of its bounding box");
			return new CMapPositions(unit, text, navalBase, buildings, Array.Empty<CPortPlacement>()) { Labels = labels };
		}
		var placed = new List<CBuildingPosition>?[shapes.Count];
		var ports = new List<CPortPlacement>();

		byte[] bytes = File.ReadAllBytes(path);
		int at = 0;
		int depth = 0;
		int province = 0;
		string key = "";
		string building = "";
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
				if (depth == 2 || depth == 3)
				{
					hasX = hasY = false;
				}
				continue;
			}
			if (length == 1 && first == (byte)'}')
			{
				if (hasX && hasY && province > 0)
				{
					var point = new CMapPosition((int)x, (int)y);
					if (depth == 2 && key == Unit)
					{
						unit[province] = point;
					}
					else if (depth == 2 && key == TextPosition)
					{
						text[province] = point;
						labels[province] = labels[province] with { X = x, Y = y, Given = true };
						if (isSea(province))
						{
							unit[province] = point;
						}
					}
					else if (depth == 3 && key == BuildingPosition)
					{
						(placed[province] ??= new List<CBuildingPosition>()).Add(new CBuildingPosition(building, x, y));
						if (building == NavalBase)
						{
							navalBase[province] = point;
							ports.Add(new CPortPlacement(province, x, y));
						}
					}
				}
				hasX = hasY = false;
				depth = Math.Max(0, depth - 1);
				afterEquals = false;
				continue;
			}
			if (length == 1 && first == (byte)'=')
			{
				afterEquals = true;
				continue;
			}

			string word = System.Text.Encoding.Latin1.GetString(bytes, start, length);
			if (depth == 0)
			{
				if (!afterEquals)
				{
					bool known = int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out province)
						&& province > 0 && province < unit.Length;
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
					key = word;
				}
				else if (province > 0 && (key == TextRotation || key == TextScale)
					&& float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
				{
					labels[province] = key == TextRotation ? labels[province] with { Rotation = value }
						: labels[province] with { Scale = value };
				}
			}
			else if (depth == 2 && key == BuildingPosition)
			{
				if (!afterEquals)
				{
					building = word;
				}
			}
			else if ((depth == 2 && (key == Unit || key == TextPosition))
				|| (depth == 3 && key == BuildingPosition))
			{
				if (!afterEquals)
				{
					name = word;
				}
				else if (float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
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
		for (int id = 0; id < placed.Length; id++)
		{
			if (placed[id] is { } list)
			{
				buildings[id] = list.ToArray();
			}
		}
		return new CMapPositions(unit, text, navalBase, buildings, ports.ToArray()) { Labels = labels };
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
