using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// Every element an install's <c>interface/*.gui</c> files declare at the top level,
/// by name. See <c>docs/mechanics/interface.md</c>, sections 1 and 2.
///
/// Stricter than the original, which drops what it does not know without a word: a
/// top-level key other than <c>guiTypes</c>, an element keyword nothing knows, a key an
/// element does not take, a key an element gives twice and an orientation that is not
/// one of the seven are each reported.
/// </summary>
public sealed class CGuiTypes
{
	private const string InterfaceDirectory = "interface";

	/// <summary>
	/// Where a mod keeps the <c>.gui</c> files it has for an enhanced game alone, read after the others: a folder of its
	/// own, as everything of this engine's in a mod is. The original does not create a window it has no code for, so what
	/// is declared there is nothing to it even where it reads the file (<c>docs/mechanics/gameoptions.md</c>, section 5).
	/// </summary>
	public const string EnhancedDirectory = "interface/openhoi";

	/// <summary>The five keys every element takes.</summary>
	private static readonly string[] CommonKeys = { "name", "dontrender", "pdx_tooltip", "pdx_tooltip_delayed", "scope" };

	/// <summary>The keys a window takes beyond its children.</summary>
	private static readonly string[] WindowKeys =
	{
		"size", "position", "fullscreen", "parent", "priority", "background", "moveable", "verticalscrollbar",
		"horizontalscrollbar", "horizontalborder", "verticalborder", "click_to_front", "orientation", "upsound",
		"downsound",
	};

	/// <summary>
	/// What may be declared inside a window. The last three are keywords the original
	/// reads and no file of its own uses.
	/// </summary>
	private static readonly string[] ChildKeywords =
	{
		"windowtype", "guibuttontype", "icontype", "textboxtype", "instanttextboxtype", "editboxtype", "checkboxtype",
		"listboxtype", "scrollbartype", "overlappingelementsboxtype", "3dbuttontype", "multispritebuttontype",
		"threedguitype",
	};

	private static readonly string[] ButtonKeys =
	{
		"quadtexturesprite", "spritetype", "position", "tooltip", "tooltiptext", "delayedtooltiptext", "buttonfont", "buttontext",
		"orientation", "shortcut", "clicksound", "parent", "rotation", "size", "scale", "format",
		"oversound", "upsound", "downsound", "bordersize",
	};

	private static readonly string[] IconKeys =
	{
		"position", "spritetype", "orientation", "rotation", "frame", "scale", "buttonmesh",
	};

	private static readonly string[] TextBoxKeys =
	{
		"position", "font", "maxwidth", "maxheight", "text", "bordersize", "orientation", "format", "texturefile",
		"fixedsize", "allwaystransparent",
	};

	private static readonly string[] EditBoxKeys =
	{
		"position", "font", "size", "text", "orientation", "bordersize", "texturefile", "cursor",
	};

	private static readonly string[] PositionKeys = { "position" };

	private static readonly string[] ListBoxKeys =
	{
		"position", "background", "size", "bordersize", "orientation", "scrollbartype", "spacing", "priority",
		"offset", "step", "allwaystransparent",
	};

	/// <summary>The keys a scrollbar takes beyond the buttons and icons declared inside it.</summary>
	private static readonly string[] ScrollbarKeys =
	{
		"guibuttontype", "icontype", "slider", "track", "leftbutton", "rightbutton", "size", "position",
		"bordersize", "maxvalue", "minvalue", "stepsize", "startvalue", "horizontal", "priority", "lockable",
		"rangelimitminicon", "rangelimitmaxicon",
	};

	private static readonly string[] OverlappingBoxKeys = { "position", "size", "spacing", "orientation", "format" };

