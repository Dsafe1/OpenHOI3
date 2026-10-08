using System;
using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// One technology in the tree: a <c>tech_entry</c> window. Which of its four
/// backgrounds shows, what its line reads and how far its bar is filled are
/// <see cref="CTechnologyScreenText"/>'s. See
/// <c>docs/mechanics/technologyscreen.md</c>, section 4.
/// </summary>
public sealed class CTechTreeEntry
{
	private static readonly (string Element, ETechBackground Kind)[] Backgrounds =
	{
		("bg_finnished", ETechBackground.Finished), ("bg_cant", ETechBackground.Cannot),
		("bg_oneshot", ETechBackground.OneShot), ("bg_endless", ETechBackground.Endless),
	};

	private readonly CGuiButton?[] _backgrounds = new CGuiButton?[Backgrounds.Length];
	private readonly CGuiIcon? _progress;
	private readonly CGuiIcon? _researching;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _difficulty;
	private readonly CGuiTextBox? _year;
	private readonly string _localisedName;

	/// <summary>Raised when the entry is clicked, and when it is clicked twice.</summary>
	public event Action? Pressed;
	public event Action? DoublePressed;

	private CTechTreeEntry(CGuiContext context, CGuiWindow window, CTechnology technology)
	{
		Window = window;
		Technology = technology;
		_localisedName = context.Localisation.Localise(technology.Name);

		// Whichever background is showing is what takes the click.
		for (int index = 0; index < Backgrounds.Length; index++)
		{
			CGuiButton? background = window.FindButton(Backgrounds[index].Element);
			_backgrounds[index] = background;
			if (background is not null)
			{
				background.Pressed += () => Pressed?.Invoke();
				background.DoublePressed += () => DoublePressed?.Invoke();
			}
		}

		_progress = window.FindIcon("progress");
		_researching = window.FindIcon("tech_status_researching");
		_name = window.FindTextBox("name");
		_difficulty = window.FindTextBox("difficulty");
		_year = window.FindTextBox("year");

		CTechCategoryIcons.ShowFinishType(window, technology, small: true);
		CTechCategoryIcons.Fill(context, window.FindBox("input_box"), technology, small: true);
	}

	public CGuiWindow Window { get; }

	public CTechnology Technology { get; }

	public static CTechTreeEntry? Create(CGuiContext context, CTechnology technology)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "tech_entry");
		return window is null ? null : new CTechTreeEntry(context, window, technology);
	}

	public void Update(CGameState state, CCountry country, Func<string, string> localise)
	{
		int level = country.TechnologyLevels.GetValueOrDefault(Technology.Name);
		bool mayResearch = CResearch.MayResearchNext(state, country, Technology);
		ETechBackground shown = CTechnologyScreenText.Background(Technology, level, mayResearch);
		for (int index = 0; index < Backgrounds.Length; index++)
		{
			if (_backgrounds[index] is { } background)
			{
				background.Visible = Backgrounds[index].Kind == shown;
			}
		}

		if (_researching is not null)
		{
			_researching.Visible = country.ResearchQueue.Contains(Technology.Name);
		}
		_progress?.SetProgress(CTechnologyScreenText.Progress(Technology, level));
		if (_name is not null)
		{
			_name.Text = CTechnologyScreenText.EntryName(localise, _localisedName, Technology, level);
		}

		bool showsCost = CTechnologyScreenText.ShowsCost(Technology, level);
		if (_difficulty is not null)
		{
			_difficulty.Text = showsCost ? CTechnologyScreenText.Difficulty(Technology.Difficulty) : "";
		}
		if (_year is not null)
		{
			_year.Text = showsCost
				? CTechnologyScreenText.YearDue(Technology, level).ToString(System.Globalization.CultureInfo.InvariantCulture)
				: "";
		}
	}
}
