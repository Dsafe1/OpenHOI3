using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Every <c>animatedmaptext</c> the interface's <c>.gfx</c> files declare, by name - in the same <c>objectTypes</c>
/// blocks as the counter and projection types. See <see cref="CAnimatedMapTextType"/>.
/// </summary>
public sealed class CAnimatedMapTextTypes
{
	private readonly Dictionary<string, CAnimatedMapTextType> _types = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _types.Count;

	public CAnimatedMapTextType? Find(string name)
	{
		return _types.TryGetValue(name, out CAnimatedMapTextType? type) ? type : null;
	}

	public static CAnimatedMapTextTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var types = new CAnimatedMapTextTypes();
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
				foreach (ClausewitzNode node in container.Children.Where(child => child.IsBlock
					&& string.Equals(child.Key, "animatedmaptext", StringComparison.OrdinalIgnoreCase)))
				{
					if (Read(node, file, warn) is { } type)
					{
						// The last declaration wins: files come base layer first.
						types._types[type.Name] = type;
					}
				}
			}
		}
		return types;
	}

	/// <summary>One declaration, every key it leaves out at the original's default.</summary>
	public static CAnimatedMapTextType? Read(ClausewitzNode node, string file, Action<string> warn)
	{
		string name = node.GetString("name");
		if (name.Length == 0)
		{
			warn(CDiagnostics.Located(file, node.Line, "an animatedmaptext has no name and is left out"));
			return null;
		}
		float speed = 0.01f;
		if (node.Get("speed") is { } written
			&& !float.TryParse(written.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
		{
			warn(CDiagnostics.Located(file, written.Line, $"'{written.Value}' is no speed; the original's 0.01 stands"));
			speed = 0.01f;
		}
		ClausewitzNode? block = node.Children.FirstOrDefault(child => child.IsBlock
			&& string.Equals(child.Key, "textblock", StringComparison.OrdinalIgnoreCase));
		return new CAnimatedMapTextType
		{
			Name = name,
			Speed = speed,
			Color = Colour(block?.Get("color")),
		};
	}

	/// <summary>A colour written as a list of three or four parts, one above 1 a part of 255 as the original reads it; white with none.</summary>
	private static CColor Colour(ClausewitzNode? list)
	{
		var parts = new List<float>();
		if (list is { IsBlock: true })
		{
			foreach (ClausewitzNode child in list.Children)
			{
				if (float.TryParse(child.Value ?? child.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out float part))
				{
					parts.Add(part > 1f ? part / 255f : part);
				}
			}
		}
		return parts.Count >= 3 ? new CColor(parts[0], parts[1], parts[2], parts.Count > 3 ? parts[3] : 1f) : CColor.White;
	}
}
