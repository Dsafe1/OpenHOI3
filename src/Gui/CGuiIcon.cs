using System;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Gui;

/// <summary>
/// A sprite placed by an <c>iconType</c>. It takes its size from its texture, because
/// the file gives it none - unless its sprite is a progress bar, which declares one.
/// </summary>
public partial class CGuiIcon : CGuiObject
{
	private CGuiContext _context = null!;
	private CSpriteType? _sprite;
	private Texture2D? _texture;
	private Texture2D? _rest;
	private TextureRect? _rect;
	private int _frame = 1;
	private int _progress;
	private CGuiButton? _over;
	private CGuiButton? _pressed;
	private bool _pressedItself;

	/// <summary>Raised when the icon itself is clicked: the mouse went down and came up on it, over no button.</summary>
	public event Action? Clicked;

	public void Initialise(CGuiIconType type, CGuiContext context)
	{
		_context = context;
		_frame = type.Frame < 1 ? 1 : type.Frame;

		_rect = new TextureRect
		{
			Name = "Sprite",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_rect);

		Place(type, Vector2.Zero);
		Use(context.Sprites.Find(type.Sprite), type.Sprite);
		// A rotation turns the icon about the middle of its lower left pixel, and a
		// positive quarter stands a bar on its end, running upwards: one w long and h high
		// at (x, y) then covers x - h + 1 to x + 1 across and y + h - w to y + h down.
		// Measured, not read: against a screenshot of the original's unit panel beside
		// this one's, in the header and in a brigade's row.
		if (type.Rotation != 0f)
		{
			PivotOffset = new Vector2(0.5f, OffsetBottom - OffsetTop - 0.5f);
			Rotation = -type.Rotation;
		}
		MouseExited += () => HoverOver(null);
	}

