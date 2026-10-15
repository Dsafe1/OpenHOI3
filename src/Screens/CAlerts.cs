using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
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

	private CGuiContext _context = null!;
	private CCountry _player = null!;
	private CGuiWindow? _deployable;

	/// <summary>Raised when the alert for units waiting to be placed is clicked.</summary>
	public event Action? DeployablePressed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext context, CCountry player)
	{
		_context = context;
		_player = player;
	}

	public override void _Ready()
	{
		Name = "Alerts";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_deployable = CGuiWindow.Create(_context, WindowName);
		if (_deployable is null)
		{
			return;
		}
		AddChild(_deployable);
		_deployable.MouseFilter = MouseFilterEnum.Ignore;

		// The first alert up stands at the start position, and with one alert built it
		// is always the first.
		CGuiPoint start = _context.Types.FindPosition(StartName)?.Position ?? default;
		_deployable.MoveTo(new Vector2(start.X, start.Y));

		if (_deployable.FindIcon(IconName) is { } icon)
		{
			icon.SetFrame(CDeployMenuText.AlertNumber + 1);
			icon.Clicked += () => DeployablePressed?.Invoke();
		}
		if (_deployable.TryFindIcon(BannerName) is { } banner)
		{
			banner.MouseFilter = MouseFilterEnum.Ignore;
		}
		_deployable.Visible = false;
	}

	/// <summary>
	/// The alert is up while the country has a unit to place. When the original puts
	/// it up was not read; its own tooltip says "We have units available to be deployed."
	/// </summary>
	public override void _Process(double delta)
	{
		if (_deployable is not null)
		{
			_deployable.Visible = _player.Deployments.Count > 0 || _player.BuildingDeployments.Count > 0;
		}
	}
}
