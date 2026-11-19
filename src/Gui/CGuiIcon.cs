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
	private System.Collections.Generic.IReadOnlyList<(int Percent, Color Colour)> _slices = System.Array.Empty<(int, Color)>();
	private CGuiButton? _over;
	private CGuiButton? _pressed;
	private bool _pressedItself;

	/// <summary>How far right of its own middle a turned flag's turning point is. Measured.</summary>
	private const float ShieldPivotShift = 1.5f;

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
		Turn();
		MouseExited += () => HoverOver(null);
	}

	/// <summary>
	/// Turns the icon by its type's rotation, about a point that depends on what it
	/// draws. Both rules are measured on screenshots of the original and neither is read.
	///
	/// A bar turns about the middle of its lower left pixel, and a positive quarter
	/// stands it on its end, running upwards: one w long and h high at (x, y) then covers
	/// x - h + 1 to x + 1 across and y + h - w to y + h down - the unit panel's bars, in
	/// the header and in a brigade's row.
	///
	/// A flag turns about its own middle, a pixel and a half to the right of it: the
	/// battle window's two, a negative quarter, 66 by 51 at (x, y), cover x + 9 to
	/// x + 60 across and y - 8 to y + 58 down.
	/// </summary>
	private void Turn()
	{
		var type = (CGuiIconType)GuiType;
		if (type.Rotation == 0f)
		{
			return;
		}
		var size = new Vector2(OffsetRight - OffsetLeft, OffsetBottom - OffsetTop);
		PivotOffset = _sprite is { IsMaskedShield: true }
			? size / 2f + new Vector2(ShieldPivotShift, 0f)
			: new Vector2(0.5f, size.Y - 0.5f);
		Rotation = -type.Rotation;
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
			MouseAt = motion.Position;
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
				&& button._HasPoint(inParent - button.Position))
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
		FillBox();
		if (_rect is not null)
		{
			_rect.Texture = texture;
		}
	}

	/// <summary>
	/// Shows a picture as the original's picture sprite draws one - here the country screens', the ledger's and the top
	/// bar's skins: in its sprite's own box, the sprite's file size times the icon's scale, with the sprite's texture
	/// coordinates laid on the picture (<see cref="Interface.Text.CPictureSprite"/>; <c>docs/mechanics/interface.md</c>,
	/// section 5). A picture the size of the sprite's file looks as <see cref="SetTexture"/> shows it; one of another size
	/// is stretched or cut. Where the sprite has no picture of its own to measure, as <see cref="SetTexture"/>.
	/// </summary>
	public void ShowPicture(Texture2D picture)
	{
		var type = (CGuiIconType)GuiType;
		int frames = _sprite?.FrameCount ?? 1;
		if (_texture is null || _rect is null || _sprite is { IsProgressBar: true } or { IsPieChart: true })
		{
			SetTexture(picture);
			return;
		}
		Place(type, FrameSize(_texture, frames) * type.Scale);
		if (Interface.Text.CPictureSprite.Place(_texture.GetWidth(), _texture.GetHeight(), frames, type.Scale,
			picture.GetWidth(), picture.GetHeight()) is not { } part)
		{
			FillBox();
			_rect.Texture = null;
			return;
		}
		_rect.SetAnchorsPreset(LayoutPreset.TopLeft);
		_rect.Position = new Vector2(part.X, part.Y);
		_rect.Size = new Vector2(part.Width, part.Height);
		_rect.Texture = part.SourceWidth == picture.GetWidth() && part.SourceHeight == picture.GetHeight()
			? picture
			: new AtlasTexture { Atlas = picture, Region = new Rect2(part.SourceX, part.SourceY, part.SourceWidth, part.SourceHeight) };
	}

	/// <summary>The picture over the whole of the icon again, after <see cref="ShowPicture"/> put it in a part of it.</summary>
	private void FillBox()
	{
		_rect?.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Goes back to the sprite's own picture after <see cref="SetTexture"/>.</summary>
	public void ShowSprite()
	{
		FillBox();
		var type = (CGuiIconType)GuiType;
		Place(type, _texture is null ? Vector2.Zero : FrameSize(_texture, _sprite?.FrameCount ?? 1) * type.Scale);
		ShowTexture(_texture);
	}

	private void Use(CSpriteType? sprite, string name)
	{
		FillBox();
		_sprite = sprite;
		if (sprite is null && name.Length > 0)
		{
			CDiagnostics.Problem(GuiType.File, GuiType.Line, $"interface: '{GuiType.Name}' names the sprite '{name}', which no .gfx file declares");
		}
		var type = (CGuiIconType)GuiType;

		if (sprite is { IsPieChart: true })
		{
			// Twice its size across, its scale not applied, and its position the top of its middle. Measured on the
			// shadow the politics screen's art has under it, 100 across with its middle 50 below the position; the
			// original's drawing was not read.
			Place(type, new Vector2(sprite.PieSize * 2f, sprite.PieSize * 2f));
			OffsetLeft -= sprite.PieSize;
			OffsetRight -= sprite.PieSize;
			MouseFilter = MouseFilterEnum.Stop;
			ShowTexture(null);
			QueueRedraw();
			return;
		}

		if (sprite is { IsProgressBar: true })
		{
			// The bar's two pictures: the part that is filled, and the rest of it.
			_texture = sprite.OverlayFile.Length == 0 ? null : _context.Textures.LoadFromGamePath(sprite.OverlayFile, sprite.File, sprite.OverlayFileLine);
			_rest = sprite.MaskFile.Length == 0 ? null : _context.Textures.LoadFromGamePath(sprite.MaskFile, sprite.File, sprite.MaskFileLine);
			_bar = _context.ProgressBars.Material(sprite, _texture, _rest);
			Material = _bar?.Material;
			_bar?.Set("CurrentState", _progress / 100f);
			Place(type, sprite.TileSize * type.Scale);
			MouseFilter = MouseFilterEnum.Ignore;
			ShowTexture(null);
			QueueRedraw();
			return;
		}

		if (sprite is { IsScrolling: true })
		{
			// Its picture at its size, slid along itself through its effect's ScrollTech by its frame - which wraps.
			_texture = sprite.OverlayFile.Length == 0 ? null : _context.Textures.LoadFromGamePath(sprite.OverlayFile, sprite.File, sprite.OverlayFileLine);
			_scroll = _context.ProgressBars.ScrollMaterial(sprite);
			Place(type, sprite.TileSize * type.Scale);
			MouseFilter = MouseFilterEnum.Ignore;
			if (_rect is not null)
			{
				_rect.Texture = _texture;
				_rect.Material = _scroll?.Material;
				_rect.TextureRepeat = TextureRepeatEnum.Enabled;
			}
			return;
		}

		if (sprite is { IsTileStrip: true })
		{
			// A quad of the declared size, the texture repeated its size over the file's in whole numbers each way, each copy
			// stretched to fill (CTileSprite): drawn in _Draw with the texture repeating.
			_texture = _context.Textures.Load(sprite);
			// Placed by the file's own height, as a plain sprite is, and the quad - y up, from its foot - stands the declared
			// height above that foot: the base game's, declared as tall as its file, where it always stood; BlackICE's, 1000
			// over a file of 512, rises 488 above where its file would start (CTileSprite).
			float fileHeight = (_texture?.GetHeight() ?? (int)sprite.TileSize.Y) * type.Scale;
			Place(type, new Vector2(sprite.TileSize.X, 0f) * type.Scale + new Vector2(0f, fileHeight));
			OffsetTop = OffsetBottom - sprite.TileSize.Y * type.Scale;
			MouseFilter = MouseFilterEnum.Ignore;
			TextureRepeat = TextureRepeatEnum.Enabled;
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

	/// <summary>
	/// Shows a frame of the sprite sheet, counted from one - or, for a scrolling sprite, slides its picture to that
	/// frame: (frame - 1) over its steps, the whole of its texture being its picture here.
	/// </summary>
	public void SetFrame(int frame)
	{
		if (_scroll is not null && _sprite is not null)
		{
			_frame = frame;
			_scroll.Set("CurrentState", Interface.Map.CMinimapFrame.OverlayShift(frame, _sprite.Step, 1f));
			return;
		}
		if (frame == _frame)
		{
			return;
		}
		_frame = frame;
		ShowTexture(_texture);
	}

	/// <summary>A scrolling sprite's material, made from its effect's ScrollTech; null for any other icon.</summary>
	private CProgressBarMaterial? _scroll;

	/// <summary>
	/// Draws the icon in one of the buttons' looks - the Strategic Effects window greys the rows a country does not have
	/// (<c>docs/mechanics/strategiceffects.md</c>, section 3). Plain until asked.
	/// </summary>
	public void SetLook(EButtonLook look)
	{
		if (_shield is not null)
		{
			_shield.SetLook(look);
			return;
		}
		if (_rect is null)
		{
			return;
		}
		if (_rect.Material is not ShaderMaterial material)
		{
			if (look == EButtonLook.Up)
			{
				return;
			}
			material = _context.ButtonShader.Material(_sprite);
			_rect.Material = material;
		}
		_context.ButtonShader.Set(material, look);
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
		// The share times the part of its texture the second picture fills, which is all of it here: a texture is the
		// picture alone, where the original's is padded to a power of two.
		_bar?.Set("CurrentState", hundredths / 100f);
		QueueRedraw();
	}

	/// <summary>
	/// The colour a progress bar with no picture fills its part with - the organisation bars' party colour: the effect's
	/// <c>vFirstColor</c>, in place of the sprite's <c>color</c>.
	/// </summary>
	public void SetBarColour(Color colour)
	{
		_bar?.Set("vFirstColor", colour);
		QueueRedraw();
	}

	/// <summary>A progress bar's material, made from its sprite's effect; null for any other icon.</summary>
	private CProgressBarMaterial? _bar;

	/// <summary>A pie chart's slices, each a whole percent in its colour, drawn from three o'clock against the clock, as a screenshot of the original has them.</summary>
	public void SetSlices(System.Collections.Generic.IReadOnlyList<(int Percent, Color Colour)> slices)
	{
		_slices = slices;
		QueueRedraw();
	}

	/// <summary>
	/// Where the mouse last moved over the icon, in its own terms - from its own events, so that a pointer the
	/// developer switches move counts as one moved by hand.
	/// </summary>
	public Vector2 MouseAt { get; private set; }

	/// <summary>Which slice of a pie chart a point of the icon is in, by its place in the list; -1 for none.</summary>
	public int SliceAt(Vector2 point)
	{
		Vector2 centre = Size / 2f;
		Vector2 offset = point - centre;
		if (offset.Length() > Size.X / 2f)
		{
			return -1;
		}
		// From three o'clock, against the clock, in percent.
		float turn = Mathf.PosMod(Mathf.Atan2(-offset.Y, offset.X) / Mathf.Tau, 1f) * 100f;
		float start = 0f;
		for (int i = 0; i < _slices.Count; i++)
		{
			start += _slices[i].Percent;
			if (turn < start)
			{
				return i;
			}
		}
		return -1;
	}

	/// <summary>Shows a country's flag, where the sprite is one that draws flags.</summary>
	public void SetCountry(CCountry country)
	{
		if (_sprite is not { IsMaskedShield: true })
		{
			return;
		}
		_shield?.QueueFree();
		_shield = _context.Shields.View(country, _sprite);
		if (_shield is not null && _context.Shields.Size(_sprite) is { } size)
		{
			var type = (CGuiIconType)GuiType;
			Place(type, size * type.Scale);
			Turn();                                          // about the middle of the size it has now
			AddChild(_shield);
		}
		_texture = null;
		ShowTexture(null);
	}

	/// <summary>The country's flag where the sprite draws flags and one has been given, or null.</summary>
	private CShieldView? _shield;

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
		if (_sprite is { IsPieChart: true })
		{
			DrawPie();
			return;
		}
		if (_sprite is { IsTileStrip: true })
		{
			if (_texture is not null)
			{
				// The far texture coordinate is the repeats; a source rectangle that many textures wide, the texture repeating.
				(int repeatsAcross, int repeatsDown) = Interface.Text.CTileSprite.Repeats((int)_sprite.TileSize.X,
					(int)_sprite.TileSize.Y, _texture.GetWidth(), _texture.GetHeight());
				var source = new Rect2(0f, 0f, repeatsAcross * _texture.GetWidth(), repeatsDown * _texture.GetHeight());
				DrawTextureRectRegion(_texture, new Rect2(Vector2.Zero, Size), source);
			}
			return;
		}
		if (_sprite is not { IsProgressBar: true })
		{
			return;
		}
		if (_bar is null)
		{
			return;
		}
		// The original's quad, y up: v nought along its foot and u nought at its left - so both pictures come out upside
		// down, as measured on screenshots of the original - and a bar that is not horizontal has u running up it, filling
		// from its foot (FINDINGS-progressfx.md, section 4). The effect splits the two at the share.
		_barPoints[0] = new Vector2(0f, Size.Y);
		_barPoints[1] = Vector2.Zero;
		_barPoints[2] = new Vector2(Size.X, 0f);
		_barPoints[3] = Size;
		bool across = _sprite.Horizontal;
		_barUvs[0] = Vector2.Zero;
		_barUvs[1] = across ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
		_barUvs[2] = Vector2.One;
		_barUvs[3] = across ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
		DrawPrimitive(_barPoints, BarColours, _barUvs, _texture);
	}

	private readonly Vector2[] _barPoints = new Vector2[4];
	private readonly Vector2[] _barUvs = new Vector2[4];
	private static readonly Color[] BarColours = { Colors.White, Colors.White, Colors.White, Colors.White };

	/// <summary>Each slice a fan from the middle, from three o'clock against the clock, a point every degree or so.</summary>
	private void DrawPie()
	{
		Vector2 centre = Size / 2f;
		float radius = Size.X / 2f;
		float start = 0f;
		foreach ((int percent, Color colour) in _slices)
		{
			if (percent <= 0)
			{
				continue;
			}
			float end = start + percent / 100f;
			int steps = Math.Max(2, (int)(percent * 3.6f));
			var points = new Vector2[steps + 2];
			points[0] = centre;
			for (int step = 0; step <= steps; step++)
			{
				float angle = Mathf.Lerp(start, end, step / (float)steps) * Mathf.Tau;
				points[step + 1] = centre + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * radius;
			}
			DrawColoredPolygon(points, colour);
			start = end;
		}
	}
}
