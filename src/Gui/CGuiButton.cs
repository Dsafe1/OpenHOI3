using System;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// A <c>guiButtonType</c> or a <c>checkboxType</c>: a sprite that can be clicked, with
/// its caption over it. Like an icon it takes its size from its texture.
///
/// The original shades the hovered, pressed and disabled states with
/// <c>gfx/FX/buttonstate.fx</c> and does not swap frames for them; so does this, with
/// <see cref="CButtonShader"/>. A button's frames are for whoever owns it to choose
/// between - the speed indicator's six, the pause bar's two.
/// </summary>
public partial class CGuiButton : CGuiObject
{
	private CSpriteType? _sprite;
	private ShaderMaterial? _material;
	private Texture2D? _texture;
	private TextureRect? _rect;
	private CGuiCaption? _caption;
	private CGuiFont? _font;
	private Key _shortcut = Key.None;
	private int _frame = 1;
	private bool _hovered;
	private bool _held;
	private bool _disabled;

	/// <summary>Raised when the button is clicked, or its shortcut key is pressed.</summary>
	public event Action? Pressed;

	/// <summary>Raised when the button is clicked twice in quick succession, after <see cref="Pressed"/> for the first.</summary>
	public event Action? DoublePressed;

	public void Initialise(CGuiButtonType type, CGuiContext context)
	{
		_sprite = context.Sprites.Find(type.Sprite);
		if (_sprite is null && type.Sprite.Length > 0)
		{
			CDiagnostics.Warn($"interface: '{type.Name}' names the sprite '{type.Sprite}', which no .gfx file declares");
		}
		_texture = _sprite is null ? null : context.Textures.Load(_sprite);

		Vector2 size = _texture is null ? Vector2.Zero : FrameSize(_texture, _sprite!.FrameCount);
		Place(type, size);
		MouseFilter = MouseFilterEnum.Stop;
		_shortcut = ParseShortcut(type.Shortcut);

		_rect = new TextureRect
		{
			Name = "Sprite",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_material = context.ButtonShader.Material();
		_rect.Material = _material;
		AddChild(_rect);
		ShowFrame();

		_font = context.Fonts.Find(type.Font);
		SetText(context.Localisation.Localise(type.Text));

		MouseEntered += () => SetHovered(true);
		MouseExited += () => SetHovered(false);
	}

	/// <summary>The words on the button, in the font its type names. Blank words take the caption away.</summary>
	public void SetText(string text)
	{
		if (text.Trim().Length == 0)
		{
			_caption?.QueueFree();
			_caption = null;
			return;
		}
		if (_caption is null)
		{
			_caption = new CGuiCaption();
			_caption.Initialise(text, _font);
			AddChild(_caption);
		}
		else
		{
			_caption.Text = text;
		}
	}

	/// <summary>A button that is not enabled draws in grey and cannot be pressed.</summary>
	public bool Disabled
	{
		get => _disabled;
		set
		{
			_disabled = value;
			Tint();
		}
	}

	/// <summary>Shows a frame of the sprite sheet, counted from one.</summary>
	public void SetFrame(int frame)
	{
		if (frame == _frame)
		{
			return;
		}
		_frame = frame;
		ShowFrame();
	}

	private void ShowFrame()
	{
		if (_rect is not null && _texture is not null)
		{
			_rect.Texture = Frame(_texture, _frame, _sprite?.FrameCount ?? 1);
		}
	}

	private void SetHovered(bool hovered)
	{
		_hovered = hovered;
		if (!hovered)
		{
			_held = false;
		}
		Tint();
	}

	private void Tint()
	{
		if (_material is not null)
		{
			CButtonShader.Set(_material, _disabled ? EButtonLook.Disabled
				: _held ? EButtonLook.Down
				: _hovered ? EButtonLook.Over
				: EButtonLook.Up);
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			return;
		}
		AcceptEvent();
		if (_disabled)
		{
			return;
		}

		if (mouse.Pressed)
		{
			_held = true;
			Tint();
			if (mouse.DoubleClick)
			{
				DoublePressed?.Invoke();
			}
			return;
		}

		// A press counts when the button is let go over what it went down on.
		bool wasHeld = _held;
		_held = false;
		Tint();
		if (wasHeld)
		{
			Pressed?.Invoke();
		}
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (_shortcut == Key.None || _disabled || !IsVisibleInTree()
			|| @event is not InputEventKey { Pressed: true, Echo: false } key || key.Keycode != _shortcut)
		{
			return;
		}
		GetViewport().SetInputAsHandled();
		Pressed?.Invoke();
	}

	/// <summary>
	/// A shortcut as an interface file names one: a function key, <c>ESCAPE</c>,
	/// <c>ENTER</c>, or a single letter or digit. Anything else has no key here yet.
	/// </summary>
	private static Key ParseShortcut(string name)
	{
		if (name.Length == 0)
		{
			return Key.None;
		}
		if (name.Length == 1)
		{
			char letter = char.ToUpperInvariant(name[0]);
			return letter is >= 'A' and <= 'Z' or >= '0' and <= '9' ? (Key)letter : Key.None;
		}
		return name.ToUpperInvariant() switch
		{
			"ESCAPE" => Key.Escape,
			"ENTER" or "RETURN" => Key.Enter,
			"SPACE" => Key.Space,
			"TAB" => Key.Tab,
			"F1" => Key.F1,
			"F2" => Key.F2,
			"F3" => Key.F3,
			"F4" => Key.F4,
			"F5" => Key.F5,
			"F6" => Key.F6,
			"F7" => Key.F7,
			"F8" => Key.F8,
			"F9" => Key.F9,
			"F10" => Key.F10,
			"F11" => Key.F11,
			"F12" => Key.F12,
			_ => Key.None,
		};
	}
}
