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
