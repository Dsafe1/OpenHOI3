using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Content.Settings;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the game options window holds between its opening and Apply, and the original's rules for it: the autosave
/// frequency, the display mode and the refresh rate stepped round with their text, the Video tab's
/// boxes and sliders, and what Apply stores into the settings and whether it asks for a restart - the original's two slips
/// with them, the refresh rate looked for among the first mode's rates. <b>The row that was the original's
/// multisampling is the window's mode here</b> - full screen, windowed, borderless - by the decision of 2026-11-26
/// (<c>docs/compatibility.md</c>, entry 33); the samples stay what the settings file says.
/// See <c>docs/mechanics/gameoptions.md</c>.
/// </summary>
public sealed class CGameOptions
{
	/// <summary>The window's modes in the order the row's buttons step through them.</summary>
	public static readonly IReadOnlyList<EWindowMode> WindowModes = new[]
	{
		EWindowMode.Fullscreen, EWindowMode.Windowed, EWindowMode.Borderless,
	};

	/// <summary>The row's heading: a mod's text where it has the key, else ours.</summary>
	public const string WindowModeLabelKey = "OPENHOI_WINDOW_MODE";

	private readonly IReadOnlyList<CDisplayMode> _modes;

	/// <param name="modes">The screen's modes, at least one; their order is the buttons' order.</param>
	public CGameOptions(IReadOnlyList<CDisplayMode> modes, CUserSettings settings)
	{
		if (modes.Count == 0)
		{
			throw new ArgumentException("a screen has at least one mode", nameof(modes));
		}
		_modes = modes;
		// The autosave choice is read from the settings once, as the window is made, and never again.
		Autosave = Math.Max(Array.IndexOf(CUserSettingsReader.AutosaveNames, settings.Autosave), 0);
	}

	public IReadOnlyList<CDisplayMode> Modes => _modes;

	/// <summary>The autosave frequency chosen, nought to five, which outlives Back.</summary>
	public int Autosave { get; private set; }

	public int Mode { get; private set; }

	public int Rate { get; private set; }

	/// <summary>
	/// The window's mode chosen; set by the window at each opening - the one stored, else the one the window is in -
	/// and put in force and stored at Apply.
	/// </summary>
	public EWindowMode WindowMode { get; set; } = EWindowMode.Windowed;

	/// <summary>The last mode and rate Apply asked a restart for, shown in brackets; null before one.</summary>
	public (int Mode, int Rate)? Pending { get; private set; }

	// The Video tab's choices, held until Apply; the boxes' own state where the box keeps it.
	public bool Trees { get; set; }

	public bool AdvancedWater { get; set; }

	public bool Hierarchy { get; set; }

	public bool UnitNames { get; set; }

	/// <summary>The counter distance's slider, nought to a hundred - or, where the window has no such slider, the distance itself.</summary>
	public float CounterDistance { get; set; }

	public float CounterScale { get; set; }

	/// <summary>
	/// The window opening: every choice but the autosave frequency's made again from the settings - the mode the screen
	/// runs at, else the first, and the rate the settings name, looked for among the first mode's rates.
	/// </summary>
	public void Refresh(CUserSettings settings, int runningWidth, int runningHeight)
	{
		Mode = RunningMode(runningWidth, runningHeight);
		Rate = RateIndex(Mode, settings.RefreshRate);
		Trees = settings.RenderTrees;
		AdvancedWater = !settings.SimpleWater;
		Hierarchy = settings.RenderHierarchy;
		UnitNames = settings.ShowUnitNames;
		// The distance itself, which the slider's own report then turns into its position.
		CounterDistance = settings.CounterDistance;
		CounterScale = settings.CounterScale;
	}

	/// <summary>Where the counter distance's slider stands for a distance: a fifth of it.</summary>
	public static float CounterDistanceSlider(float distance) => distance / 500f * 100f;

	/// <summary>A moved counter distance slider, held to nought to a hundred.</summary>
	public void CounterDistanceMoved(float value) => CounterDistance = Math.Clamp(value, 0f, 100f);

	public void CounterScaleMoved(float value) => CounterScale = Math.Clamp(value, 0f, 2f);

	public void StepAutosave(int by)
	{
		int next = Autosave + by;
		Autosave = next >= CUserSettingsReader.AutosaveNames.Length ? 0 : next < 0 ? CUserSettingsReader.AutosaveNames.Length - 1 : next;
	}

	/// <summary>The autosave frequency's key, which is its name: <c>MONTHLY</c>.</summary>
	public string AutosaveKey => CUserSettingsReader.AutosaveNames[Autosave];

	/// <summary>A resolution button: the next mode round, and the rate back to its first.</summary>
	public void StepMode(int by)
	{
		Mode = Wrap(Mode + by, _modes.Count);
		Rate = 0;
	}

	public void StepRate(int by)
	{
		Rate = Wrap(Rate + by, Math.Max(_modes[Mode].Rates.Count, 1));
	}

	/// <summary>A button of the window mode's row: the next mode round, either way.</summary>
	public void StepWindowMode(int by)
	{
		int at = 0;
		for (int i = 0; i < WindowModes.Count; i++)
		{
			if (WindowModes[i] == WindowMode)
			{
				at = i;
			}
		}
		WindowMode = WindowModes[Wrap(at + by, WindowModes.Count)];
	}

