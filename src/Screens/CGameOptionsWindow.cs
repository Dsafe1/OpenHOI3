using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Settings;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;

namespace OpenHOI3.Screens;

/// <summary>
/// The game options window: <c>settings.gui</c>'s <c>menu_settings_ingame</c>, which the in-game menu's Game Options
/// opens - four tabs that are plain buttons, Video first; the volumes and the gamma live as they move and everything else
/// held until Apply, which stores it all, writes the player's <c>settings.txt</c> and closes the window twice where no
/// restart is asked - the whole menu with it; Back puts the volumes back and closes it once. The rules are
/// <see cref="CGameOptions"/>'s; see <c>docs/mechanics/gameoptions.md</c>.
/// </summary>
public sealed partial class CGameOptionsWindow : Control
{
	public const string WindowName = "menu_settings_ingame";
	private const string InfoDialogName = "DefaultInfoDialog";

	private CGuiContext _context = null!;
	private Func<CUserSettings> _settings = null!;
	private Action<CUserSettings> _setSettings = null!;
	private (int Width, int Height, int Rate) _running;
	private CGameOptions _options = null!;
	private CGuiWindow? _window;
	private readonly Dictionary<string, CGuiWindow> _tabs = new();
	private CGuiWindow? _current;
	private CGuiWindow? _game;
	private CGuiWindow? _video;
	private CGuiWindow? _audio;
	private CGuiWindow? _controls;
	private (float Master, float Effect, float Music) _savedVolumes;
	private CConfirmDialog? _dialog;

	/// <summary>Back, and the end of Apply: the in-game menu's close, which goes back to its six buttons or shuts it.</summary>
	public event Action? CloseRequested;

	/// <summary>Settings stored by Apply, for what of them takes effect at once.</summary>
	public event Action<CUserSettings>? Applied;

	/// <summary>The gamma slider moved: the screen's brightness at once, before Apply (<see cref="CGameOptions.Brightness"/>).</summary>
	public event Action<float>? GammaPreviewed;

	public bool IsShown => _window is { Visible: true };

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="running">The mode the game runs at, which a changed choice asks a restart for.</param>
	public void Setup(CGuiContext context, Func<CUserSettings> settings, Action<CUserSettings> setSettings,
		IReadOnlyList<CDisplayMode> modes, IEnumerable<int> supportedSamples, (int Width, int Height, int Rate) running)
	{
		_context = context;
		_settings = settings;
		_setSettings = setSettings;
		_running = running;
		_options = new CGameOptions(modes, supportedSamples, settings());
	}

	public override void _Ready()
	{
		Name = "GameOptions";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		_game = _window.FindWindow("menu_settings_GAME");
		_video = _window.FindWindow("menu_settings_VIDEO");
		_audio = _window.FindWindow("menu_settings_AUDIO");
		_controls = _window.FindWindow("menu_settings_CONTROLS");
		Bind(_window.FindButton("BackButton"), Close);
		Bind(_window.FindButton("ApplyButton"), Apply);
		// The tabs are plain buttons: each shows its window in the current one's place, and nothing marks it chosen.
		string[] tabs = { "settings_tab_1", "settings_tab_2", "settings_tab_3", "settings_tab_4" };
		CGuiWindow?[] windows = { _game, _video, _audio, _controls };
		for (int i = 0; i < tabs.Length; i++)
		{
			CGuiWindow? shows = windows[i];
			Bind(_window.FindButton(tabs[i]), () => ShowTab(shows));
		}
		_current = _video;

		if (_game is not null)
		{
			Bind(_game.FindButton("decrease_autosave_button"), () => StepAutosave(-1));
			Bind(_game.FindButton("increase_autosave_button"), () => StepAutosave(1));
		}
		if (_video is not null)
		{
			Bind(_video.FindButton("decrease_resolution_button"), () => Step(() => _options.StepMode(-1)));
			Bind(_video.FindButton("increase_resolution_button"), () => Step(() => _options.StepMode(1)));
			Bind(_video.FindButton("decrease_refreshrate_button"), () => Step(() => _options.StepRate(-1)));
			Bind(_video.FindButton("increase_refreshrate_button"), () => Step(() => _options.StepRate(1)));
			Bind(_video.FindButton("decrease_multisample_button"), () => Step(() => _options.StepMultisample(-1)));
			Bind(_video.FindButton("increase_multisample_button"), () => Step(() => _options.StepMultisample(1)));
			Bind(_video.FindButton("trees_checkbox"), () => _options.Trees = !_options.Trees);
			Bind(_video.FindButton("water_checkbox"), () => _options.AdvancedWater = !_options.AdvancedWater);
			Bind(_video.FindButton("hierarchy_checkbox"), () => _options.Hierarchy = !_options.Hierarchy);
			Bind(_video.FindButton("unit_name_checkbox"), () => _options.UnitNames = !_options.UnitNames);
			if (_video.FindScrollbar("gamma_slider") is { } gamma)
			{
				gamma.Moved += value => GammaPreviewed?.Invoke(CGameOptions.Brightness(Float(value)));
			}
			if (_video.FindScrollbar("counter_slider") is { } distance)
			{
				distance.Moved += value => _options.CounterDistanceMoved(Float(value));
			}
			if (_video.FindScrollbar("counter_scale_slider") is { } scale)
			{
				scale.Moved += value => _options.CounterScaleMoved(Float(value));
			}
		}
		if (_audio is not null)
		{
			// The volumes reach the settings as they move, before Apply; Back puts them back.
			foreach (string name in new[] { "mastervolume_slider", "effectvolume_slider", "musicvolume_slider" })
			{
				if (_audio.FindScrollbar(name) is { } volume)
				{
					volume.Moved += _ => _setSettings(WithVolumes(_settings(), Volumes()));
				}
			}
		}
		_window.Visible = false;
	}

