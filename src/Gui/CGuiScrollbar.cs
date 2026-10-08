using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// A <c>scrollbarType</c>: a track, a slider that runs along it and a button at each
/// end, all four of them buttons the declaration names. It scrolls a list box, and it
/// is what the production and technology screens' sliders are.
///
/// How the original lays the four out was not read; this puts a button at each end,
/// the track between them and the slider on the track. A bar that runs across has its
/// <c>leftbutton</c> and its least value on the left; **one that runs up and down has
/// them at the bottom**, which is what puts the arrows of the original's list boxes
/// the right way up. See <c>docs/mechanics/interface.md</c>, section 2.
/// </summary>
public partial class CGuiScrollbar : CGuiObject
{
	/// <summary>An end button moves the value by this many of the smallest step a press.</summary>
	private const long StepsPerPress = 10;

	private Texture2D? _track;
	private Texture2D? _less;
	private Texture2D? _more;
	private TextureRect? _slider;
	private ShaderMaterial? _sliderLook;
	private StyleBoxTexture? _trackBox;
	private float _sliderLength;
	private bool _horizontal;
	private long _minimum;
	private long _maximum;
	private long _step = 1;
	private long _value;
	private bool _dragging;
	private bool _hovered;
	private float _grab;
	private bool _locked;

	/// <summary>Raised when the player moves the slider, with where it now is. Not raised by <see cref="Value"/>.</summary>
	public event Action<CFixedPoint>? Moved;

	/// <summary>Raised when the player right-clicks it: the original locks a slider that way.</summary>
	public event Action? LockPressed;

	/// <summary>The player has hold of the slider.</summary>
	public bool Held => _dragging;

	/// <param name="length">How long to make it, for one a list box sizes; the declaration's own size when nought.</param>
	public void Initialise(CGuiScrollbarType type, CGuiContext context, float length = 0f)
	{
		_horizontal = type.Horizontal;
		_minimum = type.MinValue.Raw;
		_maximum = Math.Max(type.MaxValue.Raw, _minimum);
		_step = Math.Max(type.StepSize.Raw, 1);
		_value = Math.Clamp(type.StartValue.Raw, _minimum, _maximum);

		Texture2D? slider = Load(type, type.Slider, context, out _);
		_track = Load(type, type.Track, context, out CSpriteType? trackSprite);
		_less = Load(type, type.LeftButton, context, out _);
		_more = Load(type, type.RightButton, context, out _);
		_sliderLength = SizeAlong(slider);

		// A track that is a cornered tile keeps its edges and stretches its middle.
		if (_track is not null && trackSprite is { IsTiled: true } && trackSprite.Border != Vector2.Zero)
		{
			_trackBox = new StyleBoxTexture { Texture = _track };
			_trackBox.SetTextureMargin(Side.Left, trackSprite.Border.X);
			_trackBox.SetTextureMargin(Side.Right, trackSprite.Border.X);
			_trackBox.SetTextureMargin(Side.Top, trackSprite.Border.Y);
			_trackBox.SetTextureMargin(Side.Bottom, trackSprite.Border.Y);
		}

		float thickness = slider is null ? 16f : _horizontal ? slider.GetHeight() : slider.GetWidth();
		Vector2 size = _horizontal
			? new Vector2(length > 0f ? length : type.Size.X, Mathf.Max(type.Size.Y, thickness))
			: new Vector2(Mathf.Max(type.Size.X, thickness), length > 0f ? length : type.Size.Y);
		Place(type, size);
		MouseFilter = MouseFilterEnum.Stop;

		// The slider is a node of its own because it is a button like any other, and
		// is drawn like one: lighter under the mouse, darker while held, grey when locked.
		if (slider is not null)
		{
			_sliderLook = context.ButtonShader.Material();
			_slider = new TextureRect
			{
				Name = "Slider",
				Texture = slider,
				Material = _sliderLook,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = new Vector2(slider.GetWidth(), slider.GetHeight()),
			};
			AddChild(_slider);
		}
		MouseEntered += () => SetHovered(true);
		MouseExited += () => SetHovered(false);
		PlaceSlider();
	}

	private static Texture2D? Load(CGuiScrollbarType type, string name, CGuiContext context, out CSpriteType? sprite)
	{
		sprite = context.Sprites.Find(type.FindButton(name)?.Sprite ?? "");
		Texture2D? texture = sprite is null ? null : context.Textures.Load(sprite);
		return texture is null ? null : Frame(texture, 1, sprite!.FrameCount);
	}

	/// <summary>Where the slider is. Setting it moves the slider and tells nobody.</summary>
	public CFixedPoint Value
	{
		get => new(_value);
		set
		{
			long clamped = Math.Clamp(value.Raw, _minimum, _maximum);
			if (clamped != _value)
			{
				_value = clamped;
				PlaceSlider();
			}
		}
	}

	public CFixedPoint Maximum => new(_maximum);

