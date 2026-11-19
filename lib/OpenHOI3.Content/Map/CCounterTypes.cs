using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Every <c>CounterType</c> the interface's <c>.gfx</c> files declare, by name. They sit
/// in an <c>objectTypes</c> block beside the map's other things - billboards,
/// projections, flags - which nothing here reads yet.
/// </summary>
public sealed class CCounterTypes
{
	/// <summary>The counter of a far view, and of a near one.</summary>
	public const string Far = "unit_counter";
	public const string Near = "unit_counter_close";

	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "effect", "effectFile", "textureFile", "textureFile2", "textureFile3", "textureFile4",
		"noOfFrames", "scale", "size", "font", "shieldtype", "offset", "offset2", "offset3", "color1", "color2",
		"delayAfterAttackColor", "level_scale",
	};

	private readonly Dictionary<string, CCounterType> _types = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _types.Count;

	public CCounterType? Find(string name)
	{
		return _types.TryGetValue(name, out CCounterType? type) ? type : null;
	}

	public static CCounterTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var types = new CCounterTypes();
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e)
			{
				warn(CDiagnostics.Located(file, 0, $"could not be parsed: {e.Message}"));
				continue;
			}
			foreach (ClausewitzNode container in root.All("objectTypes"))
			{
				foreach (ClausewitzNode node in container.All("CounterType"))
				{
					if (Read(node, Path.GetFileName(file), warn) is { } type)
					{
						// The last declaration wins: files come base layer first.
						types._types[type.Name] = type;
					}
				}
			}
		}
		return types;
	}

	private static CCounterType? Read(ClausewitzNode node, string file, Action<string> warn)
	{
		string name = node.GetString("name");
		if (name.Length == 0)
		{
			warn(CDiagnostics.Located(file, node.Line, "a CounterType has no name and is left out"));
			return null;
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && !Keys.Contains(child.Key))
			{
				warn(CDiagnostics.Located(file, child.Line, $"CounterType '{name}' has a key nothing reads: {child.Key}"));
			}
		}

		Vector3 flag = List(node, "shieldtype");
		return new CCounterType
		{
			Name = name,
			File = file,
			Line = node.Line,
			BackgroundFile = node.GetString("textureFile"),
			MaskFile = node.GetString("textureFile2"),
			SizeFile = node.GetString("textureFile3"),
			ArrowFile = node.GetString("textureFile4"),
			Frames = Math.Max(1, node.GetInt("noOfFrames", 1)),
			Scale = node.GetFloat("scale", 1f),
			LevelScale = node.GetFloat("level_scale"),
			Font = node.GetString("font"),
			TextSize = node.GetFloat("size"),
			HasFlag = node.Get("shieldtype") is not null,
			FlagWidth = flag.X,
			FlagHeight = flag.Y,
			FlagOffset = List(node, "offset"),
			FiguresOffset = List(node, "offset2"),
			NameOffset = List(node, "offset3"),
			Color1 = Colour(node, "color1"),
			Color2 = Colour(node, "color2"),
		};
	}

	/// <summary>Up to three numbers written as a plain list: <c>{ 2.5 0.0 -5.0 }</c>.</summary>
	private static Vector3 List(ClausewitzNode node, string key)
	{
		Span<float> parts = stackalloc float[4];
		Numbers(node.Get(key), parts);
		return new Vector3(parts[0], parts[1], parts[2]);
	}

	/// <summary>Four numbers from 0 to 1: red, green, blue and how solid.</summary>
	private static CColor Colour(ClausewitzNode node, string key)
	{
		Span<float> parts = stackalloc float[4];
		parts[3] = 1f;
		return Numbers(node.Get(key), parts) >= 3 ? new CColor(parts[0], parts[1], parts[2], parts[3]) : CColor.White;
	}

	private static int Numbers(ClausewitzNode? list, Span<float> into)
	{
		int found = 0;
		if (list is null || !list.IsBlock)
		{
			return found;
		}
		foreach (ClausewitzNode child in list.Children)
		{
			if (found < into.Length && child.Value is not null
				&& float.TryParse(child.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				into[found++] = value;
			}
		}
		return found;
	}
}