	/// <summary>The window up on the tab last chosen, every control but the autosave frequency's made again from the settings.</summary>
	public void Open()
	{
		if (_window is null)
		{
			return;
		}
		_window.Visible = true;
		HideTabs();
		if (_current is not null)
		{
			_current.Visible = true;
		}
		Refresh();
	}

	public void HideWindow()
	{
		if (_window is not null)
		{
			_window.Visible = false;
		}
		HideTabs();
	}

	private void HideTabs()
	{
		foreach (CGuiWindow? tab in new[] { _game, _video, _audio, _controls })
		{
			if (tab is not null)
			{
				tab.Visible = false;
			}
		}
	}

	private void ShowTab(CGuiWindow? tab)
	{
		if (_current is not null)
		{
			_current.Visible = false;
		}
		_current = tab;
		if (tab is not null)
		{
			tab.Visible = true;
		}
	}

	/// <summary>Back: the volumes as they were when the window opened, then the menu's close.</summary>
	public void Close()
	{
		_setSettings(WithVolumes(_settings(), _savedVolumes));
		CloseRequested?.Invoke();
	}

	private void Refresh()
	{
		CUserSettings settings = _settings();
		_options.Refresh(settings, _running.Width, _running.Height);
		if (_video is not null)
		{
			SetChecked(_video, "trees_checkbox", _options.Trees);
			SetChecked(_video, "water_checkbox", _options.AdvancedWater);
			SetChecked(_video, "hierarchy_checkbox", _options.Hierarchy);
			SetChecked(_video, "unit_name_checkbox", _options.UnitNames);
			// Read as it stands at Apply; set from the settings here.
			SetChecked(_video, "counters_checkbox", settings.AlwaysCounters);
			// The slider set and its report heard, as the original's observer is: the gamma shown, the distance's position kept.
			if (_video.FindScrollbar("gamma_slider") is { } gamma)
			{
				gamma.Value = Fixed(settings.Gamma);
				GammaPreviewed?.Invoke(CGameOptions.Brightness(Float(gamma.Value)));
			}
			if (_video.FindScrollbar("counter_slider") is { } distance)
			{
				distance.Value = Fixed(CGameOptions.CounterDistanceSlider(settings.CounterDistance));
				_options.CounterDistanceMoved(Float(distance.Value));
			}
			if (_video.FindScrollbar("counter_scale_slider") is { } scale)
			{
				scale.Value = Fixed(settings.CounterScale);
				_options.CounterScaleMoved(Float(scale.Value));
			}
			bool multisampling = _options.MultisamplingAvailable;
			foreach (string name in new[] { "decrease_multisample_button", "increase_multisample_button" })
			{
				if (!multisampling && _video.TryFindButton(name) is { } button)
				{
					button.Disabled = true;
				}
			}
		}
		if (_audio is not null)
		{
			bool off = settings.MasterVolume < -0.01f;
			SetSlider(_audio, "mastervolume_slider", settings.MasterVolume, off);
			SetSlider(_audio, "effectvolume_slider", settings.SoundFxVolume, off);
			SetSlider(_audio, "musicvolume_slider", settings.MusicVolume, off);
		}
		_savedVolumes = (settings.MasterVolume, settings.SoundFxVolume, settings.MusicVolume);
		if (_controls is not null)
		{
			SetSlider(_controls, "scrollspeed_slider", settings.ScrollSpeed, false);
			SetSlider(_controls, "zoomspeed_slider", settings.ZoomSpeed, false);
			// The legacy camera's box is never set from the settings: the original's starts as its file leaves it.
		}
		ShowValues();
	}

