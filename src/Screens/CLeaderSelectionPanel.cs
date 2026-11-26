using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// The window a leader is chosen in: the original's <c>leader_selection_panel</c>,
/// opened from a unit's panel. At its head the leader the unit had as it opened, with
/// the two buttons that promote and demote him; under it a row for every leader the unit
/// may be given, sorted by one of four keys and narrowed by a filter of traits; and the
/// buttons that leave the unit with nobody, take every leader of the country off its
/// unit after asking, and turn the country's own choosing of leaders off and on. A
/// leader chosen, "no leader", "remove all" and "back" each hide it, and its owner ends
/// it once hidden. See <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public sealed class CLeaderSelectionPanel
{
	/// <summary>The sort, which the original keeps for as long as it runs, whatever game is loaded.</summary>
	private static ELeaderSort s_sort = ELeaderSort.Name;
	private static bool s_reversed;

	private readonly CGuiContext _context;
	private readonly CSimulation _simulation;
	private readonly CCountry? _player;
	private readonly CUnit _unit;
	private readonly CCountry? _owner;
	private readonly CLeader? _leader;
	private readonly ELeaderType _type;
	private readonly Dictionary<ELeaderType, List<CTrait?>> _kept;
	private readonly CLeaderTraitFilter _filter;
	private readonly CGuiWindow _window;
	private readonly CGuiWindow? _filterWindow;
	private readonly CGuiListBox? _list;
	private readonly CGuiIcon? _rank;
	private readonly List<CLeaderEntry> _rows = new();
	private readonly List<CTraitFilterRow> _filterRows = new();

	/// <summary>Raised by "remove all": whoever owns the window asks first.</summary>
	public event Action? RemoveAllPressed;

	private CLeaderSelectionPanel(Control parent, CGuiWindow window, CGuiContext context, CSimulation simulation,
		CCountry? player, CUnit unit, Dictionary<ELeaderType, List<CTrait?>> kept)
	{
		_window = window;
		_context = context;
		_simulation = simulation;
		_player = player;
		_unit = unit;
		_leader = unit.Leader;
		_type = CLeaders.TypeFor(unit);
		_kept = kept;
		_filter = CLeaderTraitFilter.Load(kept.GetValueOrDefault(_type), simulation.State.Traits);
		_owner = simulation.State.Country(unit.Owner);
		parent.AddChild(window);
		// Before the rows and the trait icons are put in, which answer for themselves.
		if (_owner is { } owner)
		{
			window.SetTooltips(name => CLeaderWindowTooltips.Panel(context.Localisation.Localise, simulation.State, owner,
				unit, _leader, name));
		}

		Button("back_button", Close);
		Button("noleader_button", () =>
		{
			Post(new CAssignLeaderCommand(_unit.Owner, _unit.Id, null));
			Close();
		});
		Button("removeall_button", () =>
		{
			RemoveAllPressed?.Invoke();
			Close();
		});
		Button("sort_name", () => Resort(ELeaderSort.Name));
		Button("sort_skill", () => Resort(ELeaderSort.Skill));
		Button("sort_rank", () => Resort(ELeaderSort.Rank));
		Button("sort_traits", () => Resort(ELeaderSort.Traits));
		if (window.FindButton("filter_traits") is { } filter)
		{
			filter.Pressed += ToggleFilter;
			filter.RightPressed += () =>
			{
				_filter.AllOn(_simulation.State.Traits);
				ShowFilter();
				Fill();
			};
		}
		if (window.FindButton("auto_assign") is { } autoAssign)
		{
			autoAssign.Checked = _player?.AutoAssignLeaders ?? false;
			autoAssign.Pressed += () =>
			{
				if (_player is not null)
				{
					Post(new CToggleAutoAssignLeadersCommand(_player.Tag));
				}
			};
		}

		// The head: the unit's leader as the window opened, or the original's empty one.
		if (window.FindTextBox("selected_leader_name") is { } name)
		{
			name.Text = CLeaderWindowText.Name(_leader, context.Localisation.Localise);
		}
		if (window.FindTextBox("selected_leader_skill") is { } skill)
		{
			skill.Text = CLeaderWindowText.Skill(simulation.State, _leader, context.Localisation.Localise);
		}
		CTraitIconEntry.Fill(context, window.FindBox("selected_leader_traits"), _leader);
		_rank = window.FindIcon("selected_leader_rank");
		CGuiButton? promote = window.FindButton("promote");
		CGuiButton? demote = window.FindButton("demote");
		if (_leader is { } leader)
		{
			CLeaderEntry.ShowPortrait(context, window.FindIcon("selected_photo"), leader);
			if (promote is not null)
			{
				promote.Pressed += () => Promote(leader, false);
			}
			if (demote is not null)
			{
				demote.Pressed += () => Promote(leader, true);
			}
		}
		else
		{
			if (promote is not null)
			{
				promote.Disabled = true;
			}
			if (demote is not null)
			{
				demote.Disabled = true;
			}
		}

		// A replay takes no order: the window is there to be read (docs/replay.md, section 6).
		if (LooksOn)
		{
			foreach (string pressed in new[] { "noleader_button", "removeall_button", "auto_assign", "promote", "demote" })
			{
				if (window.FindButton(pressed) is { } button)
				{
					button.Disabled = true;
				}
			}
		}

		_list = window.FindListBox("leaderlist");
		Fill();

		_filterWindow = CGuiWindow.Create(context, CLeaderWindowText.FilterWindowName);
		if (_filterWindow is not null)
		{
			parent.AddChild(_filterWindow);
			MakeFilterRows();
			_filterWindow.Visible = false;
		}
		Update();
	}

	/// <summary>Opens the window for a unit, or answers null where no file declares it.</summary>
	public static CLeaderSelectionPanel? Open(Control parent, CGuiContext context, CSimulation simulation, CCountry? player,
		CUnit unit, Dictionary<ELeaderType, List<CTrait?>> kept)
	{
		CGuiWindow? window = CGuiWindow.Create(context, CLeaderWindowText.WindowName);
		return window is null ? null : new CLeaderSelectionPanel(parent, window, context, simulation, player, unit, kept);
	}

	public bool IsShown => _window.Visible;

	/// <summary>Each frame: the head's rank, and every row's.</summary>
	public void Update()
	{
		CLeaderEntry.SetRankFrame(_rank, CLeaderWindowText.RankFrame(_leader));
		foreach (CLeaderEntry row in _rows)
		{
			row.Update();
		}
	}

	/// <summary>Keeps the filter for the next window and hides this one.</summary>
	public void Close()
	{
		Save();
		_window.Visible = false;
		if (_filterWindow is not null)
		{
			_filterWindow.Visible = false;
		}
	}

	/// <summary>Takes the window down for good.</summary>
	public void Destroy()
	{
		Save();
		_window.QueueFree();
		_filterWindow?.QueueFree();
	}

	private void Save()
	{
		_kept[_type] = _filter.Save();
	}

	private void Button(string name, Action pressed)
	{
		if (_window.FindButton(name) is { } button)
		{
			button.Pressed += pressed;
		}
	}

	private bool LooksOn => !_simulation.TakesCommands;

	private void Post(CCommand command)
	{
		if (command.MayExecute(_simulation.State))
		{
			_simulation.Post(command);
		}
	}

	private void Promote(CLeader leader, bool down)
	{
		Post(new CPromoteLeaderCommand(leader.Id, down));
	}

	private void Resort(ELeaderSort pressed)
	{
		(s_sort, s_reversed) = CLeaderWindowText.Resort(s_sort, s_reversed, pressed);
		Fill();
	}

	/// <summary>
	/// The list made again from the leaders, keeping the rows whose leaders stay in
	/// their places.
	/// </summary>
	private void Fill()
	{
		if (_list is null)
		{
			return;
		}
		List<CLeader> leaders = CLeaderWindowText.Leaders(_simulation.State, _unit, _filter, s_sort, s_reversed);
		bool same = leaders.Count == _rows.Count;
		for (int i = 0; same && i < leaders.Count; i++)
		{
			same = _rows[i].Leader == leaders[i];
		}
		if (same)
		{
			return;
		}
		_list.Clear();
		_rows.Clear();
		foreach (CLeader leader in leaders)
		{
			if (CLeaderEntry.Create(_context, leader, _unit, RowTooltip(leader),
				CLeaderWindowText.Skill(_simulation.State, leader, _context.Localisation.Localise)) is { } row)
			{
				row.Chosen += chosen =>
				{
					if (LooksOn)
					{
						return;
					}
					Post(new CAssignLeaderCommand(_unit.Owner, _unit.Id, chosen.Id));
					Close();
				};
				row.Promoted += Promote;
				_rows.Add(row);
				_list.Add(row.Window);
			}
		}
		_list.Relayout();
	}

	/// <summary>A row's tooltips: its own, and where it says nothing the window's.</summary>
	private Func<string, CTooltip> RowTooltip(CLeader leader)
	{
		if (_owner is not { } owner)
		{
			return _ => CTooltip.Empty;
		}
		return name => CLeaderWindowTooltips.Row(_context.Localisation.Localise, _simulation.State, owner, _unit, _leader,
			leader, name);
	}

	private void MakeFilterRows()
	{
		if (_filterWindow?.FindListBox("filterslist") is not { } list)
		{
			return;
		}
		foreach (CTrait? trait in CLeaderWindowText.FilterTraits(_simulation.State, _type))
		{
			if (CTraitFilterRow.Create(_context, trait, _filter.IsOn(trait)) is { } row)
			{
				row.Ticked += (ticked, on) =>
				{
					_filter.Set(ticked, on);
					Fill();
				};
				row.Soloed += soloed =>
				{
					_filter.Solo(soloed);
					ShowFilter();
					Fill();
				};
				_filterRows.Add(row);
				list.Add(row.Window);
			}
		}
		list.Relayout();
	}

	private void ShowFilter()
	{
		foreach (CTraitFilterRow row in _filterRows)
		{
			row.Show(_filter.IsOn(row.Trait));
		}
	}

	private void ToggleFilter()
	{
		if (_filterWindow is not null)
		{
			_filterWindow.Visible = !_filterWindow.Visible;
		}
	}
}
