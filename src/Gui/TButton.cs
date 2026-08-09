using Godot;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// A live button: the sprite plus its centred caption.
///
/// The original shades hover and pressed states with <c>gfx/FX/buttonstate.fx</c>
/// rather than by swapping frames, so this approximates those states by modulating
/// the sprite.
/// </summary>
public partial class TButton : CGuiObject
{
	private static readonly Color NormalTint = Colors.White;
	private static readonly Color HoverTint = new(1.25f, 1.25f, 1.25f);
	private static readonly Color PressedTint = new(0.75f, 0.75f, 0.75f);

	[Signal]
	public delegate void PressedEventHandler();

	private TextureRect? _sprite;
	private bool _hovered;
	private bool _held;

	public void Initialise(CButtonType type, CGuiContext context)
	{
		CSpriteType? sprite = context.Sprites.Find(type.SpriteName);
		Texture2D? texture = sprite is null ? null : context.Textures.Load(sprite);

		// Frames are laid out horizontally, so one frame is that fraction of the width.
		Vector2 size = texture is null
			? new Vector2(100f, 24f)
			: new Vector2(texture.GetWidth() / (float)(sprite?.FrameCount ?? 1),
				texture.GetHeight());

		ApplyLayout(type, size);
		MouseFilter = MouseFilterEnum.Stop;

		if (texture is not null)
		{
			_sprite = new TextureRect
			{
				Name = "Sprite",
				Texture = texture,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_sprite.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			if ((sprite?.FrameCount ?? 1) > 1)
			{
				// Show only the first frame of a strip.
				var atlas = new AtlasTexture
				{
					Atlas = texture,
					Region = new Rect2(Vector2.Zero, size),
				};
				_sprite.Texture = atlas;
			}
			AddChild(_sprite);
		}

		if (type.ButtonText.Length != 0)
		{
			AddChild(CreateLabel(type, context, size));
		}

		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
	}

	private static Label CreateLabel(CButtonType type, CGuiContext context, Vector2 size)
	{
		var label = new Label
		{
			Name = "Text",
			Text = context.Localisation.Localise(type.ButtonText),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		CBitmapFont? font = context.Fonts.Find(type.ButtonFont);
		if (font?.Font is not null)
		{
			label.AddThemeFontOverride("font", font.Font);
			label.AddThemeColorOverride("font_color", font.Color);
		}
		return label;
	}

	private void OnMouseEntered()
	{
		_hovered = true;
		UpdateTint();
	}

	private void OnMouseExited()
	{
		_hovered = false;
		_held = false;
		UpdateTint();
	}

	private void UpdateTint()
	{
		if (_sprite is null)
		{
			return;
		}
		_sprite.Modulate = _held ? PressedTint : _hovered ? HoverTint : NormalTint;
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		if (mouse.Pressed)
		{
			_held = true;
			UpdateTint();
			AcceptEvent();
			return;
		}

		bool wasHeld = _held;
		_held = false;
		UpdateTint();
		if (wasHeld)
		{
			EmitSignal(SignalName.Pressed);
			AcceptEvent();
		}
	}
}
