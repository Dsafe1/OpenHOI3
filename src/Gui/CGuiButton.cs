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
	private CAlphaMask? _mask;
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

	/// <summary>Raised when the left button goes down on it, as a drag begins.</summary>
	public event Action? HeldDown;

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
		ShortcutText = "";
	}

	/// <summary>The shortcut as its tooltip writes it: the file's, or what a screen gave it.</summary>
	public string ShortcutText { get; private set; } = "";

	/// <summary>
	/// Gives the button a shortcut its screen works out - a province view's building row takes a letter of its name
	/// (<c>docs/mechanics/provinceview.md</c>, section 4).
	/// </summary>
	public void SetShortcut(string text)
	{
		_shortcut = ParseShortcut(text);
		ShortcutText = text;
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
	public bool IsDown => _held && !Off;

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
		_mask = _context.Textures.AlphaMask(sprite);
		// Drawn through the effect the new sprite names.
		if (_rect is not null)
		{
			_material = _context.ButtonShader.Material(sprite);
			_rect.Material = _material;
			Tint();
		}
		// A button is the size of its picture, as it is when made: the province view's building rows swap a large
		// default for small icons (provinceview.md, section 4).
		Place(GuiType, FrameSize(texture, sprite.FrameCount));
		ShowFrame();
		RemakeCaption();
		ShowBlank();
		return true;
	}

	/// <summary>
	/// A button on a text sprite with no font draws nothing and is never under the mouse, as the original's: its picture is
	/// never rendered (<see cref="Interface.Text.CButtonCaption.DrawsPicture"/>). The rule is the button's type's font;
	/// what the original does where code gives such a button a font afterwards was not read, and nothing here does.
	/// </summary>
	private bool IsBlank => _sprite is { IsTextSprite: true }
		&& !Interface.Text.CButtonCaption.DrawsPicture(true, ((CGuiButtonType)GuiType).Font);

	private void ShowBlank()
	{
		bool blank = IsBlank;
		if (_rect is not null)
		{
			_rect.Visible = !blank;
		}
		if (!blank)
		{
			MouseFilter = MouseFilterEnum.Stop;
			return;
		}
		_caption?.QueueFree();
		_caption = null;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void Initialise(CGuiButtonType type, CGuiContext context)
	{
		_context = context;
		IsCheckbox = type.IsCheckbox;
		_sprite = context.Sprites.Find(type.Sprite);
		if (_sprite is null && type.Sprite.Length > 0)
		{
			CDiagnostics.Problem($"interface: '{type.Name}' names the sprite '{type.Sprite}', which no .gfx file declares");
		}
		// A masked shield has no picture until it is given a country.
		_texture = _sprite is null || _sprite.IsMaskedShield ? null : context.Textures.Load(_sprite);
		_mask = _texture is null ? null : context.Textures.AlphaMask(_sprite!);

		Vector2 size = _texture is not null ? FrameSize(_texture, _sprite!.FrameCount) : new Vector2(type.Size.X, type.Size.Y);
		Place(type, size);
		MouseFilter = MouseFilterEnum.Stop;
		_shortcut = ParseShortcut(type.Shortcut);
		ShortcutText = type.Shortcut;
		// Its sounds, chosen once as it is made, as the original resolves them (docs/mechanics/sound.md, section 3).
		_clickSound = Interface.Sound.CClickSound.Choose(type.ClickSound, _sprite?.ClickSound ?? "");
		_untickSound = Interface.Sound.CClickSound.Choose(type.UpSound, _sprite?.ClickSound ?? "");

		_rect = new TextureRect
		{
			Name = "Sprite",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_material = context.ButtonShader.Material(_sprite);
		_rect.Material = _material;
		AddChild(_rect);
		ShowFrame();

		_font = context.Fonts.Find(type.Font);
		SetText(context.Localisation.Localise(type.Text));
		ShowBlank();

		MouseEntered += () => SetHovered(true);
		MouseExited += () => SetHovered(false);
	}

	/// <summary>
	/// The words on the button, in the font its type names. Blank words take the caption away, and a button drawn with a
	/// plain picture has none: the original draws a button's words only on a text sprite - its research queue's stop
	/// button shows no "--", a screenshot of the original of 2026-10-14.
	/// </summary>
	public void SetText(string text)
	{
		_captionText = text;
		if (_sprite is { IsPlain: true } || IsBlank)
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
			// Laid out as the original's button lays it out: on a sprite, in the sprite's picture as a textBoxType's text,
			// and with no sprite, as an instantTextBoxType's in the box its size gives (interface.md, section 6).
			var type = (CGuiButtonType)GuiType;
			_caption = new CGuiCaption();
			if (_sprite is not null && _texture is not null)
			{
				(int width, int height, bool cut) = Interface.Text.CButtonCaption.SpriteBox(_texture.GetWidth(),
					_texture.GetHeight(), _sprite.FrameCount, type.BorderSize.X, type.BorderSize.Y);
				_caption.InitialiseSprite(text, _font, width, height, type.BorderSize.X, type.BorderSize.Y, cut);
			}
			else
			{
				_caption.InitialiseText(text, _font, type.Format, type.Orientation,
					Interface.Text.CButtonCaption.TextWidth(type.Size.X));
			}
			AddChild(_caption);
		}
		else
		{
			_caption.Text = text;
		}
	}

	/// <summary>The words last asked for, which a new sprite lays out again or, a plain one, hides.</summary>
	private string _captionText = "";

	/// <summary>The caption laid out again, for a picture of another size or kind.</summary>
	private void RemakeCaption()
	{
		if (_caption is { } caption)
		{
			RemoveChild(caption);
			caption.QueueFree();
			_caption = null;
		}
		SetText(_captionText);
	}

	/// <summary>
	/// Shows a country's flag, where the sprite is one that draws flags - the diplomacy screen's rows,
	/// markers and faction members are such buttons. It takes the shield's size, as an icon does, and
	/// stands where its type puts it.
	/// </summary>
	public void SetCountry(CCountry country)
	{
		if (_context is null || _sprite is not { IsMaskedShield: true } || _context.Shields.View(country, _sprite) is not { } shield
			|| _context.Shields.Size(_sprite) is not { } size)
		{
			return;
		}
		_shield?.QueueFree();
		_shield = shield;
		_texture = null;
		_mask = null;
		Place(GuiType, size);
		// Drawn under the caption, where the picture is.
		AddChild(shield);
		if (_rect is not null)
		{
			MoveChild(shield, _rect.GetIndex() + 1);
		}
		Tint();
		TurnShield();
	}

	/// <summary>The country's flag where the sprite draws flags and one has been given, or null.</summary>
	private CShieldView? _shield;

	/// <summary>
	/// A shield turned by its type's rotation, as a turned flag icon is: about its own middle, a pixel and a half to the
	/// right of it (<see cref="CGuiIcon"/>; <c>docs/mechanics/interface.md</c>, section 5) - the lobby's shields, a quarter
	/// back, stand upright and apart.
	/// </summary>
	private void TurnShield()
	{
		if (GuiType is not CGuiButtonType { Rotation: not 0f } type)
		{
			return;
		}
		var size = new Vector2(OffsetRight - OffsetLeft, OffsetBottom - OffsetTop);
		PivotOffset = size / 2f + new Vector2(ShieldPivotShift, 0f);
		Rotation = -type.Rotation;
	}

	/// <summary>How far right of its middle a turned shield turns, measured on the battle window's flags.</summary>
	private const float ShieldPivotShift = 1.5f;

	/// <summary>A button that is not enabled draws in grey and cannot be pressed.</summary>
	public bool Disabled
	{
		get => _disabled;
		set
		{
			_disabled = value;
			Tint();
			ShowFrame();
		}
	}

	/// <summary>Not pressable: disabled by its screen, or greyed by itself or what holds it (<see cref="CGuiObject.Greying"/>).</summary>
	private bool Off => _disabled || IsGreyed;

	/// <summary>Drawn again for a change of greying above it, or for coming into a greyed part of the tree.</summary>
	internal override void ShowGreying()
	{
		Tint();
		ShowFrame();
	}

	public override void _EnterTree()
	{
		ShowGreying();
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
			_rect.Texture = Frame(_texture, ShownFrame, _sprite?.FrameCount ?? 1);
		}
	}

	/// <summary>
	/// The frame drawn: the one chosen, but for a checkbox switched off its fourth, ticked or not, where its sheet has
	/// four - the grey box of a row that cannot be given (<c>docs/mechanics/interface.md</c>, section 7).
	/// </summary>
	private int ShownFrame => IsCheckbox && Off && (_sprite?.FrameCount ?? 1) >= DisabledCheckboxFrame
		? DisabledCheckboxFrame
		: _frame;

	private const int DisabledCheckboxFrame = 4;

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
		EButtonLook look = Off ? EButtonLook.Disabled
			: _held ? EButtonLook.Down
			: _hovered ? EButtonLook.Over
			: EButtonLook.Up;
		// A shield's look is its flag's technique of maskedflag.fx; any other button's buttonstate.fx's.
		_shield?.SetLook(look);
		if (_material is not null)
		{
			_context!.ButtonShader.Set(_material, look);
		}
	}

	/// <summary>
	/// Whether a point of the button takes the mouse: anywhere on it, but where its sprite has <c>transparencecheck</c>
	/// only where the frame it shows is drawn - the speed indicator lets the speed-up button beside it have the part of
	/// its square it leaves empty (<c>docs/mechanics/interface.md</c>, section 5). The frame is counted from one, a
	/// nought taken as the first and one past the last as the last, as the original's test takes it.
	/// </summary>
	public override bool _HasPoint(Vector2 point)
	{
		if (point.X < 0f || point.Y < 0f || point.X >= Size.X || point.Y >= Size.Y || IsBlank)
		{
			return false;
		}
		if (_mask is null || _sprite is null || Size.X <= 0f || Size.Y <= 0f)
		{
			return true;
		}
		int frames = Math.Max(1, _sprite.FrameCount);
		int frame = Math.Clamp(ShownFrame, 1, frames);
		int frameWidth = _mask.Width / frames;
		int x = (frame - 1) * _mask.Width / frames + (int)(point.X * frameWidth / Size.X);
		int y = (int)(point.Y * _mask.Height / Size.Y);
		return _mask.IsSolid(x, y);
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
			if (Off)
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
		if (Off)
		{
			return;
		}

		if (mouse.Pressed)
		{
			_held = true;
			Tint();
			HeldDown?.Invoke();
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
			// After its action, unless it is still sounding: a button's click, a checkbox's tick or untick
			// (docs/mechanics/sound.md, section 5). *Ours*: a checkbox here is ticked as the button comes up, and so sounds
			// then, where the original's ticks and sounds as it goes down. A button's shortcut sounds nothing, as the original's.
			_context?.Sounds?.PlayUnlessPlaying(IsCheckbox && !Checked ? _untickSound : _clickSound);
		}
	}

	/// <summary>The sound a click makes, and a checkbox's tick: its own key, else its sprite's, else <c>click</c>.</summary>
	private string _clickSound = Interface.Sound.CClickSound.Default;

	/// <summary>A checkbox's untick: its <c>upsound</c>, else its sprite's <c>clicksound</c>, else <c>click</c> [exe].</summary>
	private string _untickSound = Interface.Sound.CClickSound.Default;

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (_shortcut == Key.None || Off || !IsVisibleInTree()
			|| @event is not InputEventKey { Pressed: true } key || !IsShortcut(key.Keycode, _shortcut)
			|| (key.Echo && !Repeats(_shortcut)))
		{
			return;
		}
		GetViewport().SetInputAsHandled();
		Pressed?.Invoke();
	}

	/// <summary>Whether a key is a shortcut's: the number pad's Enter is Enter.</summary>
	internal static bool IsShortcut(Key pressed, Key shortcut)
	{
		return pressed == shortcut || (shortcut == Key.Enter && pressed == Key.KpEnter);
	}

	/// <summary>
	/// Whether a shortcut presses again and again while its key is held, as a held key repeats: every one but Escape,
	/// which closes a window and would then open the in-game menu and close it again.
	/// </summary>
	internal static bool Repeats(Key shortcut)
	{
		return shortcut != Key.Escape;
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
			"PAGE_UP" => Key.Pageup,
			"PAGE_DOWN" => Key.Pagedown,
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
