using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The row of alert icons under the top bar. The original has sixteen; the one built is
/// the one that says units are waiting to be placed, and a click on it opens the deploy
/// menu. See <c>docs/mechanics/mapunits.md</c>, section 5.
/// </summary>
public partial class CAlerts : Control
{
	private const string WindowName = "alerticon_window";
	private const string IconName = "alerticon";
	private const string BannerName = "alerticon_banner";
	private const string StartName = "alerticon_startposition";

	/// <summary>The alert a faction near its victory conditions raises [exe: the sixteen's number].</summary>
	private const int VictoryAlertNumber = 14;

	private const string OffsetName = "alerticon_offset";

	private CGuiContext _context = null!;
	private CCountry _player = null!;
	private CSimulation _simulation = null!;
	private CGuiWindow? _deployable;
	private CGuiWindow? _victory;

	/// <summary>The alerts up, in the order they came up, which is the order they stand in.</summary>
	private readonly List<CGuiWindow> _up = new();

	/// <summary>Raised when the alert for units waiting to be placed is clicked.</summary>
	public event Action? DeployablePressed;

	/// <summary>Raised when the alert for a faction near victory is clicked.</summary>
	public event Action? VictoryPressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CCountry player, CSimulation simulation)
	{
		_context = context;
		_player = player;
		_simulation = simulation;
	}

	public override void _Ready()
	{
		Name = "Alerts";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_deployable = Make(CDeployMenuText.AlertNumber, () => DeployablePressed?.Invoke());
		_victory = Make(VictoryAlertNumber, () => VictoryPressed?.Invoke());
		if (_victory?.FindIcon(IconName) is { } icon)
		{
			icon.Tooltip = () => CVictoryWindowText.AlertTooltip(_context.Localisation.Localise, _simulation.State, _player);
		}
	}

	private CGuiWindow? Make(int number, Action pressed)
	{
		CGuiWindow? window = CGuiWindow.Create(_context, WindowName);
		if (window is null)
		{
			return null;
		}
		AddChild(window);
		window.MouseFilter = MouseFilterEnum.Ignore;
		if (window.FindIcon(IconName) is { } icon)
		{
			icon.SetFrame(number + 1);
			icon.Clicked += pressed;
		}
		if (window.TryFindIcon(BannerName) is { } banner)
		{
			banner.MouseFilter = MouseFilterEnum.Ignore;
		}
		window.Visible = false;
		return window;
	}

	/// <summary>
	/// The deploy alert is up while the country has a unit to place, and the victory alert while a faction but the player's is three quarters of the way to winning.
	/// </summary>
	public override void _Process(double delta)
	{
		Show(_deployable, _player.Deployments.Count > 0 || _player.BuildingDeployments.Count > 0);
		Show(_victory, CVictoryWindowText.AlertFactions(_simulation.State, _player).Count > 0);
	}

	private void Show(CGuiWindow? window, bool up)
	{
		if (window is null || window.Visible == up)
		{
			return;
		}
		window.Visible = up;
		if (up)
		{
			_up.Add(window);
		}
		else
		{
			_up.Remove(window);
		}
		// The first up stands at the start position and each after it an offset on.
		CGuiPoint start = _context.Types.FindPosition(StartName)?.Position ?? default;
		CGuiPoint offset = _context.Types.FindPosition(OffsetName)?.Position ?? default;
		for (int place = 0; place < _up.Count; place++)
		{
			_up[place].MoveTo(new Vector2(start.X + place * offset.X, start.Y));
		}
	}
}
