using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Gui;

/// <summary>
/// A live window: its type's children built in the order the file declares them, which
/// is the order they are drawn in, and found afterwards by the names the file gave
/// them. Those names are the contract between an interface file and the code of a
/// screen: <c>docs/mechanics/interface.md</c>, section 4.
/// </summary>
public partial class CGuiWindow : CGuiObject
{
	private readonly Dictionary<string, CGuiButton> _buttons = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiIcon> _icons = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiTextBox> _textBoxes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiListBox> _listBoxes = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiScrollbar> _scrollbars = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, CGuiOverlappingBox> _boxes = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>How many of its children are of kinds nothing draws yet.</summary>
	public int Unbuilt { get; private set; }

	/// <summary>Makes the top-level window of that name, or answers null when no file declares one.</summary>
	public static CGuiWindow? Create(CGuiContext context, string name)
	{
		CGuiWindowType? type = context.Types.FindWindow(name);
		if (type is null)
		{
			CDiagnostics.Warn($"interface: no file declares a window named '{name}'");
			return null;
		}
		var window = new CGuiWindow();
		window.Initialise(type, context);
		return window;
	}

	/// <summary>
	/// Makes a window that is one of many of its type - a line of a list, an entry of
	/// the technology tree - with its upper left corner where its owner will put it.
	/// </summary>
	public static CGuiWindow? CreateEntry(CGuiContext context, string name)
	{
		CGuiWindow? window = Create(context, name);
		if (window is not null)
		{
			window.MoveTo(Vector2.Zero);
		}
		return window;
	}

	/// <summary>Puts the window's upper left corner at a point of its parent, whatever its type said.</summary>
	public void MoveTo(Vector2 position)
	{
		var type = (CGuiWindowType)GuiType;
		var size = new Vector2(type.Size.X, type.Size.Y);
		AnchorLeft = 0f;
		AnchorRight = 0f;
		AnchorTop = 0f;
		AnchorBottom = 0f;
		OffsetLeft = position.X;
		OffsetTop = position.Y;
		OffsetRight = position.X + size.X;
		OffsetBottom = position.Y + size.Y;
	}

	public void Initialise(CGuiWindowType type, CGuiContext context)
	{
		if (type.FullScreen)
		{
			// The window is the screen, whatever size the file gave it.
			Place(type, Vector2.Zero);
			SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}
		else
		{
			Place(type, new Vector2(type.Size.X, type.Size.Y));
		}

		// A window is a place for things and takes no clicks itself; what is in it does.
		MouseFilter = MouseFilterEnum.Ignore;

		foreach (CGuiType child in type.Children)
		{
			bool isBackground = type.Background.Length > 0
				&& child.Name.Equals(type.Background, StringComparison.OrdinalIgnoreCase);
			if (isBackground && child is CGuiButtonType backgroundType && Background(backgroundType, context) is { } background)
			{
				AddChild(background);
				MoveChild(background, 0);
				continue;
			}

			switch (child)
			{
				case CGuiWindowType windowType:
					var window = new CGuiWindow();
					window.Initialise(windowType, context);
					AddChild(window);
					_windows.TryAdd(child.Name, window);
					break;

				case CGuiButtonType buttonType:
					var button = new CGuiButton();
					button.Initialise(buttonType, context);
					AddChild(button);
					_buttons.TryAdd(child.Name, button);
					break;

				case CGuiIconType iconType:
					var icon = new CGuiIcon();
					icon.Initialise(iconType, context);
					AddChild(icon);
					_icons.TryAdd(child.Name, icon);
					break;

				case CGuiTextBoxType textBoxType:
					var textBox = new CGuiTextBox();
					textBox.Initialise(textBoxType, context);
					AddChild(textBox);
					// The original keeps text boxes in an index where a second of one
					// name takes the place of the first.
					_textBoxes[child.Name] = textBox;
					break;

				case CGuiListBoxType listBoxType:
					var listBox = new CGuiListBox();
					listBox.Initialise(listBoxType, context);
					AddChild(listBox);
					_listBoxes.TryAdd(child.Name, listBox);
					break;

				case CGuiScrollbarType scrollbarType:
					var scrollbar = new CGuiScrollbar();
					scrollbar.Initialise(scrollbarType, context);
					AddChild(scrollbar);
					_scrollbars.TryAdd(child.Name, scrollbar);
					break;

				case CGuiOverlappingBoxType boxType:
					var box = new CGuiOverlappingBox();
					box.Initialise(boxType);
					AddChild(box);
					_boxes.TryAdd(child.Name, box);
					break;

				default:
					Unbuilt++;
					break;
			}
		}
	}

