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
				CDiagnostics.Problem(file, 0, $"Could not parse: {e.Message}");
				continue;
			}
			foreach (ClausewitzNode container in root.All("spriteTypes"))
			{
				registry.AddFrom(container, file);
			}
		}
		return registry;
	}

	private void AddFrom(ClausewitzNode container, string file)
	{
		foreach (ClausewitzNode child in container.Children)
		{
			// Each passed over is said (docs/mechanics/clausewitz.md, section 3).
			if (child.Key is null || !child.IsBlock)
			{
				CDiagnostics.Problem(file, child.Line, $"spriteTypes: '{child.Key ?? child.Value}' is no sprite - a kind and a block; passed over");
				continue;
			}
			if (Array.IndexOf(SpriteKinds, child.Key) < 0 &&
				!child.Key.EndsWith("SpriteType", StringComparison.OrdinalIgnoreCase))
			{
				CDiagnostics.Problem(file, child.Line, $"spriteTypes: '{child.Key}' is no kind of sprite read here; passed over");
				continue;
			}
			CSpriteType sprite = CSpriteType.FromNode(child.Key, child, file);
			// A pie chart and a progress bar may be drawn with no picture at all: the politics screen's are.
			if (sprite.Name.Length == 0 || (sprite.TextureFile.Length == 0 && sprite.OverlayFile.Length == 0
				&& !sprite.IsPieChart && !sprite.IsProgressBar && !sprite.IsLineChart))
			{
				CDiagnostics.Problem(file, child.Line, $"the {child.Key} '{sprite.Name}' has " +
					(sprite.Name.Length == 0 ? "no name" : "no texture") + "; passed over");
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
		if (_sprites.TryGetValue(name, out CSpriteType? sprite))
		{
			return sprite;
		}
		// A built-in element's picture is no file's: a plain sprite whose texture is its own name, which the texture
		// manager paints (docs/mechanics/builtin.md).
		if (OpenHOI3.Content.Gui.CBuiltInSprite.FromName(name) is not null)
		{
			return _sprites[name] = new CSpriteType { Name = name, TextureFile = name };
		}
		return null;
	}
}
