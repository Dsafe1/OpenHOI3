using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Gui;
using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// The panel for one technology, which takes the place of the skills along the foot of
/// the technology screen when a technology is chosen: the window <c>tech_details</c>.
/// See <c>docs/mechanics/technologyscreen.md</c>, section 7.
/// </summary>
public sealed class CTechDetails
{
	private const string PhotoFolder = "gfx/pictures/tech/";

	private readonly CGuiContext _context;
	private readonly CGuiWindow _window;
	private readonly CGuiIcon? _photo;
	private readonly CGuiIcon? _finished;
	private readonly CGuiIcon? _researching;
	private readonly CGuiTextBox? _name;
	private readonly CGuiTextBox? _levelName;
	private readonly CGuiTextBox? _description;
	private readonly CGuiTextBox? _difficulty;
	private readonly CGuiTextBox? _year;
	private readonly CGuiButton? _start;
	private readonly CGuiListBox? _effects;
	private CTechnology? _shown;

	/// <summary>Raised when the button that starts or stops the research is pressed.</summary>
	public event Action? StartPressed;

	/// <summary>Raised when the panel's own close button is pressed.</summary>
	public event Action? ClosePressed;

	public CTechDetails(CGuiContext context, CGuiWindow window)
	{
		_context = context;
		_window = window;
		_photo = window.FindIcon("photo");
		_finished = window.FindIcon("tech_status_finnished");
		_researching = window.FindIcon("tech_status_researching");
		_name = window.FindTextBox("name");
		_levelName = window.FindTextBox("name_expl");
		_description = window.FindTextBox("desc");
		_difficulty = window.FindTextBox("difficulty");
		_year = window.FindTextBox("year");
		_effects = window.FindListBox("effects");
		_start = window.FindButton("start");
		if (_start is not null)
		{
			_start.Pressed += () => StartPressed?.Invoke();
		}
		if (window.FindButton("close") is { } close)
		{
			close.Pressed += () => ClosePressed?.Invoke();
		}
	}

	public bool Visible
	{
		get => _window.Visible;
		set => _window.Visible = value;
	}

	public void Update(CGameState state, CCountry country, CTechnology technology)
	{
		Func<string, string> localise = _context.Localisation.Localise;
		string name = localise(technology.Name);
		int level = country.TechnologyLevels.GetValueOrDefault(technology.Name);
		bool queued = country.ResearchQueue.Contains(technology.Name);
		bool done = technology.IsSingleLevel && level >= 1;

		if (!ReferenceEquals(technology, _shown))
		{
			_shown = technology;
			ShowOnce(technology);
		}

		if (_name is not null)
		{
			_name.Text = CTechnologyScreenText.DetailName(name, technology, level);
		}
		if (_levelName is not null)
		{
			_levelName.Text = CTechnologyScreenText.LevelName(_context.Localisation.Find, country.Tag.Value, technology,
				name, technology.IsSingleLevel ? level : level + 1);
		}
		if (_difficulty is not null)
		{
			_difficulty.Text = done ? "" : CTechnologyScreenText.Difficulty(technology.Difficulty);
		}
		if (_year is not null)
		{
			_year.Text = done ? "" : CTechnologyScreenText.YearDue(technology, level).ToString(CultureInfo.InvariantCulture);
		}
		if (_finished is not null)
		{
			_finished.Visible = done;
		}
		if (_researching is not null)
		{
			_researching.Visible = queued;
		}
		if (_start is not null)
		{
			// The one button starts a project and stops it.
			_start.SetText(localise(queued ? CTechnologyScreenText.CancelKey : CTechnologyScreenText.StartKey));
			_start.Disabled = !queued && !CResearch.MayResearchNext(state, country, technology);
		}
	}

	/// <summary>What does not change while one technology is shown.</summary>
	private void ShowOnce(CTechnology technology)
	{
		string photo = PhotoFolder + technology.Name + ".tga";
		if (_photo is not null)
		{
			// A technology with no picture of its own shows the sprite's: "no image".
			if (_context.Textures.Exists(photo) && _context.Textures.LoadFromGamePath(photo) is { } picture)
			{
				_photo.SetTexture(picture);
			}
			else
			{
				_photo.SetSprite("GFX_no_tech_image");
				_photo.ShowSprite();
			}
		}
		if (_description is not null)
		{
			_description.Text = _context.Localisation.Localise(technology.Name + "_desc");
		}
		CTechCategoryIcons.ShowFinishType(_window, technology, small: false);
		CTechCategoryIcons.Fill(_context, _window.FindBox("input_box"), technology, small: false);

		// What a level is worth, one line an effect. The original writes these out in
		// words of its own, unit by unit; that has not been read, so these are the plain
		// figures the technology's file gives.
		if (_effects is null)
		{
			return;
		}
		_effects.Clear();
		foreach ((string kind, CFixedPoint value) in technology.EffectPerLevel.Values)
		{
			if (CGuiWindow.CreateEntry(_context, "detail_entry") is not { } line)
			{
				return;
			}
			if (line.FindTextBox("text") is { } text)
			{
				text.Text = _context.Localisation.Localise(kind) + ": " + (value.Raw > 0 ? "+" : "") + value;
			}
			_effects.Add(line);
		}
	}
}