	/// <summary>
	/// The child a window names as its background. It is put at the window's corner
	/// whatever position it declares. A cornered tile fills the window, repeating - and
	/// one that declares a border keeps its corners and repeats its edges and its middle
	/// between them, as a scrollbar's track does; the dialogs are the first such windows
	/// built. A plain picture is not a tile and is not stretched: it keeps its own size -
	/// the windows that have one are of another size than their art, every one of them.
	/// It takes the clicks that fall on it and does nothing with them. Measured on
	/// the orders windows: see <c>docs/mechanics/interface.md</c>, section 3.
	/// </summary>
	private static Control? Background(CGuiButtonType type, CGuiContext context)
	{
		CSpriteType? sprite = context.Sprites.Find(type.Sprite);
		Texture2D? texture = sprite is null ? null : context.Textures.Load(sprite);
		if (sprite is null || texture is null)
		{
			return null;
		}
		if (sprite.IsTiled && sprite.Border != Vector2.Zero)
		{
			var patch = new NinePatchRect
			{
				Name = type.Name.Length == 0 ? "Background" : type.Name,
				Texture = texture,
				PatchMarginLeft = (int)sprite.Border.X,
				PatchMarginRight = (int)sprite.Border.X,
				PatchMarginTop = (int)sprite.Border.Y,
				PatchMarginBottom = (int)sprite.Border.Y,
				AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
				AxisStretchVertical = NinePatchRect.AxisStretchMode.Tile,
				MouseFilter = MouseFilterEnum.Stop,
			};
			patch.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			return patch;
		}
		var rect = new TextureRect
		{
			Name = type.Name.Length == 0 ? "Background" : type.Name,
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = sprite.IsTiled ? TextureRect.StretchModeEnum.Tile : TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Stop,
		};
		if (sprite.IsTiled)
		{
			rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}
		else
		{
			rect.Position = Vector2.Zero;
			rect.Size = texture.GetSize();
		}
		return rect;
	}

	public CGuiButton? FindButton(string name)
	{
		return Found(_buttons, name, "button");
	}

	public CGuiIcon? FindIcon(string name)
	{
		return Found(_icons, name, "icon");
	}

	public CGuiTextBox? FindTextBox(string name)
	{
		return Found(_textBoxes, name, "text box");
	}

	public CGuiWindow? FindWindow(string name)
	{
		return Found(_windows, name, "window");
	}

	public CGuiListBox? FindListBox(string name)
	{
		return Found(_listBoxes, name, "list box");
	}

	public CGuiScrollbar? FindScrollbar(string name)
	{
		return Found(_scrollbars, name, "scrollbar");
	}

	public CGuiOverlappingBox? FindBox(string name)
	{
		return Found(_boxes, name, "box of overlapping elements");
	}

	/// <summary>
	/// An element of any kind the window may or may not have - an icon, a text box or a
	/// button, first found first - with no word in the log when it has not: for a list of
	/// names the original's code answers for, some of which a mod may drop.
	/// </summary>
	public CGuiObject? TryFindElement(string name)
	{
		return _icons.GetValueOrDefault(name) as CGuiObject
			?? _textBoxes.GetValueOrDefault(name) as CGuiObject
			?? _buttons.GetValueOrDefault(name);
	}

	/// <summary>
	/// A button the window may or may not have, with no word in the log when it has
	/// not: for names a screen makes up from the game's data, such as a building's key.
	/// </summary>
	public CGuiButton? TryFindButton(string name)
	{
		return _buttons.GetValueOrDefault(name);
	}

	/// <summary>An icon the window may or may not have; see <see cref="TryFindButton"/>.</summary>
	public CGuiIcon? TryFindIcon(string name)
	{
		return _icons.GetValueOrDefault(name);
	}

	/// <summary>
	/// Gives every element of the window, at any depth, a tooltip asked of the element's name -
	/// for an entry whose screen answers for all of it by which of its elements is under the
	/// mouse, as the original's do. An element that lets the mouse through is left so, and
	/// what is under it answers: made to take the mouse, a text box over a button would take
	/// that button's clicks too.
	/// </summary>
	public void SetTooltips(Func<string, CTooltip> tooltip)
	{
		foreach (CGuiObject element in Descendants(this))
		{
			if (element.MouseFilter == MouseFilterEnum.Ignore)
			{
				continue;
			}
			string name = element.Name;
			element.Tooltip = () => tooltip(name);
		}
	}

	private static IEnumerable<CGuiObject> Descendants(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is CGuiObject element)
			{
				yield return element;
			}
			foreach (CGuiObject below in Descendants(child))
			{
				yield return below;
			}
		}
	}

	/// <summary>
	/// A screen that asks for an element its window does not have gets nothing, and the
	/// log says which: a mod that renames an element loses it, as it would in the original.
	/// </summary>
	private T? Found<T>(Dictionary<string, T> index, string name, string kind) where T : class
	{
		if (index.TryGetValue(name, out T? found))
		{
			return found;
		}
		CDiagnostics.Warn($"interface: the window '{GuiType.Name}' has no {kind} named '{name}'");
		return null;
	}
}
