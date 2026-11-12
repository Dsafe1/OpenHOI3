using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Alerts;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Screens;

/// <summary>
/// The row of alert icons under the top bar: every three seconds of real time the alerts are tested, an icon put up at
/// the row's end for each that has come up and one taken away for each that has gone, the row closing up. See
/// <c>docs/mechanics/alerts.md</c>.
/// </summary>
public partial class CAlerts : Control
{
	private const string WindowName = "alerticon_window";
	private const string IconName = "alerticon";
	private const string BannerName = "alerticon_banner";
	private const string StartName = "alerticon_startposition";
	private const string OffsetName = "alerticon_offset";

	/// <summary>How often the alerts are tested, in seconds of real time, paused or not [exe].</summary>
	private const double PassSeconds = 3.0;

	private CGuiContext _context = null!;
	private CCountry _player = null!;
	private CSimulation _simulation = null!;

	/// <summary>Seconds until the next pass; the first is the first frame.</summary>
	private double _untilPass;

	/// <summary>The alerts up, in the order they came up, which is the order they stand in.</summary>
	private readonly List<(EAlert Alert, CGuiWindow Window)> _up = new();

	private readonly Dictionary<EAlert, CTooltip> _tooltips = new();

	/// <summary>Raised when an alert's icon is clicked.</summary>
	public event Action<EAlert>? Pressed;

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
	}

	private CGuiWindow? Make(EAlert alert)
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
			icon.SetFrame((int)alert + 1);
			icon.Clicked += () => Pressed?.Invoke(alert);
			icon.Tooltip = () => _tooltips.GetValueOrDefault(alert) ?? new CTooltip();
		}
		if (window.TryFindIcon(BannerName) is { } banner)
		{
			banner.MouseFilter = MouseFilterEnum.Ignore;
		}
		return window;
	}

	public override void _Process(double delta)
	{
		_untilPass -= delta;
		if (_untilPass > 0)
		{
			return;
		}
		_untilPass = PassSeconds;
		List<CAlertShown> shown = CAlertPass.Run(_context.Localisation.Localise, _simulation.State, _player);
		_tooltips.Clear();
		foreach (CAlertShown alert in shown)
		{
			_tooltips[alert.Alert] = alert.Tooltip;
		}
		bool changed = false;
		for (int place = _up.Count - 1; place >= 0; place--)
		{
			if (!_tooltips.ContainsKey(_up[place].Alert))
			{
				_up[place].Window.QueueFree();
				_up.RemoveAt(place);
				changed = true;
			}
		}
		foreach (CAlertShown alert in shown)
		{
			if (_up.All(up => up.Alert != alert.Alert) && Make(alert.Alert) is { } window)
			{
				_up.Add((alert.Alert, window));
				changed = true;
				// Each alert that comes up sounds the message (docs/mechanics/sound.md, section 7).
				_context.Sounds?.PlayUnlessPlaying(CEventWindows.MessageSound);
			}
		}
		if (changed)
		{
			Lay();
		}
	}

	/// <summary>The first up stands at the start position and each after it an offset on.</summary>
	private void Lay()
	{
		CGuiPoint start = _context.Types.FindPosition(StartName)?.Position ?? default;
		CGuiPoint offset = _context.Types.FindPosition(OffsetName)?.Position ?? default;
		for (int place = 0; place < _up.Count; place++)
		{
			_up[place].Window.MoveTo(new Vector2(start.X + place * offset.X, start.Y));
		}
	}
}
