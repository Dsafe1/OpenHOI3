using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// A row of the selected theatre's <c>areas</c>, an <c>area_entry</c>: a front, the province that names it, its picture, the odds and what it needs. A front no
/// province can name stays blank and answers nothing. See <c>docs/mechanics/theatresscreen.md</c>, section 5.
/// </summary>
public sealed class CAreaEntry
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CUnit _headquarters;
	private readonly CFront _front;
	private readonly CCountry _player;
	private readonly CSimulation _simulation;
	private readonly Func<uint> _draw;
	private readonly CGuiTextBox? _needsLine;
	private readonly CGuiButton? _build;
	private int[]? _needs;
	private readonly CGuiButton? _stance;
	private int _stanceShown;

	private CAreaEntry(CGuiWindow window, CGuiContext context, CSimulation simulation, CCountry player, CFront front, CUnit headquarters, Func<uint> draw, Action<int> provinceChosen)
	{
		CGameState state = simulation.State;
		_simulation = simulation;
		_player = player;
		_front = front;
		_draw = draw;
		_needsLine = window.FindTextBox("forces_needs");
		_build = window.TryFindButton("build");
		Window = window;
		_context = context;
		_state = state;
		_headquarters = headquarters;
		_stance = window.TryFindButton("stance");
		if (CTheatreScreenText.NamingProvince(state, front) is not { } province || front.Enemy is not { } enemy)
		{
			return;
		}
		foreach (string sprite in CTheatreScreenText.ContinentSprites(state, province, enemy))
		{
			if (context.Sprites.Find(sprite) is not null)
			{
				window.TryFindIcon("theatres_continent")?.SetSprite(sprite);
				break;
			}
		}
		if (window.FindTextBox("title") is { } title)
		{
			title.Text = CTheatreScreenText.AreaTitle(context.Localisation.Localise, province, state.Country(enemy)?.Name ?? enemy.Value);
		}
		if (window.FindTextBox("odds_description") is { } odds)
		{
			odds.Text = context.Localisation.Localise(CTheatreScreenText.OddsKey(state, headquarters));
		}
		window.TryFindIcon("current_odds")?.SetFrame(CTheatreScreenText.RatioFrame(state, headquarters));
		_stanceShown = headquarters.Plan.Stance;
		ShowStance();
		if (_stance is not null)
		{
			_stance.Pressed += () =>
			{
				_stanceShown = CTheatreScreenText.NextStance(_stanceShown);
				ShowStance();
				UpdateNeeds();
			};
		}
		if (window.TryFindButton("goto_button") is { } goTo)
		{
			goTo.Pressed += () => provinceChosen(province.Id);
		}
		if (_build is not null)
		{
			_build.Pressed += Build;
		}
		UpdateNeeds();
	}

	public CGuiWindow Window { get; }

	public static CAreaEntry? Create(CGuiContext context, CSimulation simulation, CCountry player, CFront front, CUnit headquarters, Func<uint> draw, Action<int> provinceChosen)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, CTheatreScreenText.AreaRowName);
		return window is null ? null : new CAreaEntry(window, context, simulation, player, front, headquarters, draw, provinceChosen);
	}

	/// <summary>The figures asked for again: only an entry the new answer has above nought is written over, which leaves one that has since gone (the original's).</summary>
	private void UpdateNeeds()
	{
		CGameState state = _simulation.State;
		int size = state.SubUnitTypes.Count + 1;
		_needs ??= new int[size];
		if (CPlanWants.Needed(state, _headquarters, _front.Provinces) is { } fresh)
		{
			for (int index = 0; index < size; index++)
			{
				if (fresh[index] > 0)
				{
					_needs[index] = fresh[index];
				}
			}
			CTheatreScreenText.Subtract(_player, _headquarters, _needs);
		}
		ShowNeeds();
	}

	private void ShowNeeds()
	{
		CGameState state = _simulation.State;
		bool any = _needs is not null && System.Array.Exists(_needs, need => need > 0);
		if (_needsLine is not null)
		{
			_needsLine.Text = CTheatreScreenText.NeedsText(_context.Localisation.Localise, state, any ? _needs : null);
		}
		if (_build is not null)
		{
			_build.Disabled = !any || _headquarters.Owner != _player.Tag;
		}
	}

	private void Build()
	{
		if (_needs is null)
		{
			return;
		}
		foreach (CCommand command in new CTheatreBuildSteps(_simulation.State, _player, _headquarters.Id, _needs, _draw).Run())
		{
			_simulation.Post(command);
		}
		ShowNeeds();
	}

	private void ShowStance()
	{
		if (_stance is not null)
		{
			_stance.SetText(_context.Localisation.Localise(CTheatreScreenText.LandStanceKey(_stanceShown)));
		}
	}
}
