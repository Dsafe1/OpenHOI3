using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Screens;

/// <summary>
/// A faction's box beside the alignment triangle, a <c>faction_box_window</c>: its symbol and name, the
/// bar of its share of the world's victory points, and a flag a member, the leader first. Each frame the
/// bar is set and the members made again where they have changed. See
/// <c>docs/mechanics/diplomacyscreen.md</c>, section 4.2.
/// </summary>
public sealed class CFactionBox
{
	public const string WindowName = "faction_box_window";
	public const string MemberWindowName = "faction_member_entry";

	private readonly CGuiContext _context;
	private readonly CGameState _state;
	private readonly CIdeologyGroup _faction;
	private readonly CGuiOverlappingBox? _members;
	private readonly CGuiIcon? _bar;
	private List<CCountry> _shown = new();

	private CFactionBox(CGuiWindow window, CGuiContext context, CGameState state, CIdeologyGroup faction)
	{
		Window = window;
		_context = context;
		_state = state;
		_faction = faction;

		Func<string, string> localise = context.Localisation.Localise;
		if (window.FindButton("faction_symbol_button") is { } symbol)
		{
			symbol.SetFrame(faction.FactionIcon);
			symbol.Tooltip = () => CDiplomacyTooltips.Faction(localise, _faction, _state);
		}
		if (window.FindTextBox("name") is { } name)
		{
			name.Text = localise(faction.Faction);
		}
		_bar = window.FindIcon("victory_progress");
		if (_bar is not null)
		{
			_bar.PassiveTooltip = () => CDiplomacyTooltips.VictoryProgress(localise, _state, _faction);
		}
		_members = window.FindBox("faction_box");
	}

	public CGuiWindow Window { get; }

	/// <summary>Raised when a member's flag is clicked, but REB's.</summary>
	public event Action<CCountry>? Chosen;

	public static CFactionBox? Create(CGuiContext context, CGameState state, CIdeologyGroup faction)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CFactionBox(window, context, state, faction);
	}

	/// <summary>
	/// The bar - the faction's share in whole percent, cut - and the members, made again
	/// where they are not the ones shown.
	/// </summary>
	public void Refresh()
	{
		_bar?.SetProgress((int)(OpenHOI3.Sim.Wars.CFactionProgress.Share(_state, _faction.Faction) / 1000));
		List<CCountry> members = CCountryLists.FactionMembers(_state, _faction.Faction);
		if (_members is null || members.SequenceEqual(_shown))
		{
			return;
		}
		_shown = members;
		_members.Clear();
		foreach (CCountry member in members)
		{
			if (CGuiWindow.CreateEntry(_context, MemberWindowName) is not { } entry)
			{
				continue;
			}
			if (entry.FindButton("faction_button") is { } flag)
			{
				flag.SetCountry(member);
				CCountry shown = member;
				flag.Pressed += () =>
				{
					if (shown.Tag.Value != "REB")
					{
						Chosen?.Invoke(shown);
					}
				};
				flag.Tooltip = () => CDiplomacyTooltips.FactionMember(_context.Localisation.Localise, _state, shown);
			}
			if (entry.TryFindIcon("cgm_faction_leave_icon") is { } leaving)
			{
				leaving.Visible = false;
			}
			_members.Add(entry);
		}
	}
}
