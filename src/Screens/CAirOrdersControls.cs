using System;
using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// What the air orders window has that the naval one does not: the four shapes of a
/// mission's area and the area itself, the three sliders and their figures, the boxes of
/// the hours, the two dates with "continuous", and the readout of the day where the
/// mission begins. <see cref="COrdersWindow"/> owns the list and accept. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 4, and <c>docs/mechanics/aircombat.md</c>,
/// section 2.
/// </summary>
public sealed class CAirOrdersControls
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCountry _player;
	private readonly CUnit _unit;
	private readonly CGuiWindow _window;
	private readonly Dictionary<EAirAreaShape, CGuiButton> _shapeButtons = new();
	private readonly CGuiScrollbar? _range;
	private readonly CGuiScrollbar? _angle;
	private readonly CGuiScrollbar? _width;
	private readonly CGuiButton? _day;
	private readonly CGuiButton? _night;
	private readonly CGuiButton? _both;
	private readonly CGuiButton? _continuous;
	private readonly CGuiTextBox? _secondTarget;
	private readonly CDateWidget? _start;
	private readonly CDateWidget? _end;

	private long _takenRange = CAirOrdersText.RangeStart;
	private long _takenAngle = CAirOrdersText.AngleStart;
	private long _takenWidth = CAirOrdersText.WidthStart;
	private int _province;

	/// <summary>Raised when the area or what may be accepted has changed: the map and accept are to be redone.</summary>
	public event Action? Changed;

	public CAirOrdersControls(CGuiContext context, CGameState state, CCountry player, CUnit unit, CGuiWindow window,
		int province, EAirAreaShape shape)
	{
		_context = context;
		_state = state;
		_player = player;
		_unit = unit;
		_window = window;
		_province = province;
		Shape = shape;

		foreach (EAirAreaShape each in new[] { EAirAreaShape.Point, EAirAreaShape.Region, EAirAreaShape.Circle, EAirAreaShape.Cone })
		{
			if (window.FindButton(CAirOrdersText.ShapeButton(each)) is { } button)
			{
				EAirAreaShape pressed = each;
				button.Pressed += () => PressShape(pressed);
				_shapeButtons[each] = button;
			}
		}

		_range = Slider(CAirOrdersText.RangeSlider, CAirOrdersText.RangeStart, value =>
		{
			if (CAirOrdersText.Takes(_takenRange, value))
			{
				_takenRange = value;
				Area?.SetRange(CAirOrdersText.RangeAt(_unit, value));
				AreaMoved();
			}
		});
		_angle = Slider(CAirOrdersText.AngleSlider, CAirOrdersText.AngleStart, value =>
		{
			if (CAirOrdersText.Takes(_takenAngle, value))
			{
				_takenAngle = value;
				Area?.SetAngle(CAirOrdersText.TurnAt(value));
				AreaMoved();
			}
		});
		_width = Slider(CAirOrdersText.WidthSlider, CAirOrdersText.WidthStart, value =>
		{
			if (CAirOrdersText.Takes(_takenWidth, value))
			{
				_takenWidth = value;
				Area?.SetWidth(CAirOrdersText.TurnAt(value));
				AreaMoved();
			}
		});

		// Both hours ticked, day and night clear, and "continuous" ticked.
		_day = Box(CAirOrdersText.DayBox, false);
		_night = Box(CAirOrdersText.NightBox, false);
		_both = Box(CAirOrdersText.BothBox, true);
		foreach (CGuiButton? box in new[] { _day, _night, _both })
		{
			if (box is not null)
			{
				box.Pressed += ChooseTime;
			}
		}
		_continuous = Box(CAirOrdersText.ContinuousBox, true);
		if (_continuous is not null)
		{
			_continuous.Pressed += SwitchDates;
		}
		_secondTarget = window.FindTextBox(CAirOrdersText.SecondTargetText);

		// The start is now, the end a month on.
		long now = state.TotalHours;
		_start = CDateWidget.Create(context, state, window, CAirOrdersText.StartPlace, now);
		_end = CDateWidget.Create(context, state, window, CAirOrdersText.EndPlace, CAirOrdersText.AddMonths(now, 1));
		if (_start is not null)
		{
			_start.Changed += ShowDayTime;
		}
		SwitchDates();
		ShowShape();
		ShowFigures();
		ShowDayTime();
	}

	/// <summary>The shape a mission's area is made in.</summary>
	public EAirAreaShape Shape { get; private set; }

	/// <summary>The provinces being given, for the order ticked; null with nothing ticked.</summary>
	public CAirArea? Area { get; private set; }

	/// <summary>0 by day, 1 by night, 2 both.</summary>
	public int Time { get; private set; } = CMission.DayAndNight;

	/// <summary>The hour the mission may first fly.</summary>
	public long StartHour => _start?.Hour ?? _state.TotalHours;

	/// <summary>The hour after which it is over: the defines' end where it is continuous.</summary>
	public long EndHour => _continuous is not { Checked: false } || _end is null
		? CAirOrdersText.ContinuousEnd(_state)
		: _end.Hour;

	/// <summary>Whether accept may be pressed, as far as the area goes: it lists a province.</summary>
	public bool HasProvinces => Area is { Provinces.Count: > 0 };

	/// <summary>
	/// Makes the area for the order ticked, or none: the shape's list, then the angle, the
	/// width and the range the sliders last gave.
	/// </summary>
	public void MakeArea(EUnitOrder order)
	{
		if (order == EUnitOrder.None)
		{
			Area = null;
			Changed?.Invoke();
			return;
		}
		Area = new CAirArea(_state, _player, _unit, order, _province, Shape);
		Area.SetShape(Shape);
		Area.SetAngle(CAirOrdersText.TurnAt(_takenAngle));
		Area.SetWidth(CAirOrdersText.TurnAt(_takenWidth));
		Area.SetRange(CAirOrdersText.RangeAt(_unit, _takenRange));
		Changed?.Invoke();
	}

	/// <summary>The window has been moved to another province.</summary>
	public void SetProvince(int province)
	{
		_province = province;
		ShowDayTime();
	}

	/// <summary>
	/// A right click on the map while the window is up. A circle or a cone is aimed at the
	/// province, or with shift has it added or taken away; a point or a region has it, or
	/// its region, added with shift. Answers true where the window is instead to be moved
	/// to the province: a point or a region without shift.
	/// </summary>
	public bool RightClick(int province, bool shift)
	{
		if (Area is null)
		{
			return false;
		}
		switch (Shape)
		{
			case EAirAreaShape.Circle:
			case EAirAreaShape.Cone:
				Area.SetSecondTarget(province, shift);
				break;
			case EAirAreaShape.Point when shift:
				Area.Add(province);
				break;
			case EAirAreaShape.Region when shift:
				Area.AddRegionOf(province);
				break;
			default:
				return true;
		}
		Changed?.Invoke();
		return false;
	}

	/// <summary>
	/// Writes the hint under the sliders where nothing can be accepted. Where something can,
	/// the line is left as it was - so it reads empty until the first time it cannot.
	/// </summary>
	public void ShowAccept(bool mayAccept)
	{
		if (!mayAccept && _secondTarget is not null)
		{
			_secondTarget.Text = _context.Localisation.Localise(CAirOrdersText.NoSecondTargetKey);
		}
	}

	/// <summary>A shape button, or the remembered shape on opening: its button pressed and the area made again.</summary>
	public void PressShape(EAirAreaShape shape)
	{
		Shape = shape;
		ShowShape();
		if (Area is not null)
		{
			Area.SetShape(shape);
			Changed?.Invoke();
		}
	}

	private void ShowShape()
	{
		foreach ((EAirAreaShape shape, CGuiButton button) in _shapeButtons)
		{
			button.SetFrame(shape == Shape ? 2 : 1);
		}
		if (_range is not null)
		{
			_range.Locked = !CAirOrdersText.UsesRange(Shape);
		}
		if (_angle is not null)
		{
			_angle.Locked = !CAirOrdersText.UsesArc(Shape);
		}
		if (_width is not null)
		{
			_width.Locked = !CAirOrdersText.UsesArc(Shape);
		}
	}

	private void AreaMoved()
	{
		ShowFigures();
		Changed?.Invoke();
	}

	private void ShowFigures()
	{
		Func<string, string> localise = _context.Localisation.Localise;
		SetText(CAirOrdersText.RangeValue, CAirOrdersText.RangeFigure(localise, CAirOrdersText.RangeAt(_unit, _takenRange)));
		SetText(CAirOrdersText.AngleValue,
			CAirOrdersText.DegreesFigure(localise, "ORDER_ANGLE_VAL", CAirOrdersText.TurnAt(_takenAngle)));
		SetText(CAirOrdersText.WidthValue,
			CAirOrdersText.DegreesFigure(localise, "ORDER_WIDTH_VAL", CAirOrdersText.TurnAt(_takenWidth)));
	}

	private void ChooseTime()
	{
		var ticked = new CTimeBoxes(_day?.Checked ?? false, _night?.Checked ?? false, _both?.Checked ?? false);
		Time = CAirOrdersText.ChooseTime(Time, ticked, out CTimeBoxes shown);
		if (_day is not null)
		{
			_day.Checked = shown.Day;
		}
		if (_night is not null)
		{
			_night.Checked = shown.Night;
		}
		if (_both is not null)
		{
			_both.Checked = shown.Both;
		}
	}

	/// <summary>Both dates' arrows are switched off while "continuous" is ticked.</summary>
	private void SwitchDates()
	{
		bool fixedDates = _continuous is { Checked: false };
		if (_start is not null)
		{
			_start.Enabled = fixedDates;
		}
		if (_end is not null)
		{
			_end.Enabled = fixedDates;
		}
	}

	private void ShowDayTime()
	{
		Func<string, string> localise = _context.Localisation.Localise;
		var province = _state.Province(_province);
		bool night = CAirOrdersText.IsNightAtStart(_state, province, StartHour);
		SetText(CAirOrdersText.DayTimeTitle, CAirOrdersText.DayTimeText(localise, province.Name));
		_window.FindIcon(CAirOrdersText.DayTimeIcon)?.SetFrame(CAirOrdersText.DayTimeFrame(night));
		SetText(CAirOrdersText.DayTimeDescription, localise(CAirOrdersText.DayTimeKey(night)));
	}

	private CGuiScrollbar? Slider(string name, long start, Action<long> moved)
	{
		if (_window.FindScrollbar(name) is not { } slider)
		{
			return null;
		}
		slider.Value = new CFixedPoint(start);
		slider.Moved += value => moved(value.Raw);
		return slider;
	}

	private CGuiButton? Box(string name, bool ticked)
	{
		if (_window.FindButton(name) is not { } box)
		{
			return null;
		}
		box.Checked = ticked;
		return box;
	}

	private void SetText(string name, string text)
	{
		if (_window.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}
}
