using System;
using OpenHOI3.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// One slider of the production or the technology screen: a window of its own with a
/// title, the amount it holds, what it needs, a button that sets it to that, a marker
/// and the scrollbar itself. The original has one class for both screens' and so has
/// this. See <c>docs/mechanics/sliders.md</c>.
/// </summary>
public sealed class CEconomySlider
{
	private readonly CGuiTextBox? _value;
	private readonly CGuiTextBox? _need;
	private readonly CGuiIcon? _limit;

	/// <summary>Raised when the player moves the slider, with where it now is.</summary>
	public event Action<CFixedPoint>? Moved;

	/// <summary>Raised when the player right-clicks the slider, which locks and unlocks it.</summary>
	public event Action? LockPressed;

	/// <summary>Raised when the player presses the button under the title.</summary>
	public event Action? NeedPressed;

	private CEconomySlider(CGuiWindow window, string title)
	{
		Window = window;
		Scrollbar = window.FindScrollbar("actual_slider");
		_value = window.FindTextBox("value");
		_need = window.FindTextBox("need");
		// The technology screen's slider has no marker; the production screen's has.
		_limit = window.TryFindIcon("limit");

		if (window.FindTextBox("title") is { } titleBox)
		{
			titleBox.Text = title;
		}
		if (_limit is not null)
		{
			_limit.Visible = false;
		}
		if (Scrollbar is not null)
		{
			Scrollbar.Moved += value => Moved?.Invoke(value);
			Scrollbar.LockPressed += () => LockPressed?.Invoke();
		}
		if (window.TryFindButton("ic_need_button") is { } needButton)
		{
			needButton.Pressed += () => NeedPressed?.Invoke();
		}
	}

	public CGuiWindow Window { get; }

	public CGuiScrollbar? Scrollbar { get; }

	/// <summary>Makes a slider from the window type of that name, or answers null when no file declares it.</summary>
	public static CEconomySlider? Create(CGuiContext context, string windowName, string title)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, windowName);
		return window is null ? null : new CEconomySlider(window, title);
	}

	/// <summary>Puts the slider where the group says, with the two lines of text beside it.</summary>
	public void Show(CFixedPoint total, CFixedPoint amount, bool locked, string value, string need)
	{
		if (Scrollbar is not null)
		{
			// It runs from nothing to the whole of what is divided, whatever the file gave it.
			Scrollbar.SetRange(CFixedPoint.Zero, total);
			if (!Scrollbar.Held)
			{
				Scrollbar.Value = amount;
			}
			Scrollbar.Locked = locked;
		}
		if (_value is not null)
		{
			_value.Text = value;
		}
		if (_need is not null)
		{
			_need.Text = need;
		}
	}
}
