using System;
using System.Globalization;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The end-of-game screen, <c>back_end</c>: the result, the player's shield and name, and each of the three factions with its members, its conditions, its points and its
/// share of the three's. It is filled once as it comes up, from the world as it then stands. See <c>docs/mechanics/endgame.md</c>, section 3.
/// </summary>
public sealed partial class CEndGameScreen : Control
{
	private CGuiContext _context = null!;
	private CGameState _state = null!;
	private CCountry _player = null!;

	/// <summary>Raised by Exit: the game is over and the front end is to come back.</summary>
	public event Action? Exited;

	public void Setup(CGuiContext context, CGameState state, CCountry player)
	{
		_context = context;
		_state = state;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "EndGame";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		// The screen's picture does not reach the edges of a wide canvas; the original's device is cleared black behind it.
		var backdrop = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);
		if (CGuiWindow.Create(_context, CEndGameText.ScreenName) is not { } window)
		{
			return;
		}
		AddChild(window);
		Func<string, string> localise = _context.Localisation.Localise;
		window.TryFindIcon("player_shield")?.SetCountry(_player);
		Set(window, "country_value", _player.Name);
		Set(window, "result", CEndGameText.ResultText(localise, _state, _player));
		(int winner, _) = CEndGameText.Result(_state);
		for (int index = 0; index < CEndGameText.Factions.Count; index++)
		{
			string faction = CEndGameText.Factions[index];
			if (window.TryFindIcon(faction + "_won") is { } won)
			{
				won.Visible = index == winner;
			}
			FillFaction(window, faction, localise);
		}
		if (window.TryFindButton("Exit") is { } exit)
		{
			exit.Pressed += () => Exited?.Invoke();
		}
	}

	private static void Set(CGuiWindow window, string name, string text)
	{
		if (window.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	private void FillFaction(CGuiWindow window, string faction, Func<string, string> localise)
	{
		if (window.TryFindButton(faction + "_icon") is { } icon && CFactions.Group(_state, faction) is { } group)
		{
			icon.SetFrame(group.FactionIcon);
		}
		Set(window, faction + "_label", localise(faction));
		Set(window, faction + "_vc", CEndGameText.ConditionsLine(localise, _state, faction));
		Set(window, faction + "_vp", CEndGameText.PointsLine(localise, _state, faction));
		window.TryFindIcon(faction + "_progress")?.SetProgress(CEndGameText.Bar(_state, faction));
		if (window.FindBox(faction + "_members") is { } members)
		{
			foreach (CCountry member in CCountryLists.FactionMembers(_state, faction))
			{
				if (CGuiWindow.CreateEntry(_context, CEndGameText.MemberRowName) is { } row)
				{
					row.TryFindIcon("flag")?.SetCountry(member);
					members.Add(row);
				}
			}
		}
		if (window.FindListBox(faction + "_goals") is { } goals)
		{
			foreach (CVictoryCondition condition in CEndGameText.Conditions(_state, faction))
			{
				if (CGuiWindow.CreateEntry(_context, CEndGameText.ConditionRowName) is not { } row)
				{
					continue;
				}
				Set(row, "name", localise(condition.Name));
				Set(row, "date", CEndGameText.Date(localise, condition));
				row.TryFindIcon("background")?.SetFrame(CVictoryWindowText.Frame(condition));
				goals.Add(row);
			}
		}
	}
}
