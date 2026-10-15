using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Every <c>billboardType</c> the interface's <c>.gfx</c> files declare, by name. They
/// sit in the same <c>objectTypes</c> blocks as the counter types.
/// </summary>
public sealed class CBillboardTypes
{
	/// <summary>The pointer beside the counter of a unit on the move.</summary>
	public const string CounterArrow = "counter_arrow";

	/// <summary>The marker of a battle, as it is from further off and from close by.</summary>
	public const string CombatStatus = "combat_status";
	public const string CombatStatusClose = "combat_status_close";

	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "effectFile", "textureFile", "scale", "offset", "offset2", "noOfFrames", "font", "font_size",
		// A weatherBillboardType's own: the original stores the last two and nothing reads them.
		"effect", "animation", "simple",
	};

	private readonly Dictionary<string, CBillboardType> _types = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _types.Count;

	public CBillboardType? Find(string name)
	{
		return _types.TryGetValue(name, out CBillboardType? type) ? type : null;
	}

	public static CBillboardTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var types = new CBillboardTypes();
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e)
			{
				warn($"Could not parse '{file}': {e.Message}");
				continue;
			}
			foreach (ClausewitzNode container in root.All("objectTypes"))
			{
				foreach (ClausewitzNode node in container.Children.Where(child => child.IsBlock
					&& (string.Equals(child.Key, "billboardType", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(child.Key, "weatherBillboardType", StringComparison.OrdinalIgnoreCase))))
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

	private static CBillboardType? Read(ClausewitzNode node, string file, Action<string> warn)
	{
		string name = node.GetString("name");
		if (name.Length == 0)
		{
			warn($"{file}: a billboardType has no name and is left out");
			return null;
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && !Keys.Contains(child.Key))
			{
				warn($"{file}: billboardType '{name}' has a key nothing reads: {child.Key}");
			}
		}
		return new CBillboardType
		{
			Name = name,
			TextureFile = node.GetString("textureFile"),
			Scale = node.GetFloat("scale", 1f),
			Offset = node.GetFloat("offset"),
			Frames = Math.Max(1, node.GetInt("noOfFrames", 1)),
			Font = node.GetString("font"),
			TextureFiles = node.All("textureFile").Select(texture => texture.Value ?? "").Where(path => path.Length > 0).ToList(),
			Effect = node.GetString("effect"),
		};
	}
}
