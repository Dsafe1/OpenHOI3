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
	/// <summary>The arrow of a unit marching.</summary>
	public const string Move = "moveArrow";

	/// <summary>The arrow of a unit whose next province an enemy holds.</summary>
	public const string Attack = "attackArrow";

	/// <summary>The arrow of a unit falling back.</summary>
	public const string Retreat = "retreatArrow";

	/// <summary>The arrow of a unit under a strategic redeployment (<c>docs/mechanics/landorders.md</c>, section 8).</summary>
	public const string Strategic = "stratArrow";

	/// <summary>The arrow of a unit landing from the sea, on anybody's ground.</summary>
	public const string Invasion = "unitInvasionArrow";

	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "size", "textureFile", "bodytexture", "color", "colortwo", "endAt", "height", "type", "heading",
		"effect",
	};

	private readonly Dictionary<string, CArrowType> _types = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _types.Count;

	/// <summary>
	/// The effect file every arrow is drawn through: the first type read's <c>effect</c>. The original keeps every
	/// arrow type's effect under one key, so the first one loaded is the one all of them use
	/// (<c>reversing/findings/FINDINGS-movement.md</c>, section 11.7). Empty where no type names one.
	/// </summary>
	public string EffectFile { get; private set; } = "";

	public CArrowType? Find(string name)
	{
		return _types.TryGetValue(name, out CArrowType? type) ? type : null;
	}

	public static CArrowTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
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
				warn(CDiagnostics.Located(file, 0, $"could not be parsed: {e.Message}"));
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
						if (types.EffectFile.Length == 0)
						{
							types.EffectFile = type.Effect;
						}
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
			warn(CDiagnostics.Located(file, node.Line, "an arrowType has no name and is left out"));
			return null;
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && !Keys.Contains(child.Key))
			{
				warn(CDiagnostics.Located(file, child.Line, $"arrowType '{name}' has a key nothing reads: {child.Key}"));
			}
		}
		return new CArrowType
		{
			Name = name,
			TextureFile = node.GetString("textureFile"),
			BodyTextureFile = node.GetString("bodytexture"),
			Size = node.GetFloat("size"),
			Effect = node.GetString("effect"),
		};
	}
}
