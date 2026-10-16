using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// The original's <c>datewidget</c>: a date with its hour and a pair of arrows each for
/// the year, the month, the day and the hour, the date held between the defines' start
/// and end. The air orders window has two, its start and its end. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 4.
/// </summary>
public sealed class CDateWidget
{
	private readonly CGuiWindow _window;
	private readonly CGameState _state;
	private readonly Func<string, string> _localise;
	private readonly CGuiTextBox? _date;
	private readonly List<CGuiButton> _arrows = new();

	private CDateWidget(CGuiWindow window, CGameState state, Func<string, string> localise, long hour)
	{
		_window = window;
		_state = state;
		_localise = localise;
		_date = window.FindTextBox("date");
		Hour = hour;
		Arrow("year_up", EDateStep.Year, 1);
		Arrow("year_down", EDateStep.Year, -1);
		Arrow("month_up", EDateStep.Month, 1);
		Arrow("month_down", EDateStep.Month, -1);
		Arrow("day_up", EDateStep.Day, 1);
		Arrow("day_down", EDateStep.Day, -1);
		Arrow("hour_up", EDateStep.Hour, 1);
		Arrow("hour_down", EDateStep.Hour, -1);
		Show();
	}

	/// <summary>
	/// Makes one inside a window, at the <c>positionType</c> of that name, showing an hour
	/// of the game. Null where no file declares <c>datewidget</c>.
	/// </summary>
	public static CDateWidget? Create(CGuiContext context, CGameState state, CGuiWindow owner, string place, long hour)
	{
		if (CGuiWindow.CreateEntry(context, CAirOrdersText.DateWidget) is not { } window)
		{
			return null;
		}
		if (context.Types.FindPosition(place) is { } position)
		{
			window.MoveTo(new Vector2(position.Position.X, position.Position.Y));
		}
		owner.AddChild(window);
		return new CDateWidget(window, state, context.Localisation.Localise, hour);
	}

	/// <summary>The date, as an hour of the game.</summary>
	public long Hour { get; private set; }

	/// <summary>Raised when an arrow has moved the date.</summary>
	public event Action? Changed;

	/// <summary>Moves the date on to an hour where it is earlier, as the window does a paradrop's start.</summary>
	public void HoldAtLeast(long hour)
	{
		if (Hour < hour)
		{
			Hour = hour;
			Show();
			Changed?.Invoke();
		}
	}

	/// <summary>Whether its arrows can be pressed. The date shows either way.</summary>
	public bool Enabled
	{
		set
		{
			foreach (CGuiButton arrow in _arrows)
			{
				arrow.Disabled = !value;
			}
		}
	}

	private void Arrow(string name, EDateStep step, int by)
	{
		if (_window.FindButton(name) is not { } arrow)
		{
			return;
		}
		arrow.Pressed += () =>
		{
			Hour = CAirOrdersText.Step(_state, Hour, step, by);
			Show();
			Changed?.Invoke();
		};
		_arrows.Add(arrow);
	}

	private void Show()
	{
		if (_date is not null)
		{
			_date.Text = CAirOrdersText.DateText(_localise, Hour);
		}
	}
}
