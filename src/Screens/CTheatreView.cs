using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The theatres screen, <c>country_theatres</c>: the player's theatres and the allies', and the one selected - its stances, its forces, its objectives and a row for
/// each front. The selected half is filled when the selection changes or the screen opens, and is otherwise left as it stands. See
/// <c>docs/mechanics/theatresscreen.md</c>.
/// </summary>
public sealed partial class CTheatreView : CCountryView
{
	private readonly List<CTheatreEntry> _ownRows = new();
	private readonly List<CTheatreEntry> _alliedRows = new();
	private CGuiListBox? _own;
	private CGuiListBox? _allied;
	private CGuiListBox? _areas;
	private CGuiWindow? _selected;
	private CGuiButton? _goTo;
	private CGuiButton? _build;
	private int[]? _needs;
	private CUnit? _filledFor;
	private readonly Random _draw = new();
	private CTheatre? _selection;
	private bool _dirty;

	protected override string WindowName => CTheatreScreenText.WindowName;
	protected override string TopColourName => CTheatreScreenText.TopColourName;

	/// <summary>What is selected on the map: a selected theatre headquarters is the theatre the screen opens on.</summary>
	public Func<IReadOnlyList<CUnit>>? MapSelection { get; set; }

	/// <summary>The go-to of the player's own theatre: its headquarters selected and shown.</summary>
	public event Action<CUnit>? HeadquartersChosen;

	/// <summary>A front's go-to: the map shown at the province that names it.</summary>
	public event Action<int>? ProvinceChosen;

	protected override void Build(CGuiWindow window)
	{
		_own = window.FindListBox("my_theatres");
		_allied = window.FindListBox("allied_theatres");
		_selected = window.FindWindow("selected_theatre");
		_areas = _selected?.FindListBox("areas");
		_goTo = _selected?.TryFindButton("goto_button");
		if (_goTo is not null)
		{
			_goTo.Pressed += GoToSelected;
		}
		_build = _selected?.TryFindButton("build");
		if (_build is not null)
		{
			_build.Pressed += Build;
		}
	}

	protected override void Opened()
	{
		RefillLists();
		_dirty = true;
	}

	protected override void Refresh()
	{
		(CTheatre Theatre, CUnit Headquarters)? shown = Shown();
		if (shown is { } found && _dirty)
		{
			_dirty = false;
			Fill(found.Theatre, found.Headquarters);
		}
	}

	/// <summary>
	/// The lists made again. A selected theatre headquarters chooses its theatre, and failing that the first row of each list in turn does, where nothing has been
	/// chosen yet.
	/// </summary>
	private void RefillLists()
	{
		var state = Simulation.State;
		CUnit? onMap = MapSelection?.Invoke().FirstOrDefault(unit => unit.Kind == EUnitKind.Theatre);
		Rebuild(_own, _ownRows, CTheatreScreenText.Rows(state, Player), onMap);
		Rebuild(_allied, _alliedRows, CTheatreScreenText.AlliedRows(state, Player), null);
	}

	private void Rebuild(CGuiListBox? list, List<CTheatreEntry> rows, List<(CTheatre Theatre, CUnit Headquarters)> theatres, CUnit? onMap)
	{
		if (list is null)
		{
			return;
		}
		list.Clear();
		rows.Clear();
		foreach ((CTheatre theatre, CUnit headquarters) in theatres)
		{
			if (CTheatreEntry.Create(Context, Simulation.State, theatre, headquarters, Choose) is not { } row)
			{
				continue;
			}
			rows.Add(row);
			list.Add(row.Window);
			if (ReferenceEquals(headquarters, onMap) || _selection is null)
			{
				_selection = theatre;
			}
		}
	}

	private void Choose(CTheatre theatre)
	{
		_selection = theatre;
		_dirty = true;
	}

	/// <summary>The selected theatre where it is still in the game, and otherwise the first of the player's own, without choosing it.</summary>
	private (CTheatre Theatre, CUnit Headquarters)? Shown()
	{
		var state = Simulation.State;
		if (_selection is not null && CTheatres.Headquarters(state, _selection) is { } headquarters && !IsGone(_selection))
		{
			return (_selection, headquarters);
		}
		List<(CTheatre Theatre, CUnit Headquarters)> own = CTheatreScreenText.Rows(state, Player);
		return own.Count > 0 ? own[0] : null;
	}

	private bool IsGone(CTheatre theatre)
	{
		return Simulation.State.Country(theatre.Country) is not { } owner || !owner.Strategy.Theatres.Contains(theatre);
	}

	private void Fill(CTheatre theatre, CUnit headquarters)
	{
		if (_selected is null)
		{
			return;
		}
		var state = Simulation.State;
		Func<string, string> localise = Context.Localisation.Localise;
		CUnitPlan plan = headquarters.Plan;
		Set(_selected.FindTextBox("title"), headquarters.Name);
		if (state.Country(headquarters.Owner) is { } owner)
		{
			_selected.TryFindButton("country_icon")?.SetCountry(owner);
		}
		Set(_selected.FindTextBox("land_stance"), localise(CTheatreScreenText.LandStanceKey(plan.Stance)));
		Set(_selected.FindTextBox("air_stance"), localise(CTheatreScreenText.AirStanceKey(plan.AirStance)));
		Set(_selected.FindTextBox("naval_stance"), localise(CTheatreScreenText.NavalStanceKey(plan.NavalStance)));
		Set(_selected.FindTextBox("forces"), CTheatreScreenText.Forces(state, theatre, headquarters));
		_filledFor = headquarters;
		_needs = CTheatreScreenText.Needs(state, Player, headquarters, null);
		Set(_selected.FindTextBox("immediate_needs"), CTheatreScreenText.NeedsText(localise, state, _needs));
		if (_build is not null)
		{
			_build.Disabled = _needs is null || headquarters.Owner != Player.Tag;
		}
		string objectives = CTheatreScreenText.Objectives(localise, state, headquarters);
		Set(_selected.FindTextBox("objective"), objectives.Length > 0 ? objectives : localise("NO_OBJECTIVES"));
		Set(_selected.FindTextBox("current_operation"), CTheatreScreenText.CurrentOperation(localise, state, theatre, headquarters));
		_areas?.Clear();
		foreach (CFront front in CTheatreScreenText.AreaFronts(state, Player, theatre))
		{
			if (CAreaEntry.Create(Context, Simulation, Player, front, headquarters, Draw, ChooseProvince) is { } row)
			{
				_areas?.Add(row.Window);
			}
		}
	}

	/// <summary>The original draws on a generator nobody seeds, so a pick is arbitrary; this one is the screen's own.</summary>
	private uint Draw() => (uint)_draw.NextInt64(uint.MaxValue);

	/// <summary>The orders made and sent. The line above is left as it stands until the theatre is filled again, as the original leaves it.</summary>
	private void Build()
	{
		if (_needs is null || _filledFor is null)
		{
			return;
		}
		foreach (CCommand command in new CTheatreBuildSteps(Simulation.State, Player, _filledFor.Id, _needs, Draw).Run())
		{
			Simulation.Post(command);
		}
	}

	/// <summary>Only the player's own headquarters is selected and shown; either way the screen shuts.</summary>
	private void GoToSelected()
	{
		if (Shown() is { } shown && shown.Headquarters.Owner == Player.Tag)
		{
			HeadquartersChosen?.Invoke(shown.Headquarters);
		}
		Close();
	}

	private void ChooseProvince(int province)
	{
		ProvinceChosen?.Invoke(province);
		Close();
	}
}
