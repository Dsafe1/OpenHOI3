using System;
using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// A line of the research queue: a <c>current_research_entry</c> window with the
/// project's name, the level being worked on, the day it will be done, how far it has
/// got, and buttons that move it up and down the queue or take it off. See
/// <c>docs/mechanics/technologyscreen.md</c>, section 6.
/// </summary>
public sealed class CCurrentResearchEntry
{
	private static readonly (string Element, EQueueBackground Kind)[] Backgrounds =
	{
		("bg_current_green", EQueueBackground.Green), ("bg_current_orange", EQueueBackground.Orange),
		("bg_current_red", EQueueBackground.Red),
	};

	private readonly CGuiButton?[] _backgrounds = new CGuiButton?[Backgrounds.Length];
	private readonly CGuiIcon? _progress;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _level;
	private readonly CGuiTextBox? _date;
	private readonly string _localisedName;

	/// <summary>Raised when the line itself is clicked.</summary>
	public event Action? Pressed;

	/// <summary>Raised when the project is to be taken off the queue.</summary>
	public event Action? Cancelled;

	/// <summary>
	/// Raised when the project is to change its place: by one towards the front (-1) or
	/// the back (+1), or all the way to either (<see cref="int.MinValue"/>, <see cref="int.MaxValue"/>).
	/// </summary>
	public event Action<int>? Reordered;

	private CCurrentResearchEntry(CGuiContext context, CGuiWindow window, CTechnology technology)
	{
		Window = window;
		Technology = technology;
		_localisedName = context.Localisation.Localise(technology.Name);

		for (int index = 0; index < Backgrounds.Length; index++)
		{
			CGuiButton? background = window.FindButton(Backgrounds[index].Element);
			_backgrounds[index] = background;
			if (background is not null)
			{
				background.Pressed += () => Pressed?.Invoke();
			}
		}
		_progress = window.FindIcon("progress");
		_name = window.FindTextBox("name");
		_level = window.FindTextBox("level");
		_date = window.FindTextBox("data");

		Bind(window, "cancel", () => Cancelled?.Invoke());
		Bind(window, "tech_prio_maxup", () => Reordered?.Invoke(int.MinValue));
		Bind(window, "tech_prio_up", () => Reordered?.Invoke(-1));
		Bind(window, "tech_prio_down", () => Reordered?.Invoke(1));
		Bind(window, "tech_prio_maxdown", () => Reordered?.Invoke(int.MaxValue));
	}

	public CGuiWindow Window { get; }

	public CTechnology Technology { get; }

	public static CCurrentResearchEntry? Create(CGuiContext context, CTechnology technology)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "current_research_entry");
		return window is null ? null : new CCurrentResearchEntry(context, window, technology);
	}

	/// <param name="leadership">What the project is given of a day's research leadership.</param>
	public void Update(CGameState state, CCountry country, CFixedPoint leadership, Func<string, string> localise)
	{
		int level = country.TechnologyLevels.GetValueOrDefault(Technology.Name);
		EQueueBackground shown = CTechnologyScreenText.QueueBackground(leadership);
		for (int index = 0; index < Backgrounds.Length; index++)
		{
			if (_backgrounds[index] is { } background)
			{
				background.Visible = Backgrounds[index].Kind == shown;
			}
		}

		if (_name is not null)
		{
			_name.Text = CTechnologyScreenText.QueueName(_localisedName, Technology, level);
		}
		if (_level is not null)
		{
			_level.Text = CTechnologyScreenText.QueueLevel(level);
		}
		if (_date is not null)
		{
			int days = CResearch.DaysToFinish(state, country, Technology, leadership);
			_date.Text = CTechnologyScreenText.QueueDate(localise, state.Date, days);
		}
		_progress?.SetProgress(CTechnologyScreenText.QueueProgress(
			country.ResearchProgress.GetValueOrDefault(Technology.Name)));
	}

	private static void Bind(CGuiWindow window, string name, Action handler)
	{
		if (window.FindButton(name) is { } button)
		{
			button.Pressed += handler;
		}
	}
}