	/// <summary>Gives the slider a new range, keeping its value where that is still inside it.</summary>
	public void SetRange(CFixedPoint minimum, CFixedPoint maximum)
	{
		if (minimum.Raw == _minimum && maximum.Raw == _maximum)
		{
			return;
		}
		_minimum = minimum.Raw;
		_maximum = Math.Max(maximum.Raw, _minimum);
		_value = Math.Clamp(_value, _minimum, _maximum);
		PlaceSlider();
	}

	/// <summary>A locked slider draws in grey, as any button that is switched off does, and cannot be moved.</summary>
	public bool Locked
	{
		get => _locked;
		set
		{
			if (value != _locked)
			{
				_locked = value;
				_dragging = false;
				ShowLook();
			}
		}
	}

	private float Length => _horizontal ? Size.X : Size.Y;

	private float SizeAlong(Texture2D? texture) => texture is null ? 0f : _horizontal ? texture.GetWidth() : texture.GetHeight();

	/// <summary>
	/// How far a point is from the end the least value is at: the left of a bar that
	/// runs across, the bottom of one that runs up and down.
	/// </summary>
	private float Along(Vector2 point) => _horizontal ? point.X : Size.Y - point.Y;

	/// <summary>The room the slider has to move in.</summary>
	private float Room => Length - SizeAlong(_less) - SizeAlong(_more) - _sliderLength;

	/// <summary>How far the slider's near edge is from the least value's end, for the value it has.</summary>
	private float SliderStart()
	{
		long range = _maximum - _minimum;
		float room = Room;
		return SizeAlong(_less) + (range <= 0 || room <= 0f ? 0f : room * (_value - _minimum) / range);
	}

	private void PlaceSlider()
	{
		if (_slider is null)
		{
			return;
		}
		float start = Mathf.Round(SliderStart());
		_slider.Position = _horizontal ? new Vector2(start, 0f) : new Vector2(0f, Size.Y - start - _sliderLength);
		_slider.Visible = _maximum > _minimum;
	}

	private void SetHovered(bool hovered)
	{
		_hovered = hovered;
		ShowLook();
	}

	private void ShowLook()
	{
		if (_sliderLook is not null)
		{
			CButtonShader.Set(_sliderLook, _locked ? EButtonLook.Disabled
				: _dragging ? EButtonLook.Down
				: _hovered ? EButtonLook.Over
				: EButtonLook.Up);
		}
	}

	public override void _Draw()
	{
		float less = SizeAlong(_less);
		float more = SizeAlong(_more);
		Rect2 track = _horizontal
			? new Rect2(less, 0f, Mathf.Max(Size.X - less - more, 0f), Size.Y)
			: new Rect2(0f, more, Size.X, Mathf.Max(Size.Y - less - more, 0f));
		if (_trackBox is not null)
		{
			DrawStyleBox(_trackBox, track);
		}
		else if (_track is not null)
		{
			DrawTextureRect(_track, track, tile: false);
		}

		if (_less is not null)
		{
			DrawTexture(_less, _horizontal ? Vector2.Zero : new Vector2(0f, Size.Y - less));
		}
		if (_more is not null)
		{
			DrawTexture(_more, _horizontal ? new Vector2(Size.X - more, 0f) : Vector2.Zero);
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
		{
			AcceptEvent();
			LockPressed?.Invoke();
			return;
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
		{
			AcceptEvent();
			if (!button.Pressed)
			{
				_dragging = false;
				ShowLook();
				return;
			}
			if (_locked)
			{
				return;
			}

			float at = Along(button.Position);
			float start = SliderStart();
			if (at < SizeAlong(_less))
			{
				MoveTo(_value - _step * StepsPerPress);
			}
			else if (at >= Length - SizeAlong(_more))
			{
				MoveTo(_value + _step * StepsPerPress);
			}
			else
			{
				// On the slider it is picked up where it was touched; anywhere else on the
				// track it comes to the mouse first.
				_grab = at >= start && at < start + _sliderLength ? at - start : _sliderLength / 2f;
				_dragging = true;
				ShowLook();
				MoveTo(ValueAt(at - _grab));
			}
			return;
		}

		if (@event is InputEventMouseMotion motion && _dragging)
		{
			AcceptEvent();
			MoveTo(ValueAt(Along(motion.Position) - _grab));
		}
	}

	/// <summary>The value that puts the slider's near edge that far from the least value's end.</summary>
	private long ValueAt(float sliderStart)
	{
		float room = Room;
		if (room <= 0f)
		{
			return _minimum;
		}
		float fraction = Mathf.Clamp((sliderStart - SizeAlong(_less)) / room, 0f, 1f);
		long value = _minimum + (long)Math.Round((double)fraction * (_maximum - _minimum));
		return value / _step * _step;
	}

	private void MoveTo(long value)
	{
		value = Math.Clamp(value, _minimum, _maximum);
		if (value == _value)
		{
			return;
		}
		_value = value;
		PlaceSlider();
		Moved?.Invoke(new CFixedPoint(value));
	}
}
