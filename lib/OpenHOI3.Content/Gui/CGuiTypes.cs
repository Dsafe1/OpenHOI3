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
		"quadtexturesprite", "position", "tooltip", "tooltiptext", "delayedtooltiptext", "buttonfont", "buttontext",
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
	private readonly Dictionary<string, int> _complaints = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _firstSeenIn = new(StringComparer.OrdinalIgnoreCase);
	private string _file = "";

	public int Count => _types.Count;

	/// <summary>Names declared more than once at the top level. The later declaration is the one kept.</summary>
	public List<string> Replaced { get; } = new();

	public static CGuiTypes Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var types = new CGuiTypes();
		foreach (string file in directories.ListFiles(InterfaceDirectory, "*.gui"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (Exception e) when (e is IOException or FormatException)
			{
				warn($"Could not read '{file}': {e.Message}");
				continue;
			}
			types.AddFile(root, Path.GetFileName(file));
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
				Complain($"top-level '{top.Key}' is not guiTypes and is not read");
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
					Complain($"a top-level {node.Key} has no name and cannot be found");
					continue;
				}
				if (_types.ContainsKey(type.Name))
				{
					Replaced.Add(type.Name);
				}
				_types[type.Name] = type;
			}
		}
	}

	private CGuiType? Read(ClausewitzNode node, bool topLevel)
	{
		if (node.Key is null || !node.IsBlock)
		{
			if (node.Key is not null && topLevel)
			{
				Complain($"'{node.Key}' in guiTypes is not an element");
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
				return new CGuiButtonType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Sprite = node.GetString("quadTextureSprite"),
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
				};

			case "icontype":
				Check(node, IconKeys);
				return new CGuiIconType
				{
					Name = node.GetString("name"),
					Keyword = kind,
					Position = Point(node, "position"),
					Orientation = Orientation(node),
					Sprite = node.GetString("spriteType"),
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
					Complain($"'{node.Key}' is not a kind of element");
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
				Complain($"{node.Key} does not take '{child.Key}'");
			}
		}
		return window;
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
				Complain($"{node.Key} gives 'format' in quotes, which is not taken");
				repeats = true;
				continue;
			}
			if (last.ContainsKey(child.Key))
			{
				// Valid, and what the original does with it is known; it is said because
				// the earlier value is almost never what the file's author thinks is used.
				Complain($"{node.Key} gives '{child.Key}' more than once and the last is the one used");
				repeats = true;
			}
			last[child.Key] = child;
		}
		if (!repeats)
		{
			return node;
		}

		var settled = new ClausewitzNode { Key = node.Key, Value = node.Value };
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
				Complain($"{node.Key} does not take '{child.Key}'");
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
		string? word = node.Get("orientation")?.Value;
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
				Complain($"orientation '{word}' is not one of the seven and is taken for UPPER_LEFT");
				return EGuiOrientation.UpperLeft;
		}
	}

	private EGuiTextFormat Format(ClausewitzNode node)
	{
		string? word = node.Get("format")?.Value;
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
				Complain($"format '{word}' is not left, centre or right and is taken for left");
				return EGuiTextFormat.Left;
		}
	}

	private void Complain(string what)
	{
		_complaints[what] = _complaints.GetValueOrDefault(what) + 1;
		_firstSeenIn.TryAdd(what, _file);
	}

	/// <summary>One line for each kind of complaint, however many times it came up.</summary>
	private void Report(Action<string> warn)
	{
		foreach ((string what, int count) in _complaints.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
		{
			string where = count == 1 ? _firstSeenIn[what] : $"{_firstSeenIn[what]} and {count - 1} more";
			warn($"interface: {what} ({where})");
		}
		foreach (string name in Replaced)
		{
			warn($"interface: '{name}' is declared more than once at the top level; the later one is used");
		}
		_complaints.Clear();
	}
}