	/// <summary>The kinds that are read and not yet drawn, with the keys the original's own files give them.</summary>
	private static readonly Dictionary<string, string[]> OtherKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		["shieldtype"] = new[] { "spritetype", "position", "rotation" },
		["3dbuttontype"] = Array.Empty<string>(),
		["multispritebuttontype"] = Array.Empty<string>(),
		["threedguitype"] = Array.Empty<string>(),
	};

	private readonly Dictionary<string, CGuiType> _types = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>Each fault found, at its file and line, in the order found and each once (<c>docs/mechanics/logs.md</c>, section 4).</summary>
	private readonly List<string> _complaints = new();
	private readonly HashSet<string> _complained = new(StringComparer.Ordinal);

	/// <summary>The line of each button's <c>parent</c>, for a report of a parent its container does not hold.</summary>
	private readonly Dictionary<CGuiType, int> _parentLine = new();
	private string _file = "";

	public int Count => _types.Count;

	/// <summary>Names declared more than once at the top level. The later declaration is the one kept.</summary>
	public List<string> Replaced { get; } = new();

	public static CGuiTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var types = new CGuiTypes();
		// An enhanced game's own declarations after the rest, from a folder of their own (EnhancedDirectory).
		IEnumerable<string> files = directories.ListFiles(InterfaceDirectory, "*.gui");
		if (Rules.CRulesReader.IsEnhanced(directories))
		{
			files = files.Concat(directories.ListFiles(EnhancedDirectory, "*.gui"));
		}
		foreach (string file in files)
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e) when (e is IOException or FormatException)
			{
				warn(CDiagnostics.Located(file, 0, $"could not be read: {e.Message}"));
				continue;
			}
			types.AddFile(root, file);
		}
		types.Report(warn);
		return types;
	}

	/// <summary>Reads one parsed file. For tests, which have no folder to list.</summary>
	public static CGuiTypes Parse(string text, Action<string>? warn = null)
	{
		var types = new CGuiTypes();
		types.AddFile(ClausewitzParser.Parse(text), "text");
		types.Report(warn ?? CDiagnostics.Problem);
		return types;
	}

	/// <summary>The top-level element of that name, whatever its kind, or null.</summary>
	public CGuiType? Find(string name)
	{
		return _types.TryGetValue(name, out CGuiType? type) ? type : null;
	}

	public CGuiWindowType? FindWindow(string name)
	{
		return Find(name) as CGuiWindowType;
	}

	public CGuiPositionType? FindPosition(string name)
	{
		return Find(name) as CGuiPositionType;
	}

	public CGuiScrollbarType? FindScrollbar(string name)
	{
		return Find(name) as CGuiScrollbarType;
	}

	private void AddFile(ClausewitzNode root, string file)
	{
		_file = file;
		foreach (ClausewitzNode top in root.Children)
		{
			if (top.Key is null)
			{
				continue;
			}
			if (!top.Key.Equals("guiTypes", StringComparison.OrdinalIgnoreCase) || !top.IsBlock)
			{
				// The original reads this one key and nothing else, so a file that opens
				// with an element loads nothing.
				Complain(top.Line, $"top-level '{top.Key}' is not guiTypes and is not read");
				continue;
			}
			foreach (ClausewitzNode node in top.Children)
			{
				CGuiType? type = Read(node, topLevel: true);
				if (type is null)
				{
					continue;
				}
				if (type.Name.Length == 0)
				{
					Complain(node.Line, $"a top-level {node.Key} has no name and cannot be found");
					continue;
				}
				if (_types.ContainsKey(type.Name))
				{
					Replaced.Add(type.Name);
					Complain(node.Line, $"'{type.Name}' is declared more than once at the top level; the later one is used");
				}
				_types[type.Name] = type;
			}
		}
	}

	/// <summary>An element read, with the file and line it is declared at.</summary>
	private CGuiType? Read(ClausewitzNode node, bool topLevel)
	{
		CGuiType? type = ReadElement(node, topLevel);
		if (type is not null)
		{
			type.File = _file;
			type.Line = node.Line;
		}
		return type;
	}

	private CGuiType? ReadElement(ClausewitzNode node, bool topLevel)
	{
		if (node.Key is null || !node.IsBlock)
		{
			if (node.Key is not null && topLevel)
			{
				Complain(node.Line, $"'{node.Key}' in guiTypes is not an element");
			}
			return null;
		}

		string kind = node.Key;
		string keyword = kind.ToLowerInvariant();
		node = Settled(node);
		switch (keyword)
		{
			case "windowtype":
			case "eu3dialogtype":
				return ReadWindow(node, keyword == "eu3dialogtype");

			case "guibuttontype":
			case "checkboxtype":
				Check(node, ButtonKeys);
				return WithParentLine(node, new CGuiButtonType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					// One field to the original, so the later of the two names stands (FINDINGS-guikeys.md, section 1).
					Sprite = Later(node, "quadTextureSprite", "spriteType"),
					Parent = node.GetString("parent"),
					Text = node.GetString("buttonText"),
					Font = node.GetString("buttonFont"),
					Shortcut = node.GetString("shortcut"),
					ClickSound = node.GetString("clicksound"),
					UpSound = node.GetString("upsound"),
					DownSound = node.GetString("downsound"),
					OverSound = node.GetString("oversound"),
					Rotation = node.GetFloat("rotation"),
					IsCheckbox = keyword == "checkboxtype",
					Size = Point(node, "size"),
					Format = node.Get("format") is null ? EGuiTextFormat.Centre : Format(node),
					BorderSize = Point(node, "borderSize"),
				});

			case "icontype":
				Check(node, IconKeys);
				return new CGuiIconType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					// buttonMesh is spriteType under another name to the original (FINDINGS-guikeys.md, section 4).
					Sprite = Later(node, "spriteType", "buttonMesh"),
					Frame = node.GetInt("frame"),
					Scale = node.GetFloat("scale", 1f),
					Rotation = node.GetFloat("rotation"),
				};

			case "shieldtype":
				// A dialog's frame for a country's flag: an icon on a masked shield sprite, which shows
				// the flag of whatever country it is given.
				Check(node, OtherKeys["shieldtype"]);
				return new CGuiIconType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Sprite = node.GetString("spriteType"),
					Scale = 1f,
					Rotation = node.GetFloat("rotation"),
				};

			case "textboxtype":
			case "instanttextboxtype":
				Check(node, TextBoxKeys);
				return new CGuiTextBoxType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Font = node.GetString("font"),
					Text = node.GetString("text"),
					MaxWidth = node.GetInt("maxWidth"),
					MaxHeight = node.GetInt("maxHeight"),
					BorderSize = Point(node, "borderSize"),
					TextureFile = node.GetString("textureFile"),
					Format = Format(node),
					FixedSize = node.GetBool("fixedsize"),
					IsInstant = keyword == "instanttextboxtype",
				};

			case "editboxtype":
				// Shown as the text box it is until typing is built: its size is the room
				// its text has, and its text starts at the left.
				Check(node, EditBoxKeys);
				return new CGuiTextBoxType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Font = node.GetString("font"),
					Text = node.GetString("text"),
					MaxWidth = Point(node, "size").X,
					MaxHeight = Point(node, "size").Y,
					BorderSize = Point(node, "borderSize"),
					TextureFile = node.GetString("textureFile"),
					Format = EGuiTextFormat.Left,
					IsEdit = true,
				};

			case "positiontype":
				Check(node, PositionKeys);
				return new CGuiPositionType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
				};

			case "listboxtype":
				Check(node, ListBoxKeys);
				return new CGuiListBoxType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Size = Point(node, "size"),
					Spacing = node.GetInt("spacing"),
					Scrollbar = node.GetString("scrollbartype"),
					BorderSize = Point(node, "borderSize"),
					Background = node.GetString("background"),
					Offset = Point(node, "offset"),
				};

			case "scrollbartype":
				return ReadScrollbar(node);

			case "overlappingelementsboxtype":
				Check(node, OverlappingBoxKeys);
				return new CGuiOverlappingBoxType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Size = Point(node, "size"),
					Spacing = Number(node, "spacing"),
					Format = Format(node),
				};

			default:
				if (!OtherKeys.TryGetValue(keyword, out string[]? keys))
				{
					Complain(node.Line, $"'{node.Key}' is not a kind of element");
					return null;
				}
				Check(node, keys);
				return new CGuiOtherType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Size = Point(node, "size"),
				};
		}
	}

	private CGuiWindowType ReadWindow(ClausewitzNode node, bool isDialog)
	{
		var window = new CGuiWindowType
		{
			Name = node.GetString("name"),
			Keyword = node.Key!,
			Position = Point(node, "position"),
			Orientation = Orientation(node),
			Size = Point(node, "size"),
			FullScreen = node.GetBool("fullScreen"),
			Moveable = node.GetInt("moveable") != 0,
			Background = node.GetString("backGround"),
			IsDialog = isDialog,
		};

		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null)
			{
				continue;
			}
			string key = child.Key.ToLowerInvariant();
			if (ChildKeywords.Contains(key) || (isDialog && key == "shieldtype"))
			{
				if (Read(child, topLevel: false) is { } element)
				{
					window.Children.Add(element);
				}
			}
			else if (!CommonKeys.Contains(key) && !WindowKeys.Contains(key))
			{
				Complain(child.Line, $"{node.Key} does not take '{child.Key}'");
			}
		}
		InheritSprites(window.Children, window.Name);
		return window;
	}

	/// <summary>
	/// A button's <c>parent</c>, settled once its container's elements are all read, as the original resolves it: a
	/// button with no sprite takes its parent's - the sibling of that name, without case - and where that has none, its
	/// parent's, until one has a sprite or the chain ends or comes straight back. Field by field: nothing else is
	/// inherited here, the tooltips the original also passes down being no part of how this draws a button. A parent the
	/// container does not hold is reported; the original reads through nothing and falls
	/// (<c>reversing/findings/FINDINGS-guikeys.md</c>, section 1).
	/// </summary>
	private void InheritSprites(List<CGuiType> children, string container)
	{
		foreach (CGuiButtonType button in children.OfType<CGuiButtonType>())
		{
			string at = button.Parent;
			if (button.Sprite.Length > 0 || at.Length == 0)
			{
				continue;
			}
			while (at.Length > 0)
			{
				CGuiType? parent = children.Find(child => child.Name.Equals(at, StringComparison.OrdinalIgnoreCase));
				if (parent is null)
				{
					Complain(_parentLine.GetValueOrDefault(button), $"'{button.Name}' in '{container}' names a parent '{at}' " +
						"it does not hold; the original reads through nothing there");
					break;
				}
				if (parent is not CGuiButtonType from)
				{
					break;
				}
				if (from.Sprite.Length > 0)
				{
					button.Sprite = from.Sprite;
					break;
				}
				at = from.Parent.Equals(button.Name, StringComparison.OrdinalIgnoreCase) ? "" : from.Parent;
			}
		}
	}

	/// <summary>A button, its <c>parent</c>'s line - its own where it gives none - kept for a report.</summary>
	private CGuiButtonType WithParentLine(ClausewitzNode node, CGuiButtonType button)
	{
		_parentLine[button] = node.Get("parent")?.Line ?? node.Line;
		return button;
	}

	/// <summary>The later of two keys that are one field to the original, or an empty string.</summary>
	private static string Later(ClausewitzNode node, string first, string second)
	{
		string found = "";
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not null && child.Value is not null
				&& (child.Key.Equals(first, StringComparison.OrdinalIgnoreCase)
					|| child.Key.Equals(second, StringComparison.OrdinalIgnoreCase)))
			{
				found = child.Value;
			}
		}
		return found;
	}

	/// <summary>A scrollbar, with the buttons and icons declared inside it.</summary>
	private CGuiScrollbarType ReadScrollbar(ClausewitzNode node)
	{
		Check(node, ScrollbarKeys);
		var scrollbar = new CGuiScrollbarType
		{
			Name = node.GetString("name"),
			Keyword = node.Key!,
			Position = Point(node, "position"),
			Orientation = Orientation(node),
			Size = Point(node, "size"),
			Horizontal = node.GetInt("horizontal") != 0,
			MinValue = Number(node, "minValue"),
			MaxValue = Number(node, "maxValue"),
			StartValue = Number(node, "startValue"),
			StepSize = Number(node, "stepSize"),
			BorderSize = Point(node, "borderSize"),
			Slider = node.GetString("slider"),
			Track = node.GetString("track"),
			LeftButton = node.GetString("leftbutton"),
			RightButton = node.GetString("rightbutton"),
		};
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || !child.IsBlock)
			{
				continue;
			}
			string key = child.Key.ToLowerInvariant();
			if ((key == "guibuttontype" || key == "icontype") && Read(child, topLevel: false) is { } element)
			{
				scrollbar.Children.Add(element);
			}
		}
		InheritSprites(scrollbar.Children, scrollbar.Name);
		return scrollbar;
	}

	/// <summary>
	/// An element as it stands once every key has been read. The original reads an
	/// element a key at a time, each into its own place, so a key given twice leaves
	/// what the second said: <c>brigade_entry_3</c>'s button has two positions and is
	/// drawn at the later one. Elements declared inside are all kept, in their order.
	/// </summary>
	private ClausewitzNode Settled(ClausewitzNode node)
	{
		var last = new Dictionary<string, ClausewitzNode>(StringComparer.OrdinalIgnoreCase);
		bool repeats = false;
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || IsElement(child))
			{
				continue;
			}
			// A format in quotes is not taken, and the one before it stands (interface.md, section 1).
			if (IsQuotedFormat(child))
			{
				Complain(child.Line, $"{node.Key} gives 'format' in quotes, which is not taken");
				repeats = true;
				continue;
			}
			if (last.ContainsKey(child.Key))
			{
				// Valid, and what the original does with it is known; it is said because
				// the earlier value is almost never what the file's author thinks is used.
				Complain(child.Line, $"{node.Key} gives '{child.Key}' more than once and the last is the one used");
				repeats = true;
			}
			last[child.Key] = child;
		}
		if (!repeats)
		{
			return node;
		}

		var settled = new ClausewitzNode { Key = node.Key, Value = node.Value, Line = node.Line };
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null || IsElement(child)
				|| (!IsQuotedFormat(child) && ReferenceEquals(last[child.Key], child)))
			{
				settled.Children.Add(child);
			}
		}
		return settled;
	}

	/// <summary>
	/// A <c>format</c> written in quotes: <i>measured</i>, on a screenshot of the original's reorganise window, where
	/// <c>subunit_strength</c>'s <c>format = left</c> and then <c>format = "right"</c> draw its men to the left - the only
	/// two quoted formats in the base game.
	/// </summary>
	private static bool IsQuotedFormat(ClausewitzNode child)
	{
		return child.IsQuoted && string.Equals(child.Key, "format", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsElement(ClausewitzNode node)
	{
		string key = node.Key!.ToLowerInvariant();
		return node.IsBlock && (ChildKeywords.Contains(key) || OtherKeys.ContainsKey(key));
	}

	/// <summary>A plain number an element gives, or nothing when it gives none or one that is no number.</summary>
	private static CFixedPoint Number(ClausewitzNode node, string key)
	{
		return CFixedPoint.TryParse(node.Get(key)?.Value, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	/// <summary>Reports every key of an element that neither it nor every element takes.</summary>
	private void Check(ClausewitzNode node, string[] keys)
	{
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null)
			{
				continue;
			}
			string key = child.Key.ToLowerInvariant();
			if (!CommonKeys.Contains(key) && !keys.Contains(key))
			{
				Complain(child.Line, $"{node.Key} does not take '{child.Key}'");
			}
		}
	}

	private static CGuiPoint Point(ClausewitzNode node, string key)
	{
		ClausewitzNode? block = node.Get(key);
		if (block is null || !block.IsBlock)
		{
			return default;
		}
		return new CGuiPoint(block.GetInt("x"), block.GetInt("y"));
	}

	/// <summary>
	/// The original compares the word exactly and takes anything else for the upper
	/// left. So does this, and says so.
	/// </summary>
	private EGuiOrientation Orientation(ClausewitzNode node)
	{
		ClausewitzNode? given = node.Get("orientation");
		string? word = given?.Value;
		switch (word)
		{
			case null:
			case "UPPER_LEFT":
				return EGuiOrientation.UpperLeft;
			case "UPPER_RIGHT":
				return EGuiOrientation.UpperRight;
			case "LOWER_LEFT":
				return EGuiOrientation.LowerLeft;
			case "LOWER_RIGHT":
				return EGuiOrientation.LowerRight;
			case "CENTER":
				return EGuiOrientation.Center;
			case "CENTER_UP":
				return EGuiOrientation.CenterUp;
			case "CENTER_DOWN":
				return EGuiOrientation.CenterDown;
			default:
				Complain(given?.Line ?? node.Line, $"orientation '{word}' is not one of the seven and is taken for UPPER_LEFT");
				return EGuiOrientation.UpperLeft;
		}
	}

	private EGuiTextFormat Format(ClausewitzNode node)
	{
		ClausewitzNode? given = node.Get("format");
		string? word = given?.Value;
		switch (word?.ToLower(CultureInfo.InvariantCulture))
		{
			case null:
			case "left":
				return EGuiTextFormat.Left;
			case "centre":
			case "center":
				return EGuiTextFormat.Centre;
			case "right":
				return EGuiTextFormat.Right;
			default:
				Complain(given?.Line ?? node.Line, $"format '{word}' is not left, centre or right and is taken for left");
				return EGuiTextFormat.Left;
		}
	}

	/// <summary>A fault at a line of the file being read, kept once.</summary>
	private void Complain(int line, string what)
	{
		string located = CDiagnostics.Located(_file, line, what);
		if (_complained.Add(located))
		{
			_complaints.Add(located);
		}
	}

	/// <summary>Each fault, one line each, in the order found.</summary>
	private void Report(Action<string> warn)
	{
		foreach (string complaint in _complaints)
		{
			warn(complaint);
		}
		_complaints.Clear();
		_complained.Clear();
	}
}
