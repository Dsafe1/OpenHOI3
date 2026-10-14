using System;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Sim.Countries;

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
	private bool _checked;
	private CGuiContext? _context;

	/// <summary>Raised when the button is clicked, or its shortcut key is pressed.</summary>
	public event Action? Pressed;

	/// <summary>Raised when the button is clicked twice in quick succession, after <see cref="Pressed"/> for the first.</summary>
	public event Action? DoublePressed;

	/// <summary>
	/// Raised when the right button goes down and comes up on it. The button takes the
	/// right button only while something listens for this, so that elsewhere a right
	/// click goes on to what lies under it as before.
	/// </summary>
	public event Action? RightPressed;

	private bool _rightHeld;

	/// <summary>
	/// Takes away the key its file gives it, as the original's offer dialogs do with Accept's Enter.
	/// </summary>
	public void ClearShortcut()
	{
		_shortcut = Key.None;
	}

	/// <summary>Declared with <c>checkboxType</c>: a click ticks it or clears it.</summary>
	public bool IsCheckbox { get; private set; }

	/// <summary>
	/// Whether a checkbox is ticked. The sheet's first frame is the empty box and its
	/// second the ticked one.
	/// </summary>
	public bool Checked
	{
		get => _checked;
		set
		{
			_checked = value;
			SetFrame(value ? 2 : 1);
		}
	}

	/// <summary>Whether the mouse is being held down on it now.</summary>
	public bool IsDown => _held && !_disabled;

	/// <summary>
	/// Gives the button another sprite out of the <c>.gfx</c> files. One that no file
	/// declares, or whose texture is missing, leaves it as it was and answers false.
	/// </summary>
	public bool SetSprite(string name)
	{
		if (_context?.Sprites.Find(name) is not { } sprite || _context.Textures.Load(sprite) is not { } texture)
		{
			return false;
		}
		_sprite = sprite;
		_texture = texture;
		ShowFrame();
		return true;
	}

	public void Initialise(CGuiButtonType type, CGuiContext context)
	{
		_context = context;
		IsCheckbox = type.IsCheckbox;
		_sprite = context.Sprites.Find(type.Sprite);
		if (_sprite is null && type.Sprite.Length > 0)
		{
			CDiagnostics.Warn($"interface: '{type.Name}' names the sprite '{type.Sprite}', which no .gfx file declares");
		}
		// A masked shield has no picture until it is given a country.
		_texture = _sprite is null || _sprite.IsMaskedShield ? null : context.Textures.Load(_sprite);

		Vector2 size = _texture is not null ? FrameSize(_texture, _sprite!.FrameCount) : new Vector2(type.Size.X, type.Size.Y);
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

	/// <summary>
	/// The words on the button, in the font its type names. Blank words take the caption away, and a button drawn with a
	/// plain picture has none: the original draws a button's words only on a text sprite - its research queue's stop
	/// button shows no "--", the maintainer's screenshot of 2026-10-14.
	/// </summary>
	public void SetText(string text)
	{
		if (_sprite is { IsPlain: true })
		{
			text = "";
		}
		if (text.Trim().Length == 0)
		{
			_caption?.QueueFree();
			_caption = null;
			return;
		}
		if (_caption is null)
		{
			_caption = new CGuiCaption();
			_caption.Initialise(text, _font, ((CGuiButtonType)GuiType).Format);
			AddChild(_caption);
		}
		else
		{
			_caption.Text = text;
		}
	}

	/// <summary>
	/// Shows a country's flag, where the sprite is one that draws flags - the diplomacy screen's rows,
	/// markers and faction members are such buttons. It takes the shield's size, as an icon does, and
	/// stands where its type puts it.
	/// </summary>
	public void SetCountry(CCountry country)
	{
		if (_context is null || _sprite is not { IsMaskedShield: true } || _context.Shields.Build(country, _sprite) is not { } shield)
		{
			return;
		}
		_texture = shield;
		Place(GuiType, FrameSize(shield, 1));
		ShowFrame();
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

	/// <summary>
	/// Whether the mouse is over the button. The button knows when nothing lies on it;
	/// an icon that does says so for it. Leaving lets go of a press.
	/// </summary>
	public void SetHovered(bool hovered)
	{
		_hovered = hovered;
		if (!hovered)
		{
			_held = false;
			_rightHeld = false;
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
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
		{
			AcceptEvent();
			Click(mouse);
		}
		else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right } right && RightPressed is not null)
		{
			AcceptEvent();
			if (_disabled)
			{
				return;
			}
			if (right.Pressed)
			{
				_rightHeld = true;
			}
			else if (_rightHeld)
			{
				_rightHeld = false;
				RightPressed.Invoke();
			}
		}
	}

	/// <summary>
	/// The left mouse button going down or coming up on the button: its own, or one an
	/// icon lying over the button hands on to it.
	/// </summary>
	public void Click(InputEventMouseButton mouse)
	{
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
			if (IsCheckbox)
			{
				Checked = !Checked;
			}
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
