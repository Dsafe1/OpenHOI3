using System;
using Godot;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Settings;

namespace OpenHOI3.FrontEnd;

/// <summary>
/// The lobby's Options panel, <c>options.gui</c>'s <c>option_window</c>, a window of its own beside the lobby: the
/// difficulty, the game mode - its arrows greyed, Arcade not being built - and the five areas the player may hand to the
/// AI, each arrow wrapping and each press saying what changed in the lobby's chat. What it chooses is the front end's
/// until Play. See <c>docs/mechanics/frontend.md</c>, section 4; <c>reversing/findings/FINDINGS-frontend.md</c>, C.
/// </summary>
public partial class CLobbyOptions : Control
{
	public const string WindowName = "option_window";

	private CFrontEnd _frontEnd = null!;
	private CGuiContext _context = null!;
	private CGuiWindow? _window;

	/// <summary>The window of an enhanced game's own rules, where the mod's files declare one, and what is in it.</summary>
	public const string RulesWindowName = "openhoi_lobby_rules";
	public const string StatisticsButton = "statistics";
	public const string StatisticsLabel = "OPENHOI_RULE_STATISTICS";
	public const string StatisticsTip = "OPENHOI_RULE_STATISTICS_TIP";

	private CGuiWindow? _rules;

	/// <summary>A choice changed: the line the lobby's chat is to say.</summary>
	public event Action<string>? Changed;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CFrontEnd frontEnd)
	{
		_frontEnd = frontEnd;
		_context = frontEnd.Gui;
	}

	private Func<string, string> Localise => _context.Localisation.Localise;

	public override void _Ready()
	{
		Name = "LobbyOptions";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Visible = false;
		_window = CGuiWindow.Create(_context, WindowName);
		if (_window is null)
		{
			return;
		}
		AddChild(_window);
		CGamePlaySettings play = _frontEnd.ChosenPlay;
		CPlayerAutomation automation = _frontEnd.ChosenAutomation;
		Bind("difficulty_prev", () => StepDifficulty(-1));
		Bind("difficulty_next", () => StepDifficulty(1));
		// Arcade's supply from the capital is not built, so the game mode stays Normal (frontend.md, section 4).
		foreach (string arrow in new[] { "arcade_prev", "arcade_next" })
		{
			if (_window.TryFindButton(arrow) is { } button)
			{
				button.Disabled = true;
				button.Modulate = Faint;
			}
		}
		foreach ((string element, string label, EAutomation area) in CLobbyOptionsText.Areas)
		{
			Bind(element + "_prev", () => StepArea(label, area, -1));
			Bind(element + "_next", () => StepArea(label, area, 1));
		}
		// An enhanced game's own rules, in a window of the mod's beside the original's (statisticsrecord.md, section 6).
		_rules = _context.Types.FindWindow(RulesWindowName) is null ? null : CGuiWindow.Create(_context, RulesWindowName);
		if (_rules is not null)
		{
			AddChild(_rules);
			if (_rules.TryFindButton(StatisticsButton) is { } statistics)
			{
				statistics.Pressed += () =>
				{
					play.CollectStatistics = !play.CollectStatistics;
					Refresh();
					Changed?.Invoke(CLobbyOptionsText.Changed(Localise, StatisticsLabel,
						Localise(play.CollectStatistics ? "YES" : "NO")));
				};
			}
			// What the rule keeps, said under the mouse on the tick, its words and the `?` beside them: figures of
			// the game, in the save - a player may take "collect statistics" for his own being gathered.
			string tip = Localise(StatisticsTip);
			foreach (string name in new[] { StatisticsButton, StatisticsButton + "_label", StatisticsButton + "_help" })
			{
				if (_rules.TryFindElement(name) is { } element)
				{
					element.Tooltip = () => new Interface.Tooltips.CTooltip { Text = tip };
				}
			}
		}
		Refresh();

		void StepDifficulty(int step)
		{
			play.Difficulty = CLobbyOptionsText.Step(play.Difficulty, step, CGamePlaySettings.DifficultyCount);
			Refresh();
			Changed?.Invoke(CLobbyOptionsText.Changed(Localise, "FE_DIFFICULTY",
				CLobbyOptionsText.Text(Localise, CLobbyOptionsText.Difficulties, play.Difficulty)));
		}

		void StepArea(string label, EAutomation area, int step)
		{
			string[] values = CLobbyOptionsText.Values(area);
			int value = CLobbyOptionsText.Step(CLobbyOptionsText.Value(automation, area), step, values.Length);
			CLobbyOptionsText.Set(automation, area, value);
			Refresh();
			Changed?.Invoke(CLobbyOptionsText.Changed(Localise, label, CLobbyOptionsText.Text(Localise, values, value)));
		}
	}

	/// <summary>
	/// Whether the panel shows another machine's choices - the host's, on a client: every arrow is then greyed. The
	/// original greys the difficulty's and the game mode's four there and leaves the five areas' to the player; here
	/// the areas are the game's one record, the host's, so theirs are greyed too (docs/compatibility.md, entry 31).
	/// </summary>
	public bool ReadOnly
	{
		get => _readOnly;
		set
		{
			_readOnly = value;
			if (_rules?.TryFindButton(StatisticsButton) is { } statistics)
			{
				statistics.Disabled = value;
			}
			if (_window is null)
			{
				return;
			}
			var arrows = new System.Collections.Generic.List<string> { "difficulty_prev", "difficulty_next" };
			foreach ((string element, string _, EAutomation _) in CLobbyOptionsText.Areas)
			{
				arrows.Add(element + "_prev");
				arrows.Add(element + "_next");
			}
			foreach (string arrow in arrows)
			{
				if (_window.TryFindButton(arrow) is { } button)
				{
					button.Disabled = value;
					// The arrows' picture has no greyed frame, so one that does nothing is drawn faint. Ours.
					button.Modulate = value ? Faint : Colors.White;
				}
			}
		}
	}

	private static readonly Color Faint = new(1f, 1f, 1f, 0.35f);

	private bool _readOnly;

	/// <summary>The panel's seven choices as numbers - the difficulty, the game mode and the five areas - as they travel to the other machines.</summary>
	public static System.Collections.Generic.List<int> Values(CGamePlaySettings play, CPlayerAutomation automation)
	{
		var values = new System.Collections.Generic.List<int> { play.Difficulty, play.GameMode };
		foreach ((string _, string _, EAutomation area) in CLobbyOptionsText.Areas)
		{
			values.Add(CLobbyOptionsText.Value(automation, area));
		}
		// An enhanced game's rule, after the original's seven: whether statistics are kept.
		values.Add(play.CollectStatistics ? 1 : 0);
		return values;
	}

	/// <summary>Such numbers written into the choices; ones out of range, or too few, change nothing.</summary>
	public static void Apply(System.Collections.Generic.IReadOnlyList<int> values, CGamePlaySettings play, CPlayerAutomation automation)
	{
		if (values.Count < 2 + CLobbyOptionsText.Areas.Count || values[0] < 0 || values[0] >= CGamePlaySettings.DifficultyCount)
		{
			return;
		}
		play.Difficulty = values[0];
		play.GameMode = values[1];
		for (int i = 0; i < CLobbyOptionsText.Areas.Count; i++)
		{
			EAutomation area = CLobbyOptionsText.Areas[i].Area;
			if (values[2 + i] >= 0 && values[2 + i] < CLobbyOptionsText.Values(area).Length)
			{
				CLobbyOptionsText.Set(automation, area, values[2 + i]);
			}
		}
		if (values.Count > 2 + CLobbyOptionsText.Areas.Count)
		{
			play.CollectStatistics = values[2 + CLobbyOptionsText.Areas.Count] != 0;
		}
	}

	/// <summary>Every box written again from the front end's choices.</summary>
	public void Refresh()
	{
		if (_window is null)
		{
			return;
		}
		if (_rules?.TryFindButton(StatisticsButton) is { } statistics)
		{
			// A tick with no picture of its own: a field that shows an X while it is on.
			statistics.SetText(_frontEnd.ChosenPlay.CollectStatistics ? "X" : "");
			statistics.Disabled = _readOnly;
		}
		Set("difficulty_box", CLobbyOptionsText.Text(Localise, CLobbyOptionsText.Difficulties, _frontEnd.ChosenPlay.Difficulty));
		Set("arcade_box", CLobbyOptionsText.Text(Localise, CLobbyOptionsText.GameModes, _frontEnd.ChosenPlay.GameMode));
		foreach ((string element, string _, EAutomation area) in CLobbyOptionsText.Areas)
		{
			Set(element + "_box", CLobbyOptionsText.Text(Localise, CLobbyOptionsText.Values(area),
				CLobbyOptionsText.Value(_frontEnd.ChosenAutomation, area)));
		}
	}

	private void Set(string name, string text)
	{
		if (_window?.FindTextBox(name) is { } box)
		{
			box.Text = text;
		}
	}

	private void Bind(string name, Action pressed)
	{
		if (_window?.TryFindButton(name) is { } button)
		{
			button.Pressed += pressed;
		}
	}
}
