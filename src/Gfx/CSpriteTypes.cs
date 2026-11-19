using System;
using System.Collections.Generic;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Gfx;

/// <summary>Every sprite declared across the game's <c>.gfx</c> files, by name.</summary>
public sealed class CSpriteTypes
{
	private static readonly string[] SpriteKinds =
	{
		"spriteType", "textSpriteType", "corneredTileSpriteType", "maskedShieldType",
		"progressbartype", "tileSpriteType", "LineChartType", "PieChartType", "scrollingSprite",
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
				CDiagnostics.Problem($"Could not parse '{file}': {e.Message}");
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
			// A pie chart and a progress bar may be drawn with no picture at all: the politics screen's are.
			if (sprite.Name.Length == 0 || (sprite.TextureFile.Length == 0 && sprite.OverlayFile.Length == 0
				&& !sprite.IsPieChart && !sprite.IsProgressBar && !sprite.IsLineChart))
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
