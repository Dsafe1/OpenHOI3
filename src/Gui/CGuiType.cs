using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Core;

namespace OpenHOI3.Gui;

/// <summary>
/// Anchor corner an element positions itself from, within its parent window.
/// Positions in the game files are offsets from this point, and may be negative.
/// </summary>
public enum EOrientation
{
	UpperLeft,
	UpperRight,
	LowerLeft,
	LowerRight,
	Center,
	CenterUp,
	CenterDown,
}

/// <summary>Base of the parsed <c>.gui</c> element types.</summary>
public abstract class CGuiType
{
	public string Name { get; init; } = "";
	public Vector2 Position { get; init; }
	public EOrientation Orientation { get; init; }

	public static EOrientation ParseOrientation(string raw)
	{
		return raw.Replace("_", "").ToUpperInvariant() switch
		{
			"UPPERRIGHT" => EOrientation.UpperRight,
			"LOWERLEFT" => EOrientation.LowerLeft,
			"LOWERRIGHT" => EOrientation.LowerRight,
			"CENTER" or "CENTRE" => EOrientation.Center,
			"CENTERUP" or "CENTREUP" => EOrientation.CenterUp,
			"CENTERDOWN" or "CENTREDOWN" => EOrientation.CenterDown,
			_ => EOrientation.UpperLeft,
		};
	}

	/// <summary>Builds the element for a node, or null for kinds not yet supported.</summary>
	public static CGuiType? FromNode(ClausewitzNode node)
	{
		if (node.Key is null)
		{
			return null;
		}
		string name = node.GetString("name");
		Vector2 position = node.GetVector2("position");
		EOrientation orientation = ParseOrientation(node.GetString("orientation"));

		switch (node.Key.ToLowerInvariant())
		{
			case "windowtype":
				var window = new CWindowType
				{
					Name = name,
					Position = position,
					Orientation = orientation,
					Size = node.GetVector2("size"),
					Background = node.GetString("backGround"),
					FullScreen = node.GetBool("fullscreen"),
				};
				foreach (ClausewitzNode child in node.Children)
				{
					CGuiType? element = FromNode(child);
					if (element is not null)
					{
						window.Children.Add(element);
					}
				}
				return window;

			case "guibuttontype":
			case "checkboxtype":
				return new CButtonType
				{
					Name = name,
					Position = position,
					Orientation = orientation,
					SpriteName = node.GetString("quadTextureSprite"),
					ButtonText = node.GetString("buttonText"),
					ButtonFont = node.GetString("buttonFont"),
				};

			case "icontype":
				return new CIconType
				{
					Name = name,
					Position = position,
					Orientation = orientation,
					SpriteName = node.GetString("spriteType"),
					Frame = node.GetInt("frame", 0),
				};

			case "instanttextboxtype":
			case "textboxtype":
				return new CTextBoxType
				{
					Name = name,
					Position = position,
					Orientation = orientation,
					FontName = node.GetString("font"),
					Text = node.GetString("text"),
					Format = node.GetString("format", "left"),
					MaxSize = new Vector2(node.GetFloat("maxWidth", 100f),
						node.GetFloat("maxHeight", 20f)),
				};

			default:
				return null;                          // listbox, editbox, ... not needed yet
		}
	}
}

/// <summary>A <c>windowType</c>: a positioned container with child elements.</summary>
public sealed class CWindowType : CGuiType
{
	public Vector2 Size { get; init; }

	/// <summary>Name of the child element drawn stretched behind the rest, if any.</summary>
	public string Background { get; init; } = "";

	public bool FullScreen { get; init; }

	public List<CGuiType> Children { get; } = new();

	public CWindowType? FindWindow(string name)
	{
		foreach (CGuiType child in Children)
		{
			if (child is CWindowType window)
			{
				if (string.Equals(window.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return window;
				}
				CWindowType? nested = window.FindWindow(name);
				if (nested is not null)
				{
					return nested;
				}
			}
		}
		return null;
	}
}

public sealed class CButtonType : CGuiType
{
	public string SpriteName { get; init; } = "";

	/// <summary>Localisation key, not display text.</summary>
	public string ButtonText { get; init; } = "";

	public string ButtonFont { get; init; } = "";
}

public sealed class CIconType : CGuiType
{
	public string SpriteName { get; init; } = "";
	public int Frame { get; init; }
}

public sealed class CTextBoxType : CGuiType
{
	public string FontName { get; init; } = "";
	public string Text { get; init; } = "";
	public string Format { get; init; } = "left";
	public Vector2 MaxSize { get; init; }
}

/// <summary>Top-level contents of one parsed <c>.gui</c> file.</summary>
public sealed class CGuiFile
{
	public List<CGuiType> Elements { get; } = new();

	public static CGuiFile ParseFile(string absolutePath)
	{
		var file = new CGuiFile();
		ClausewitzNode root = ClausewitzParser.ParseFile(absolutePath);
		foreach (ClausewitzNode container in root.All("guiTypes"))
		{
			foreach (ClausewitzNode child in container.Children)
			{
				CGuiType? element = CGuiType.FromNode(child);
				if (element is not null)
				{
					file.Elements.Add(element);
				}
			}
		}
		return file;
	}

	public CWindowType? FindWindow(string name)
	{
		foreach (CGuiType element in Elements)
		{
			if (element is CWindowType window)
			{
				if (string.Equals(window.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return window;
				}
				CWindowType? nested = window.FindWindow(name);
				if (nested is not null)
				{
					return nested;
				}
			}
		}
		return null;
	}
}
