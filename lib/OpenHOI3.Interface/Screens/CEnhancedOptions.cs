using System;
using OpenHOI3.Content.Settings;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The choices an enhanced game adds to the game options' Game tab, as they stand between an opening and Apply: four
/// rows, each stepped by a pair of arrows as the autosave frequency above them is, each wrapping at its ends. Made again
/// from the settings at every opening and stored by Apply. <c>docs/mechanics/gameoptions.md</c>, section 5.
/// </summary>
public sealed class CEnhancedOptions
{
	/// <summary>The timer's steps in minutes, nought for none: the arrows go from one to the next.</summary>
	public static readonly int[] MinuteSteps = { 0, 5, 10, 15, 20, 30, 45, 60, 90, 120, 180, 240 };

	/// <summary>The rows, in the order they stand below the autosave frequency.</summary>
	public static readonly string[] Rows = { "premonth", "timed", "edge", "event" };

	public int PreMonthDays { get; private set; }

	public int TimedMinutes { get; private set; }

	public bool EdgeScrolling { get; private set; } = true;

	public EEventWindowPlace EventWindow { get; private set; }

	/// <summary>Every row made again from the settings.</summary>
	public void Refresh(CEnhancedSettings settings)
	{
		CEnhancedSettings held = settings.Held();
		PreMonthDays = held.PreMonthDays;
		TimedMinutes = held.TimedMinutes;
		EdgeScrolling = held.EdgeScrolling;
		EventWindow = held.EventWindow;
	}

	/// <summary>The rows as settings.</summary>
	public CEnhancedSettings Apply()
	{
		return new CEnhancedSettings(PreMonthDays, TimedMinutes, EdgeScrolling, EventWindow);
	}

	/// <summary>A row stepped up or down by its arrows.</summary>
	public void Step(string row, int by)
	{
		switch (row)
		{
			case "premonth":
				// None, then 1 to 27 days, and round again.
				PreMonthDays = Wrap(PreMonthDays + by, CEnhancedSettings.MostDays + 1);
				break;
			case "timed":
				TimedMinutes = StepMinutes(TimedMinutes, by);
				break;
			case "edge":
				EdgeScrolling = !EdgeScrolling;
				break;
			case "event":
				EventWindow = (EEventWindowPlace)Wrap((int)EventWindow + by, 3);
				break;
		}
	}

	/// <summary>
	/// The timer's next step up or down from a figure, round at the ends. A figure between two steps, which only the
	/// file can give, goes to the step beside it on the side asked.
	/// </summary>
	public static int StepMinutes(int minutes, int by)
	{
		int at = Array.IndexOf(MinuteSteps, minutes);
		if (at >= 0)
		{
			return MinuteSteps[Wrap(at + by, MinuteSteps.Length)];
		}
		if (by > 0)
		{
			foreach (int step in MinuteSteps)
			{
				if (step > minutes)
				{
					return step;
				}
			}
			return MinuteSteps[0];
		}
		for (int i = MinuteSteps.Length - 1; i >= 0; i--)
		{
			if (MinuteSteps[i] < minutes)
			{
				return MinuteSteps[i];
			}
		}
		return MinuteSteps[^1];
	}

	private static int Wrap(int value, int count) => ((value % count) + count) % count;
}
