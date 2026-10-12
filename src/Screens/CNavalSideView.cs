using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Screens;

/// <summary>
/// One half of the window of a battle at sea: the original's
/// <c>naval_l_combat_side</c> or <c>naval_r_combat_side</c>, with the side's flag, its
/// leader, its positioning, and one list of every ship of every fleet on it. See
/// <c>docs/mechanics/combatview.md</c>, section 12.
/// </summary>
public sealed class CNavalSideView : ICombatSideView
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCombatSide _side;
	private readonly CGuiTextBox? _shipsLabel;
	private readonly CGuiListBox? _ships;
	private readonly CGuiIcon? _shield;
	private readonly CCombatLeaderView _leader;
	private readonly CGuiTextBox? _positioning;
	private readonly List<CCombatShipEntry> _entries = new();
	private CCountryTag? _flag;

	private CNavalSideView(CGuiWindow window, CGuiContext context, CGameState state, CCombatSide side)
	{
		Window = window;
		_context = context;
		_state = state;
		_side = side;
		_shipsLabel = window.FindTextBox("ships_label");
		_ships = window.FindListBox("ships");
		_shield = window.FindIcon("shield");
		_leader = new CCombatLeaderView(window, context);
		_positioning = window.FindTextBox("positioning");
		window.SetTooltips(element => CBattleTooltips.Side(context.Localisation.Localise, state, side.Combat, side, element),
			"positioning", "leader_name");
		Update();
	}

	public CGuiWindow Window { get; }

	public static CNavalSideView? Create(CGuiContext context, CGameState state, CCombatSide side)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context,
			side.IsAttacker ? "naval_l_combat_side" : "naval_r_combat_side");
		return window is null ? null : new CNavalSideView(window, context, state, side);
	}

	/// <summary>A side at sea is there for as long as it has a fleet.</summary>
	public bool HasUnits => _side.Units.Count > 0;

	public void Update()
	{
		Set(_positioning, CCombatViewText.Positioning(_side));
		Set(_shipsLabel, CCombatViewText.ShipsLabel(_context.Localisation.Localise, _side));
		Fill();

		_leader.Update(_side);
		CCountryTag? flag = CCombatViewText.Flag(_state, _side);
		if (flag != _flag && flag is { } tag && _state.Country(tag) is { } country)
		{
			_flag = flag;
			_shield?.SetCountry(country);
		}
	}

	/// <summary>
	/// Keeps the list in step with the side's ships: while it holds as many entries as
	/// the side has ships and each entry's ship is still on the side, each is only
	/// written again; otherwise it is emptied and filled, fleet by fleet in the side's
	/// order and ship by ship in each fleet's.
	/// </summary>
	private void Fill()
	{
		if (_ships is null)
		{
			return;
		}
		var ships = new List<CSubUnit>();
		foreach (CUnit fleet in _side.Units)
		{
			ships.AddRange(fleet.SubUnits);
		}
		bool same = _entries.Count == ships.Count;
		for (int index = 0; same && index < _entries.Count; index++)
		{
			same = ships.Contains(_entries[index].Ship);
		}
		if (same)
		{
			foreach (CCombatShipEntry entry in _entries)
			{
				entry.Update();
			}
			return;
		}
		_ships.Clear();
		_entries.Clear();
		foreach (CSubUnit ship in ships)
		{
			if (CCombatShipEntry.Create(_context, _state, _side.Combat, ship, _side.IsAttacker) is { } entry)
			{
				_entries.Add(entry);
				_ships.Add(entry.Window);
			}
		}
		_ships.Relayout();
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null && box.Text != text)
		{
			box.Text = text;
		}
	}
}