	/// <summary>
	/// An icon keeps a click from the map and not from a button of its own window that
	/// it lies on: the counter on a line of a list is over the line's button, and a click
	/// on it picks the line. What the mouse does to the icon it does to that button.
	/// </summary>
	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			HoverOver(ButtonUnder(motion.Position));
			return;
		}
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			return;
		}

		CGuiButton? button = ButtonUnder(mouse.Position);
		if (mouse.Pressed)
		{
			HoverOver(button);
			_pressed = button;
			_pressedItself = button is null;
			button?.Click(mouse);
			return;
		}

		// With no button under it the click is the icon's own: an alert is an icon
		// that is clicked.
		if (_pressedItself)
		{
			_pressedItself = false;
			if (button is null && new Rect2(Vector2.Zero, Size).HasPoint(mouse.Position))
			{
				Clicked?.Invoke();
			}
			return;
		}

		// The press counts when the mouse comes up over the button it went down on.
		CGuiButton? pressed = _pressed;
		_pressed = null;
		if (pressed is null || !IsInstanceValid(pressed))
		{
			return;
		}
		if (ReferenceEquals(pressed, button))
		{
			pressed.Click(mouse);
		}
		else
		{
			pressed.SetHovered(false);
		}
	}

	/// <summary>
	/// The uppermost button drawn under this icon, in the same window, at a point of the
	/// icon - or null.
	/// </summary>
	private CGuiButton? ButtonUnder(Vector2 point)
	{
		if (GetParent() is not { } parent)
		{
			return null;
		}
		Vector2 inParent = Position + point;
		for (int index = GetIndex() - 1; index >= 0; index--)
		{
			if (parent.GetChild(index) is CGuiButton { Visible: true } button
				&& new Rect2(button.Position, button.Size).HasPoint(inParent))
			{
				return button;
			}
		}
		return null;
	}

	private void HoverOver(CGuiButton? button)
	{
		if (ReferenceEquals(button, _over))
		{
			return;
		}
		if (_over is not null && IsInstanceValid(_over))
		{
			_over.SetHovered(false);
		}
		_over = button;
		button?.SetHovered(true);
	}

	/// <summary>
	/// Gives the icon another sprite, keeping its place and its scale. Answers whether
	/// there is a sprite of that name; when there is not the icon is left as it was,
	/// which is what the original does with a name it cannot find.
	/// </summary>
	public bool SetSprite(string name)
	{
		CSpriteType? sprite = _context.Sprites.Find(name);
		if (sprite is null)
		{
			return false;
		}
		if (!ReferenceEquals(sprite, _sprite))
		{
			Use(sprite, name);
		}
		return true;
	}

	/// <summary>
	/// Shows a picture that no sprite declares - a file found by a name built at run
	/// time - in the place and at the size of the sprite's own.
	/// </summary>
	public void SetTexture(Texture2D texture)
	{
		// The original makes a sprite of the file, so the icon is the file's size -
		// which matters where the sprite's own picture is missing and has none.
		var type = (CGuiIconType)GuiType;
		Place(type, FrameSize(texture, 1) * type.Scale);
		if (_rect is not null)
		{
			_rect.Texture = texture;
		}
	}

	/// <summary>Goes back to the sprite's own picture after <see cref="SetTexture"/>.</summary>
	public void ShowSprite()
	{
		var type = (CGuiIconType)GuiType;
		Place(type, _texture is null ? Vector2.Zero : FrameSize(_texture, _sprite?.FrameCount ?? 1) * type.Scale);
		ShowTexture(_texture);
	}

	private void Use(CSpriteType? sprite, string name)
	{
		_sprite = sprite;
		if (sprite is null && name.Length > 0)
		{
			CDiagnostics.Warn($"interface: '{GuiType.Name}' names the sprite '{name}', which no .gfx file declares");
		}
		var type = (CGuiIconType)GuiType;

		if (sprite is { IsProgressBar: true })
		{
			// The bar's two pictures: the part that is filled, and the rest of it.
			_texture = _context.Textures.LoadFromGamePath(sprite.OverlayFile);
			_rest = sprite.MaskFile.Length == 0 ? null : _context.Textures.LoadFromGamePath(sprite.MaskFile);
			Place(type, sprite.TileSize * type.Scale);
			MouseFilter = MouseFilterEnum.Ignore;
			ShowTexture(null);
			QueueRedraw();
			return;
		}

		// A masked shield has no picture until it is given a country.
		_texture = sprite is null || sprite.IsMaskedShield ? null : _context.Textures.Load(sprite);
		Vector2 size = _texture is null ? Vector2.Zero : FrameSize(_texture, sprite!.FrameCount) * type.Scale;
		Place(type, size);

		// What is drawn takes the click, so that the map under a panel does not.
		MouseFilter = sprite is { AlwaysTransparent: true } ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
		ShowTexture(_texture);
	}

	/// <summary>Shows a frame of the sprite sheet, counted from one.</summary>
	public void SetFrame(int frame)
	{
		if (frame == _frame)
		{
			return;
		}
		_frame = frame;
		ShowTexture(_texture);
	}

	/// <summary>How much of a progress bar is filled, in hundredths. Nothing to any other icon.</summary>
	public void SetProgress(int hundredths)
	{
		hundredths = Mathf.Clamp(hundredths, 0, 100);
		if (hundredths == _progress)
		{
			return;
		}
		_progress = hundredths;
		QueueRedraw();
	}

	/// <summary>Shows a country's flag, where the sprite is one that draws flags.</summary>
	public void SetCountry(CCountry country)
	{
		if (_sprite is not { IsMaskedShield: true })
		{
			return;
		}
		_texture = _context.Shields.Build(country, _sprite);
		if (_texture is not null)
		{
			var type = (CGuiIconType)GuiType;
			Place(type, FrameSize(_texture, 1) * type.Scale);
		}
		ShowTexture(_texture);
	}

	private void ShowTexture(Texture2D? texture)
	{
		if (_rect is null)
		{
			return;
		}
		bool isBar = _sprite is { IsProgressBar: true };
		_rect.Texture = texture is null || isBar ? null : Frame(texture, _frame, _sprite?.FrameCount ?? 1);
	}

	public override void _Draw()
	{
		if (_sprite is not { IsProgressBar: true })
		{
			return;
		}
		float filled = Mathf.Round(Size.X * _progress / 100f);
		if (_texture is not null && filled > 0f)
		{
			float part = _texture.GetWidth() * _progress / 100f;
			DrawTextureRectRegion(_texture, new Rect2(0f, 0f, filled, Size.Y), new Rect2(0f, 0f, part, _texture.GetHeight()));
		}
		if (_rest is not null && filled < Size.X)
		{
			float from = _rest.GetWidth() * _progress / 100f;
			DrawTextureRectRegion(_rest, new Rect2(filled, 0f, Size.X - filled, Size.Y),
				new Rect2(from, 0f, _rest.GetWidth() - from, _rest.GetHeight()));
		}
	}
}