	/// <summary>A window mode's text key, which a mod may give words: <c>OPENHOI_WINDOW_MODE_BORDERLESS</c>.</summary>
	public static string WindowModeKey(EWindowMode mode) => mode switch
	{
		EWindowMode.Fullscreen => "OPENHOI_WINDOW_MODE_FULLSCREEN",
		EWindowMode.Borderless => "OPENHOI_WINDOW_MODE_BORDERLESS",
		_ => "OPENHOI_WINDOW_MODE_WINDOWED",
	};

	/// <summary>The row's heading: the key's text where a table has it - the original's have not - else "Window Mode".</summary>
	public static string WindowModeLabel(Func<string, string> localise) => Worded(localise, WindowModeLabelKey, "Window Mode");

	/// <summary>The mode chosen as the row writes it.</summary>
	public string WindowModeText(Func<string, string> localise) => Worded(localise, WindowModeKey(WindowMode), WindowMode switch
	{
		EWindowMode.Fullscreen => "Fullscreen",
		EWindowMode.Borderless => "Borderless Window",
		_ => "Windowed",
	});

	private static string Worded(Func<string, string> localise, string key, string ours)
	{
		string text = localise(key);
		return text == key || text.Length == 0 ? ours : text;
	}

	/// <summary>The resolution as the window writes it, with the one a restart waits for in brackets.</summary>
	public string ModeText()
	{
		string text = Size(_modes[Mode]);
		return Pending is { } pending ? text + " (" + Size(_modes[pending.Mode]) + ")" : text;
	}

	public string RateText()
	{
		string text = Hz(RateOf(Mode, Rate));
		return Pending is { } pending ? text + " (" + Hz(RateOf(pending.Mode, pending.Rate)) + ")" : text;
	}

	/// <summary>
	/// Apply's store: every choice into the settings - the slider values given as they stand - and whether a restart is
	/// asked, which only a changed mode or rate asks. The samples are left as the settings have them, no row choosing
	/// them; the window's mode is not the settings' and is the window's to store.
	/// </summary>
	public (CUserSettings Settings, bool Restart) Apply(CUserSettings settings, CGameOptionsSliders sliders,
		int runningWidth, int runningHeight, int runningRate)
	{
		bool restart = false;
		int runningMode = RunningMode(runningWidth, runningHeight);
		int running = RateIndex(Mode, runningRate);
		if (Mode != runningMode || Rate != running)
		{
			Pending = (Mode, Rate);
			restart = true;
		}
		else
		{
			Pending = null;
		}
		CDisplayMode mode = _modes[Mode];
		CUserSettings stored = settings with
		{
			RenderTrees = Trees,
			SimpleWater = !AdvancedWater,
			RenderHierarchy = Hierarchy,
			ShowUnitNames = UnitNames,
			AlwaysCounters = sliders.CountersTicked,
			CounterDistance = CounterDistance / 100f * 500f,
			CounterScale = CounterScale,
			Autosave = AutosaveKey,
			Gamma = sliders.Gamma,
			MasterVolume = sliders.MasterVolume,
			SoundFxVolume = sliders.EffectVolume,
			MusicVolume = sliders.MusicVolume,
			// The original's least speeds: scroll 5 and zoom 1.
			ScrollSpeed = sliders.ScrollSpeed <= 5f ? 5f : sliders.ScrollSpeed,
			ZoomSpeed = sliders.ZoomSpeed <= 1f ? 1f : sliders.ZoomSpeed,
			UseLegacyCamera = sliders.LegacyCameraTicked,
			Width = mode.Width,
			Height = mode.Height,
			RefreshRate = RateOf(Mode, Rate),
		};
		return (stored, restart);
	}

	/// <summary>The gamma setting as the brightness scale the original's ramp is: a fiftieth, held to 0.1 and 1.9.</summary>
	public static float Brightness(float gamma) => Math.Clamp(Math.Clamp(gamma, 0f, 100f) / 50f, 0.1f, 1.9f);

	private int RunningMode(int width, int height)
	{
		for (int i = 0; i < _modes.Count; i++)
		{
			if (_modes[i].Width == width && _modes[i].Height == height)
			{
				return i;
			}
		}
		return 0;
	}

	/// <summary>
	/// The index of a rate - looked for, as the original looks, among the first mode's rates and as far as the chosen
	/// mode has rates; nought where it is not found there.
	/// </summary>
	private int RateIndex(int mode, int rate)
	{
		IReadOnlyList<int> first = _modes[0].Rates;
		int count = Math.Min(_modes[mode].Rates.Count, first.Count);
		for (int i = 0; i < count; i++)
		{
			if (first[i] == rate)
			{
				return i;
			}
		}
		return 0;
	}

	private int RateOf(int mode, int rate)
	{
		IReadOnlyList<int> rates = _modes[mode].Rates;
		return rates.Count == 0 ? 0 : rates[Math.Clamp(rate, 0, rates.Count - 1)];
	}

	private static int Wrap(int value, int count) => value >= count ? 0 : value < 0 ? count - 1 : value;

	private static string Size(CDisplayMode mode) =>
		mode.Width.ToString(CultureInfo.InvariantCulture) + "x" + mode.Height.ToString(CultureInfo.InvariantCulture);

	private static string Hz(int rate) => rate.ToString(CultureInfo.InvariantCulture) + " Hz";
}
