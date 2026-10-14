using System;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Screens;

/// <summary>
/// A mission row of the intelligence screen, a <c>spy_mission_entry</c>, about one country: the mission's title and a
/// button whose frame is its weight plus one, which a click raises and a right click lowers, going round. Every element
/// of it says the mission's description. See <c>docs/mechanics/espionagescreen.md</c>, sections 3 and 4.
/// </summary>
public sealed class CSpyMissionEntry
{
	public const string WindowName = "spy_mission_entry";

	private readonly CCountry _player;
	private readonly CGuiButton? _priority;
	private int _frame;

	private CSpyMissionEntry(CGuiWindow window, Func<string, string> localise, CCountry player, CCountry target, ESpyMission mission)
	{
		Window = window;
		_player = player;
		Target = target;
		Mission = mission;
		if (window.FindTextBox("title") is { } title)
		{
			title.Text = CEspionageScreenText.MissionTitle(localise, mission);
		}
		_priority = window.FindButton("priority");
		if (_priority is not null)
		{
			_priority.Pressed += () => Step(up: true);
			_priority.RightPressed += () => Step(up: false);
		}
		// The title answers too, though it lets the mouse through (the maintainer, 2026-10-14).
		window.SetTooltips(_ => CEspionageTooltips.Mission(localise, mission), "title");
		Refresh();
	}

	public CGuiWindow Window { get; }

	public CCountry Target { get; }

	public ESpyMission Mission { get; }

	/// <summary>Raised when the button has moved, with the weight to post.</summary>
	public event Action<CSpyMissionEntry, int>? WeightChanged;

	public static CSpyMissionEntry? Create(CGuiContext context, CCountry player, CCountry target, ESpyMission mission)
	{
		CGuiWindow? window = CGuiWindow.CreateEntry(context, WindowName);
		return window is null ? null : new CSpyMissionEntry(window, context.Localisation.Localise, player, target, mission);
	}

	/// <summary>Every frame: the button's frame the weight the presence holds, plus one.</summary>
	public void Refresh()
	{
		int frame = CEspionageScreenText.PriorityFrame(CGameState.Presence(_player, Target).Weight(Mission));
		if (frame != _frame)
		{
			_frame = frame;
			_priority?.SetFrame(frame);
		}
	}

	private void Step(bool up)
	{
		_frame = CEspionageScreenText.NextFrame(_frame, up);
		_priority?.SetFrame(_frame);
		WeightChanged?.Invoke(this, _frame - 1);
	}
}