	private void StepAutosave(int by)
	{
		_options.StepAutosave(by);
		ShowValues();
	}

	private void Step(Action step)
	{
		step();
		ShowValues();
	}

	private void ShowValues()
	{
		if (_game?.FindTextBox("autosave_value") is { } autosave)
		{
			autosave.Text = _context.Localisation.Localise(_options.AutosaveKey);
		}
		if (_video is null)
		{
			return;
		}
		if (_video.FindTextBox("resolution_value") is { } resolution)
		{
			resolution.Text = _options.ModeText();
		}
		if (_video.FindTextBox("refreshrate_value") is { } rate)
		{
			rate.Text = _options.RateText();
		}
		if (_video.FindTextBox("multisample_value") is { } multisample)
		{
			multisample.Text = _options.MultisampleText();
		}
	}

	/// <summary>
	/// Apply: every choice stored and the file written - a failure says so and leaves the window up - then a restart
	/// asked, or not: with one the window's close once, back to the menu's buttons under the notice; without, twice, which
	/// shuts the menu (gameoptions.md, section 2).
	/// </summary>
	private void Apply()
	{
		CUserSettings settings = _settings();
		var sliders = new CGameOptionsSliders(
			Slider(_video, "gamma_slider", settings.Gamma),
			Slider(_audio, "mastervolume_slider", settings.MasterVolume),
			Slider(_audio, "effectvolume_slider", settings.SoundFxVolume),
			Slider(_audio, "musicvolume_slider", settings.MusicVolume),
			Slider(_controls, "scrollspeed_slider", settings.ScrollSpeed),
			Slider(_controls, "zoomspeed_slider", settings.ZoomSpeed),
			_video?.TryFindButton("counters_checkbox") is { Checked: true },
			_controls?.TryFindButton("legacy_camera_checkbox") is { Checked: true });
		(CUserSettings stored, bool restart) = _options.Apply(settings, sliders, _running.Width, _running.Height, _running.Rate);
		_setSettings(stored);
		_savedVolumes = (stored.MasterVolume, stored.SoundFxVolume, stored.MusicVolume);
		GammaPreviewed?.Invoke(CGameOptions.Brightness(stored.Gamma));
		Applied?.Invoke(stored);
		if (!CUserSettingsWriter.WriteFile(stored, stored.Path ?? CUserSettingsReader.DefaultPath()))
		{
			Inform("SM_ALERT", "SM_WP");
			return;
		}
		if (restart)
		{
			Inform("RESTART_REQ", "RESTART_REQ_DESC");
			Refresh();
			Close();
			return;
		}
		Close();
		Refresh();
		Close();
	}

	/// <summary><c>DefaultInfoDialog</c>, its one button taking Enter's place and Escape's, over the menu.</summary>
	private void Inform(string title, string text)
	{
		_dialog?.Close();
		Func<string, string> localise = _context.Localisation.Localise;
		if (GetParent() is { } parent)
		{
			_dialog = CConfirmDialog.Open(parent, _context, InfoDialogName, localise(title), localise(text));
		}
	}

	private (float Master, float Effect, float Music) Volumes()
	{
		CUserSettings settings = _settings();
		return (Slider(_audio, "mastervolume_slider", settings.MasterVolume),
			Slider(_audio, "effectvolume_slider", settings.SoundFxVolume),
			Slider(_audio, "musicvolume_slider", settings.MusicVolume));
	}

	private static CUserSettings WithVolumes(CUserSettings settings, (float Master, float Effect, float Music) volumes)
	{
		return settings with { MasterVolume = volumes.Master, SoundFxVolume = volumes.Effect, MusicVolume = volumes.Music };
	}

	private static float Slider(CGuiWindow? tab, string name, float fallback)
	{
		return tab?.FindScrollbar(name) is { } slider ? Float(slider.Value) : fallback;
	}

	private static void SetSlider(CGuiWindow tab, string name, float value, bool locked)
	{
		if (tab.FindScrollbar(name) is { } slider)
		{
			slider.Value = Fixed(value);
			slider.Locked = locked;
		}
	}

	private static void SetChecked(CGuiWindow tab, string name, bool value)
	{
		if (tab.TryFindButton(name) is { } box)
		{
			box.Checked = value;
		}
	}

	private static float Float(CFixedPoint value) => value.Raw / (float)CFixedPoint.Scale;

	/// <summary>A figure as a slider's thousandths, as the original sets one: the figure plus half a thousandth, cut.</summary>
	private static CFixedPoint Fixed(float value) => new((long)MathF.Floor((value + 0.0005f) * CFixedPoint.Scale));

	private static void Bind(CGuiButton? button, Action action)
	{
		if (button is not null)
		{
			button.Pressed += action;
		}
	}
}
