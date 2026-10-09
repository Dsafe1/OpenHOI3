using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Every <c>arrowType</c> the interface's <c>.gfx</c> files declare, by name. They sit
/// in <c>objectTypes</c> blocks like the counter types.
/// </summary>
public sealed class CArrowTypes
{
	/// <summary>The arrow of a unit marching. The original has others for an attack, a retreat and a redeployment.</summary>
	public const string Move = "moveArrow";

	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "size", "textureFile", "bodytexture", "color", "colortwo", "endAt", "height", "type", "heading",
		"effect",
	};

	private readonly Dictionary<string, CArrowType> _types = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _types.Count;

	public CArrowType? Find(string name)
	{
		return _types.TryGetValue(name, out CArrowType? type) ? type : null;
	}

	public static CArrowTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var types = new CArrowTypes();
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
				foreach (ClausewitzNode node in container.All("arrowType"))
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

	private static CArrowType? Read(ClausewitzNode node, string file, Action<string> warn)
	{
		string name = node.GetString("name");
		if (name.Length == 0)
		{
			warn($"{file}: an arrowType has no name and is left out");
			return null;
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && !Keys.Contains(child.Key))
			{
				warn($"{file}: arrowType '{name}' has a key nothing reads: {child.Key}");
			}
		}
		return new CArrowType
		{
			Name = name,
			TextureFile = node.GetString("textureFile"),
			BodyTextureFile = node.GetString("bodytexture"),
			Size = node.GetFloat("size"),
		};
	}
}
