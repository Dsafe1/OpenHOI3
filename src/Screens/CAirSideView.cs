using System.Collections.Generic;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// One half of the window of a battle in the air or of a bombing: the original's
/// <c>air_combat_side</c>, which both halves are made from, with the side's flag, its
/// leader, and one list - its wings, and for the bombed side of a bombing what of the
/// province is being bombed as well. See <c>docs/mechanics/combatview.md</c>, section 11.
/// </summary>
public sealed class CAirSideView : ICombatSideView
{
	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CCombatSide _side;
	private readonly CGuiTextBox? _planesLabel;
	private readonly CGuiListBox? _planes;
	private readonly CGuiIcon? _shield;
	private readonly CGuiTextBox? _leaderName;
	private readonly List<CCombatWingEntry> _entries = new();
	private CCountryTag? _flag;

	private CAirSideView(CGuiWindow window, CGuiContext context, CGameState state, CCombatSide side)
	{
		Window = window;
		_context = context;
		_state = state;
		_side = side;
		_planesLabel = window.FindTextBox("planes_label");
		_planes = window.FindListBox("planes");
		_shield = window.FindIcon("shield");
		_leaderName = window.FindTextBox("leader_name");
		Update();
	}

	public CGuiWindow Window { get; }

	public static CAirSideView? Create(CGuiContext context, CGameState state, CCombatSide side)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, "air_combat_side");
		return window is null ? null : new CAirSideView(window, context, state, side);
	}

	/// <summary>
	/// Whether the half keeps the window up. A side of an air battle and the bombers of
	/// a bombing do while their list has a row; the bombed side of a bombing always
	/// does, with nobody on it too.
	/// </summary>
	public bool HasUnits => CAirCombatText.KeepsWindowUp(_side, _entries.Count);

	public void Update()
	{
		Set(_planesLabel, CAirCombatText.PlanesLabel(_context.Localisation.Localise, _side));
		Fill();

		// A battle's leader is not built yet: a side has none, and its portrait stays the empty one.
		Set(_leaderName, CCombatViewText.LeaderName(_context.Localisation.Localise, _side));
		CCountryTag? flag = CCombatViewText.Flag(_state, _side);
		if (flag != _flag && flag is { } tag && _state.Country(tag) is { } country)
		{
			_flag = flag;
			_shield?.SetCountry(country);
		}
	}

	/// <summary>
	/// Keeps the list in step with what the side has: while it holds the same rows each
	/// is only written again; otherwise it is emptied and filled in the rows' order.
	/// </summary>
	private void Fill()
	{
		if (_planes is null)
		{
			return;
		}
		List<CCombatRow> rows = CAirCombatText.Rows(_state, _side);
		bool same = _entries.Count == rows.Count;
		for (int index = 0; same && index < _entries.Count; index++)
		{
			same = rows.Contains(_entries[index].Row);
		}
		if (same)
		{
			foreach (CCombatWingEntry entry in _entries)
			{
				entry.Update();
			}
			return;
		}
		_planes.Clear();
		_entries.Clear();
		foreach (CCombatRow row in rows)
		{
			if (CCombatWingEntry.Create(_context, _state, _side.Combat, row) is { } entry)
			{
				_entries.Add(entry);
				_planes.Add(entry.Window);
			}
		}
		_planes.Relayout();
	}

	private static void Set(CGuiTextBox? box, string text)
	{
		if (box is not null && box.Text != text)
		{
			box.Text = text;
		}
	}
}
