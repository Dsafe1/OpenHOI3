using System.Collections.Generic;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One half of the battle window: the original's <c>combat_side_l</c> or
/// <c>combat_side_r</c>, with the side's flag, its leader, its tactic, and its two
/// lists - the units on the front and the ones in reserve. See
/// <c>docs/mechanics/combatview.md</c>, section 4.
/// </summary>
public sealed class CCombatSideView : ICombatSideView
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCombatSide _side;
	private readonly CGuiTextBox? _committedLabel;
	private readonly CGuiTextBox? _reservesLabel;
	private readonly CGuiListBox? _committed;
	private readonly CGuiListBox? _reserves;
	private readonly CGuiIcon? _shield;
	private readonly CGuiIcon? _initiative;
	private readonly CCombatLeaderView _leader;
	private readonly CGuiTextBox? _tacticName;
	private readonly CGuiIcon? _tacticPicture;
	private readonly List<CCombatUnitEntry> _front = new();
	private readonly List<CCombatUnitEntry> _waiting = new();
	private readonly CCountryTag? _player;
	private CCountryTag? _flag;
	private CCombatTactic? _tactic;

	private CCombatSideView(CGuiWindow window, CGuiContext context, CGameState state, CCombatSide side, CCountryTag? player)
	{
		Window = window;
		_context = context;
		_state = state;
		_side = side;
		_player = player;
		_committedLabel = window.FindTextBox("committed_label");
		_reservesLabel = window.FindTextBox("reserves_label");
		_committed = window.FindListBox("committed");
		_reserves = window.FindListBox("reserves");
		_shield = window.FindIcon("shield");
		_initiative = window.FindIcon("initiative_indicator");
		_leader = new CCombatLeaderView(window, context);
		_tacticName = window.FindTextBox("tactic_name");
		_tacticPicture = window.FindIcon("tactic_picture");
		window.SetTooltips(element => CBattleTooltips.Side(context.Localisation.Localise, state, side.Combat, side, element),
			"tactic_name", "leader_name");
		Update();
	}

	public CGuiWindow Window { get; }

	/// <param name="player">Whose knowledge decides which units show the unknown picture; nobody's knows everything.</param>
	public static CCombatSideView? Create(CGuiContext context, CGameState state, CCombatSide side, CCountryTag? player)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, side.IsAttacker ? "combat_side_l" : "combat_side_r");
		return window is null ? null : new CCombatSideView(window, context, state, side, player);
	}

	/// <summary>Whether either of the side's lists has anything in it. A battle window with an empty side goes away.</summary>
	public bool HasUnits => _front.Count + _waiting.Count > 0;

	public void Update()
	{
		Set(_committedLabel, CCombatViewText.CommittedLabel(_context.Localisation.Localise, _state, _side));
		Set(_reservesLabel, CCombatViewText.ReservesLabel(_context.Localisation.Localise, _side));
		Fill(_committed, _front, _side.Front, false);
		// Only the attacker's reserves say their chance of coming up; the defender's say a unit's whole.
		Fill(_reserves, _waiting, _side.Reserves, _side.IsAttacker);

		_leader.Update(_side);
		bool initiative = CCombatViewText.HasInitiative(_side);
		if (_initiative is not null && _initiative.Visible != initiative)
		{
			_initiative.Visible = initiative;
		}

		CCountryTag? flag = CCombatViewText.Flag(_state, _side);
		if (flag != _flag && flag is { } tag && _state.Country(tag) is { } country)
		{
			_flag = flag;
			_shield?.SetCountry(country);
		}

		// A side with no tactic keeps what it showed.
		if (_side.Tactic is { } tactic && !ReferenceEquals(tactic, _tactic))
		{
			_tactic = tactic;
			Set(_tacticName, CCombatViewText.TacticName(_context.Localisation.Localise, _side));
			string picture = CCombatViewText.TacticPicture(_side);
			if (picture.Length != 0 && _context.Textures.Exists(picture)
				&& _context.Textures.LoadFromGamePath(picture) is { } texture)
			{
				_tacticPicture?.SetTexture(texture);
			}
		}
	}

	/// <summary>
	/// Keeps a list of the window in step with a list of the side: while they hold the
	/// same units each entry is only written again; otherwise the list is emptied and
	/// filled in the side's order.
	/// </summary>
	private void Fill(CGuiListBox? list, List<CCombatUnitEntry> entries, List<CUnit> units, bool chance)
	{
		if (list is null)
		{
			return;
		}
		bool same = entries.Count == units.Count;
		for (int index = 0; same && index < entries.Count; index++)
		{
			same = units.Contains(entries[index].Unit);
		}
		if (same)
		{
			foreach (CCombatUnitEntry entry in entries)
			{
				entry.Update();
			}
			return;
		}
		list.Clear();
		entries.Clear();
		foreach (CUnit unit in units)
		{
			if (CCombatUnitEntry.Create(_context, _state, _side.Combat, unit, chance, _player) is { } entry)
			{
				entries.Add(entry);
				list.Add(entry.Window);
			}
		}
		list.Relayout();
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null && box.Text != text)
		{
			box.Text = text;
		}
	}
}
