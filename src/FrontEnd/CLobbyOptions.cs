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

	/// <summary>An enhanced game's host's rule on other files (docs/multiplayer.md, section 15).</summary>
	public const string OtherFilesButton = "other_files";

	/// <summary>An enhanced game's rule: whether it is recorded beside its saves (docs/replay.md, section 10).</summary>
	public const string ReplayButton = "record_replay";

	/// <summary>
	/// The mod's window with the button that plays back the game a loaded save came of, shown only where there is one
	/// to play (docs/replay.md, section 11).
	/// </summary>
	public const string ReplayWindowName = "openhoi_lobby_replay";
	public const string WatchButton = "watch_replay";
	public const string ExportButton = "export_replay";
	public const string ImportButton = "import_replay";

	/// <summary>How many replay files wait in the import folder, looked for once a second while the lobby is up.</summary>
	private int _waiting;
	private ulong _lookedAt;

	private CGuiWindow? _replay;

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
		_replay = _context.Types.FindWindow(ReplayWindowName) is null ? null : CGuiWindow.Create(_context, ReplayWindowName);
		if (_replay is not null)
		{
			AddChild(_replay);
			if (_replay.TryFindButton(WatchButton) is { } watch)
			{
				watch.SetText(CReplayText.Text(Localise, CReplayText.WatchKey, CReplayText.Watch));
				watch.Pressed += () => _frontEnd.WatchReplay();
				string nothing = CReplayText.Text(Localise, CReplayText.NothingKey, CReplayText.Nothing);
				watch.Tooltip = () => new Interface.Tooltips.CTooltip
				{
					Text = _frontEnd.LoadedReplay is { } probe
						? CReplayText.Tooltip(Localise, probe.Recordings, probe.OtherMade, _frontEnd.MadeWith?.Invoke() ?? "")
						: nothing,
				};
			}
			if (_replay.TryFindButton(ExportButton) is { } export)
			{
				export.SetText(CReplayText.Text(Localise, CReplayText.ExportKey, CReplayText.Export));
				export.Pressed += ExportReplay;
				export.Tooltip = () => new Interface.Tooltips.CTooltip
				{
					Text = _frontEnd.LoadedReplay is null
						? CReplayText.Text(Localise, CReplayText.NothingKey, CReplayText.Nothing)
						: CReplayText.Text(Localise, CReplayText.ExportTipKey, CReplayText.ExportTip)
							.Replace("$FOLDER$", Content.Replays.CReplayArchive.ExportFolder(_frontEnd.SaveFolder)),
				};
			}
			if (_replay.TryFindButton(ImportButton) is { } import)
			{
				import.SetText(CReplayText.Text(Localise, CReplayText.ImportKey, CReplayText.Import));
				import.Pressed += ImportReplays;
				import.Tooltip = () => new Interface.Tooltips.CTooltip
				{
					Text = CReplayText.Text(Localise, CReplayText.ImportTipKey, CReplayText.ImportTip)
						.Replace("$FOLDER$", Content.Replays.CReplayArchive.ImportFolder(_frontEnd.SaveFolder))
						.Replace("$COUNT$", _waiting.ToString(System.Globalization.CultureInfo.InvariantCulture)),
				};
			}
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
			if (_rules.TryFindButton(OtherFilesButton) is { } otherFiles)
			{
				otherFiles.Pressed += () =>
				{
					play.AllowOtherFiles = !play.AllowOtherFiles;
					Refresh();
					Changed?.Invoke(CLobbyOptionsText.Changed(
						key => key == CLobbyRulesText.OtherFilesKey
							? CLobbyRulesText.Text(Localise, key, CLobbyRulesText.OtherFiles) : Localise(key),
						CLobbyRulesText.OtherFilesKey, Localise(play.AllowOtherFiles ? "YES" : "NO")));
				};
			}
			if (_rules.TryFindButton(ReplayButton) is { } replay)
			{
				replay.Pressed += () =>
				{
					play.RecordReplay = !play.RecordReplay;
					Refresh();
					Changed?.Invoke(CLobbyOptionsText.Changed(
						key => key == CLobbyRulesText.ReplayKey
							? CLobbyRulesText.Text(Localise, key, CLobbyRulesText.Replay) : Localise(key),
						CLobbyRulesText.ReplayKey, Localise(play.RecordReplay ? "YES" : "NO")));
				};
			}
			if (_rules.TryFindButton(ReplayButton + "_help") is { } replayHelp)
			{
				string replayTip = CLobbyRulesText.Text(Localise, CLobbyRulesText.ReplayTipKey, CLobbyRulesText.ReplayTip);
				replayHelp.Tooltip = () => new Interface.Tooltips.CTooltip { Text = replayTip };
			}
			if (_rules.TryFindElement(ReplayButton + "_label") is CGuiTextBox replayLabel)
			{
				replayLabel.Text = CLobbyRulesText.Text(Localise, CLobbyRulesText.ReplayKey, CLobbyRulesText.Replay);
			}
			if (_rules.TryFindButton(OtherFilesButton + "_help") is { } otherHelp)
			{
				string otherTip = CLobbyRulesText.Text(Localise, CLobbyRulesText.OtherFilesTipKey, CLobbyRulesText.OtherFilesTip);
				otherHelp.Tooltip = () => new Interface.Tooltips.CTooltip { Text = otherTip };
			}
			// The window's own words where no table has their keys.
			if (_rules.TryFindElement("title") is CGuiTextBox title)
			{
				title.Text = CLobbyRulesText.Text(Localise, CLobbyRulesText.TitleKey, CLobbyRulesText.Title);
			}
			if (_rules.TryFindElement(OtherFilesButton + "_label") is CGuiTextBox otherLabel)
			{
				otherLabel.Text = CLobbyRulesText.Text(Localise, CLobbyRulesText.OtherFilesKey, CLobbyRulesText.OtherFiles);
			}
			// What the rule keeps, said under the mouse on the `?` beside it and nowhere else: figures of the game, in
			// the save - a player may take "collect statistics" for his own being gathered.
			string tip = Localise(StatisticsTip);
			if (_rules.TryFindButton(StatisticsButton + "_help") is { } help)
			{
				help.Tooltip = () => new Interface.Tooltips.CTooltip { Text = tip };
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
			if (_rules?.TryFindButton(OtherFilesButton) is { } otherFiles)
			{
				otherFiles.Disabled = value;
			}
			if (_rules?.TryFindButton(ReplayButton) is { } replay)
			{
				replay.Disabled = value;
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
		// And the host's rule on other files, which the others are shown.
		values.Add(play.AllowOtherFiles ? 1 : 0);
		// And whether the game is recorded.
		values.Add(play.RecordReplay ? 1 : 0);
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
		if (values.Count > 3 + CLobbyOptionsText.Areas.Count)
		{
			play.AllowOtherFiles = values[3 + CLobbyOptionsText.Areas.Count] != 0;
		}
		if (values.Count > 4 + CLobbyOptionsText.Areas.Count)
		{
			play.RecordReplay = values[4 + CLobbyOptionsText.Areas.Count] != 0;
		}
	}

	/// <summary>Whether the lobby is one of a game between machines: a host's, or a machine's that joined one.</summary>
	private bool BetweenMachines => _readOnly || _frontEnd.HostAs is not null || _frontEnd.LobbyNet is not null;

	/// <summary>Which kind of lobby the rules' rows were last laid out for; null before the first.</summary>
	private bool? _laidOutFor;
	private readonly System.Collections.Generic.Dictionary<Control, Vector2> _home = new();

	/// <summary>
	/// The rules a lobby does not have are taken out of the mod's window: the host's rule on other files in a game of one
	/// (and Watch replay between machines, by <see cref="Refresh"/>). The rows under a row taken out move up by its
	/// height, and the window's background is as much shorter. Looked at each frame, the lobby becoming a host's or a
	/// client's without this being told.
	/// </summary>
	public override void _Process(double delta)
	{
		// The import folder is looked into once a second: a file put there while the lobby is up lights the button.
		if (Visible && _replay is not null && Time.GetTicksMsec() - _lookedAt >= 1000)
		{
			_lookedAt = Time.GetTicksMsec();
			int waiting = Content.Replays.CReplayArchive.Waiting(_frontEnd.SaveFolder).Count;
			if (waiting != _waiting)
			{
				_waiting = waiting;
				Refresh();
			}
		}
		if (_rules is null || !Visible || _laidOutFor == BetweenMachines)
		{
			return;
		}
		bool between = BetweenMachines;
		_laidOutFor = between;
		Control?[] other = { _rules.TryFindButton(OtherFilesButton), _rules.TryFindElement(OtherFilesButton + "_label"),
			_rules.TryFindButton(OtherFilesButton + "_help") };
		Control?[] below = { _rules.TryFindButton(ReplayButton), _rules.TryFindElement(ReplayButton + "_label"),
			_rules.TryFindButton(ReplayButton + "_help") };
		Control? background = _rules.TryFindElement("background");
		foreach (Control? element in other)
		{
			if (element is not null)
			{
				element.Visible = between;
			}
		}
		// How far the rows below move: from their own row to the one taken out.
		float step = other[0] is { } first && below[0] is { } next ? Home(next).Y - Home(first).Y : 0f;
		foreach (Control? element in below)
		{
			if (element is not null)
			{
				element.Position = Home(element) - new Vector2(0, between ? 0f : step);
			}
		}
		if (background is not null)
		{
			if (!_home.ContainsKey(background))
			{
				_home[background] = background.Size;
			}
			background.Size = _home[background] - new Vector2(0, between ? 0f : step);
		}
		// The window under the rules follows their foot up.
		if (_replay is not null)
		{
			_replay.Position = Home(_replay) - new Vector2(0, between ? 0f : step);
		}
		Refresh();
	}

	/// <summary>The loaded save and its replay written as one file, and where said in the lobby's chat.</summary>
	private void ExportReplay()
	{
		if (_frontEnd.LoadedSavePath is not { } save || _frontEnd.LoadedReplay is null)
		{
			return;
		}
		try
		{
			if (Content.Replays.CReplayArchive.Export(save, _frontEnd.SaveFolder) is { } written)
			{
				Changed?.Invoke(CReplayText.Text(Localise, CReplayText.ExportedKey, CReplayText.Exported).Replace("$FILE$", written));
			}
		}
		catch (Exception failed) when (failed is System.IO.IOException or UnauthorizedAccessException)
		{
			Changed?.Invoke(failed.Message);
		}
	}

	/// <summary>Every replay file waiting taken in, each said in the lobby's chat, and the list of saves shown again.</summary>
	private void ImportReplays()
	{
		foreach (string archive in Content.Replays.CReplayArchive.Waiting(_frontEnd.SaveFolder))
		{
			string? saved = Content.Replays.CReplayArchive.Import(archive, _frontEnd.SaveFolder);
			Changed?.Invoke(saved is null
				? CReplayText.Text(Localise, CReplayText.ImportFailedKey, CReplayText.ImportFailed)
					.Replace("$FILE$", System.IO.Path.GetFileName(archive))
				: CReplayText.Text(Localise, CReplayText.ImportedKey, CReplayText.Imported).Replace("$FILE$", saved));
		}
		_lookedAt = 0;
		Imported?.Invoke();
	}

	/// <summary>Replays were taken in: the list of saves has new ones.</summary>
	public event Action? Imported;

	/// <summary>Where an element stood as the window was made.</summary>
	private Vector2 Home(Control element)
	{
		if (!_home.TryGetValue(element, out Vector2 home))
		{
			_home[element] = home = element.Position;
		}
		return home;
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
		if (_rules?.TryFindButton(OtherFilesButton) is { } otherFiles)
		{
			otherFiles.SetText(_frontEnd.ChosenPlay.AllowOtherFiles ? "X" : "");
			otherFiles.Disabled = _readOnly;
		}
		if (_rules?.TryFindButton(ReplayButton) is { } replay)
		{
			replay.SetText(_frontEnd.ChosenPlay.RecordReplay ? "X" : "");
			replay.Disabled = _readOnly;
		}
		if (_replay is not null)
		{
			// Always there in a game of one and never between machines: Watch and Export greyed without a loaded save
			// whose game can be played back, Import without a replay file waiting (docs/replay.md, section 17).
			Content.Replays.CReplayProbe? probe = _frontEnd.LoadedReplay;
			_replay.Visible = !BetweenMachines;
			if (_replay.TryFindButton(WatchButton) is { } watch)
			{
				watch.Disabled = probe is null;
			}
			if (_replay.TryFindButton(ExportButton) is { } export)
			{
				export.Disabled = probe is null;
			}
			if (_replay.TryFindButton(ImportButton) is { } import)
			{
				import.Disabled = _waiting == 0;
			}
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
