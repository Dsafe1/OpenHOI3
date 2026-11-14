using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

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
	private CGameState? _state;
	private CCountry? _country;

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

		// What the panel's elements say: technologyscreen.md, section 9.
		Func<string, string> localise = context.Localisation.Localise;
		if (_start is not null)
		{
			_start.Tooltip = () => _shown is null || _state is null || _country is null ? CTooltip.Empty
				: new CTooltip { Text = CTechnologyTooltips.Start(localise, _state, _country, _shown) };
		}
		if (window.FindIcon("icon_finish_type") is { } finish)
		{
			finish.Tooltip = () => _shown is null ? CTooltip.Empty : new CTooltip { Text = CTechnologyTooltips.IconFinish(localise, _shown) };
		}
		if (_difficulty is not null)
		{
			_difficulty.Tooltip = () => new CTooltip { Text = localise("DIFFICULTY_TECH") };
		}
	}

	public bool Visible
	{
		get => _window.Visible;
		set => _window.Visible = value;
	}

	/// <summary>
	/// Makes the panel for a technology, as the original does when one is pressed in the tree or the queue: its name,
	/// its level's name, year and difficulty, its picture and description, and the rows of what its level does. None of
	/// it is made again until something is pressed - a level finished meanwhile leaves the old figures up, as the
	/// original's panel does (<c>technologyscreen.md</c>, section 7).
	/// </summary>
	public void Show(CGameState state, CCountry country, CTechnology technology)
	{
		Func<string, string> localise = _context.Localisation.Localise;
		string name = localise(technology.Name);
		int level = country.TechnologyLevels.GetValueOrDefault(technology.Name);
		bool done = technology.IsSingleLevel && level >= 1;
		_state = state;
		_country = country;
		_shown = technology;
		ShowOnce(state, country, technology);

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
		Update(state, country);
	}

	/// <summary>
	/// What the panel keeps up to date as it stands: whether the technology is finished or on the queue, and the button
	/// that starts or stops it - ours, the original's routine for these not being read apart from the panel's making.
	/// </summary>
	public void Update(CGameState state, CCountry country)
	{
		if (_shown is not { } technology)
		{
			return;
		}
		Func<string, string> localise = _context.Localisation.Localise;
		int level = country.TechnologyLevels.GetValueOrDefault(technology.Name);
		bool queued = country.ResearchQueue.Contains(technology.Name);
		bool done = technology.IsSingleLevel && level >= 1;
		_state = state;
		_country = country;
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

	/// <summary>The picture, the description, the category icons and the rows of what the level does.</summary>
	private void ShowOnce(CGameState state, CCountry country, CTechnology technology)
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
		CTechCategoryIcons.Fill(_context, _window.FindBox("input_box"), technology, small: false,
			(category, weight) => _country is null ? CTooltip.Empty
				: new CTooltip { Text = CTechnologyTooltips.CategoryIcon(_context.Localisation.Localise, _country, category, weight) });

		// What its level does: the text a tree entry's tooltip gives, at the same level, laid out at the row's text box
		// width and cut into rows - a piece under two characters dropped, and an empty row always last. Each row goes into
		// the list before its text is set, so the list counts its rows by the window's own height; a row given a piece is
		// then made 180 wide and as tall as its text, and the list lays its rows out a first row's height apart.
		if (_effects is null)
		{
			return;
		}
		_effects.Clear();
		if (CGuiWindow.CreateEntry(_context, "detail_entry") is not { } first)
		{
			return;
		}
		CGuiTextBox? box = first.FindTextBox("text");
		string text = CTechnologyEffectsText.For(_context.Localisation.Localise, state, country, technology,
			CTechnologyTooltips.EntryLevel(country, technology));
		List<string> rows = box is null ? new List<string> { "" } : CTechDetailsRows.Rows(text, box.MaxWidth, box.Measure);
		for (int index = 0; index < rows.Count; index++)
		{
			CGuiWindow? line = index == 0 ? first : CGuiWindow.CreateEntry(_context, "detail_entry");
			if (line is null)
			{
				break;
			}
			_effects.Add(line);
			if (line.FindTextBox("text") is { } row)
			{
				row.Text = rows[index];
				(float width, float height) = CTechDetailsRows.RowSize(rows[index], line.Size.X, line.Size.Y, row.DrawnHeight);
				line.Size = new Godot.Vector2(width, height);
			}
		}
		_effects.Relayout();
	}
}
