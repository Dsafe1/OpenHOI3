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

	/// <summary>The key every plain billboard's effect is kept under unless its declaration names another.</summary>
	public const string PlainEffect = "Billboard";

	/// <summary>The effect file a plain billboard draws through unless its declaration names another.</summary>
	public const string PlainEffectFile = "gfx/FX/billboard.fx";

	/// <summary>The technique a plain billboard is drawn with, by name.</summary>
	public const string PlainTechnique = "standard";

	/// <summary>The effect file of the first type read that names each <c>effect</c>.</summary>
	private readonly Dictionary<string, string> _effectFiles = new(StringComparer.Ordinal);

	public int Count => _types.Count;

	/// <summary>
	/// The effect file a type with this <c>effect</c> draws through: the first such type's. The original keeps a weather
	/// type's effect under its <c>effect</c> and not its file, so a second file under one name is never loaded
	/// (<c>reversing/findings/FINDINGS-weathersim.md</c>, C.3.1). Empty where no type names it.
	/// </summary>
	public string EffectFileOf(string effect) => _effectFiles.GetValueOrDefault(effect, "");

	public CBillboardType? Find(string name)
	{
		return _types.TryGetValue(name, out CBillboardType? type) ? type : null;
	}

	public static CBillboardTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
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
				warn(CDiagnostics.Located(file, 0, $"could not be parsed: {e.Message}"));
				continue;
			}
			foreach (ClausewitzNode container in root.All("objectTypes"))
			{
				foreach (ClausewitzNode node in container.Children.Where(child => child.IsBlock
					&& (string.Equals(child.Key, "billboardType", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(child.Key, "weatherBillboardType", StringComparison.OrdinalIgnoreCase))))
				{
					if (Read(node, file, warn) is { } type)
					{
						// The last declaration wins: files come base layer first.
						types._types[type.Name] = type;
						// A plain billboardType starts with the key "Billboard" and the file gfx/FX/billboard.fx, which its
						// declaration's own overwrite (FINDINGS-billboardfx.md, section 1).
						bool plain = string.Equals(node.Key, "billboardType", StringComparison.OrdinalIgnoreCase);
						string key = type.Effect.Length != 0 ? type.Effect : plain ? PlainEffect : "";
						string effectFile = type.EffectFile.Length != 0 ? type.EffectFile : plain ? PlainEffectFile : "";
						if (key.Length != 0 && effectFile.Length != 0)
						{
							types._effectFiles.TryAdd(key, effectFile);
						}
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
			warn(CDiagnostics.Located(file, node.Line, "a billboardType has no name and is left out"));
			return null;
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && !Keys.Contains(child.Key))
			{
				warn(CDiagnostics.Located(file, child.Line, $"billboardType '{name}' has a key nothing reads: {child.Key}"));
			}
		}
		return new CBillboardType
		{
			Name = name,
			File = file,
			Line = node.Line,
			TextureFile = node.GetString("textureFile"),
			Scale = node.GetFloat("scale", 1f),
			Offset = node.GetFloat("offset"),
			Frames = Math.Max(1, node.GetInt("noOfFrames", 1)),
			Font = node.GetString("font"),
			TextureFiles = node.All("textureFile").Select(texture => texture.Value ?? "").Where(path => path.Length > 0).ToList(),
			Effect = node.GetString("effect"),
			EffectFile = node.GetString("effectFile"),
		};
	}
}
