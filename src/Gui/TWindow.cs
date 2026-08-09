using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// A live window. Instantiates its child elements and, when the type names a
/// background element, draws that stretched behind them instead of as a normal child.
/// </summary>
public partial class TWindow : CGuiObject
{
	private readonly Dictionary<string, CGuiObject> _children =
		new(StringComparer.OrdinalIgnoreCase);

	public void Initialise(CWindowType type, CGuiContext context)
	{
		Vector2 size = type.Size;
		if (type.FullScreen)
		{
			// A fullscreen window ignores its declared size and fills the viewport.
			ApplyLayout(type, Vector2.Zero);
			AnchorRight = 1f;
			AnchorBottom = 1f;
			OffsetLeft = 0f;
			OffsetTop = 0f;
			OffsetRight = 0f;
			OffsetBottom = 0f;
		}
		else
		{
			ApplyLayout(type, size);
		}

		// Containers must not eat clicks meant for the buttons inside them.
		MouseFilter = MouseFilterEnum.Ignore;

		foreach (CGuiType child in type.Children)
		{
			if (child is CWindowType childWindow)
			{
				var nested = new TWindow();
				nested.Initialise(childWindow, context);
				AddChild(nested);
				_children[childWindow.Name] = nested;
				continue;
			}

			bool isBackground = type.Background.Length != 0 &&
				string.Equals(child.Name, type.Background, StringComparison.OrdinalIgnoreCase);

			if (isBackground && child is CButtonType backgroundButton)
			{
				Control? background = CreateBackground(backgroundButton, context);
				if (background is not null)
				{
					AddChild(background);
					MoveChild(background, 0);
				}
				continue;
			}

			CGuiObject? element = CGuiObject.Create(child, context);
			if (element is null)
			{
				continue;
			}
			AddChild(element);
			if (child.Name.Length != 0)
			{
				_children[child.Name] = element;
			}
		}
	}

	/// <summary>
	/// The element named by <c>backGround</c> fills the window rather than sitting at
	/// its own position. Cornered tile sprites repeat; everything else stretches.
	/// </summary>
	private static Control? CreateBackground(CButtonType type, CGuiContext context)
	{
		CSpriteType? sprite = context.Sprites.Find(type.SpriteName);
		if (sprite is null)
		{
			return null;
		}
		Texture2D? texture = context.Textures.Load(sprite);
		if (texture is null)
		{
			return null;
		}

		var rect = new TextureRect
		{
			Name = type.Name.Length == 0 ? "Background" : type.Name,
			Texture = texture,
			StretchMode = sprite.IsTiled ? TextureRect.StretchModeEnum.Tile
				: TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return rect;
	}

	public CGuiObject? FindElement(string name)
	{
		return _children.TryGetValue(name, out CGuiObject? child) ? child : null;
	}

	public TButton? FindButton(string name)
	{
		return FindElement(name) as TButton;
	}

	public TWindow? FindChildWindow(string name)
	{
		return FindElement(name) as TWindow;
	}
}
