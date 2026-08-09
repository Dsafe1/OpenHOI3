using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Core;

namespace OpenHOI3.Gfx;

/// <summary>
/// A sprite declaration from a <c>.gfx</c> file: the <c>GFX_*</c> name the GUI refers
/// to, plus the texture backing it.
/// </summary>
public sealed class CSpriteType
{
	/// <summary>The declaring key, e.g. <c>spriteType</c> or <c>corneredTileSpriteType</c>.</summary>
	public string Kind { get; init; } = "spriteType";

	public string Name { get; init; } = "";

	/// <summary>Game-relative, as written in the file.</summary>
	public string TextureFile { get; init; } = "";

	/// <summary>
	/// Horizontal strip count. Button states use a shader rather than frames in this
	/// game, so this is almost always 1.
	/// </summary>
	public int FrameCount { get; init; } = 1;

	/// <summary>Cell size of a cornered tile; zero for ordinary sprites.</summary>
	public Vector2 TileSize { get; init; }

	public bool IsTiled => Kind.Equals("corneredTileSpriteType", StringComparison.OrdinalIgnoreCase);

	public static CSpriteType FromNode(string kind, ClausewitzNode node)
	{
		return new CSpriteType
		{
			Kind = kind,
			Name = node.GetString("name"),
			TextureFile = node.GetString("textureFile"),
			FrameCount = Math.Max(1, node.GetInt("noOfFrames", 1)),
			TileSize = node.GetVector2("size"),
		};
	}
}

/// <summary>Every sprite declared across the game's <c>.gfx</c> files, by name.</summary>
public sealed class CSpriteTypes
{
	private static readonly string[] SpriteKinds =
	{
		"spriteType", "textSpriteType", "corneredTileSpriteType", "maskedShieldType",
		"progressbartype", "tileSpriteType", "LineChartType", "PieChartType",
	};

	private readonly Dictionary<string, CSpriteType> _sprites =
		new(StringComparer.OrdinalIgnoreCase);

	public int Count => _sprites.Count;

	public static CSpriteTypes Load(CDirectorySettings directories)
	{
		var registry = new CSpriteTypes();
		foreach (string file in directories.ListFiles("interface", "*.gfx"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e)
			{
				GD.PushWarning($"Could not parse '{file}': {e.Message}");
				continue;
			}
			foreach (ClausewitzNode container in root.All("spriteTypes"))
			{
				registry.AddFrom(container);
			}
		}
		return registry;
	}

	private void AddFrom(ClausewitzNode container)
	{
		foreach (ClausewitzNode child in container.Children)
		{
			if (child.Key is null || !child.IsBlock)
			{
				continue;
			}
			if (Array.IndexOf(SpriteKinds, child.Key) < 0 &&
				!child.Key.EndsWith("SpriteType", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			CSpriteType sprite = CSpriteType.FromNode(child.Key, child);
			if (sprite.Name.Length == 0 || sprite.TextureFile.Length == 0)
			{
				continue;
			}
			// Last declaration wins. Files arrive base layer first, so an expansion
			// redefining a sprite replaces the base version.
			_sprites[sprite.Name] = sprite;
		}
	}

	public CSpriteType? Find(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		return _sprites.TryGetValue(name, out CSpriteType? sprite) ? sprite : null;
	}
}
