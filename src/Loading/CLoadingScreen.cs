using System;
using System.Threading.Tasks;
using Godot;
using OpenHOI3.Content;


namespace OpenHOI3.Loading;

/// <summary>
/// The loading screen: one of the install's <c>gfx/loadingscreens</c> pictures with a line of progress text over
/// <c>gfx/load_textbg.dds</c>, over everything while a game, or the front end, is being made
/// (<c>docs/mechanics/loadingscreen.md</c>).
///
/// The work is handed to it in steps, each with the localisation key of its line. A step's line is drawn before its work
/// starts; work that touches no engine object runs on a thread of its own (<see cref="Work"/>) so that the window keeps
/// answering, and work that makes nodes runs on the main thread after a frame has shown the line (<see cref="OnMain"/>).
/// Every input is swallowed while it is up, so nothing under it is clicked or typed into.
/// </summary>
public partial class CLoadingScreen : CanvasLayer
{
	/// <summary>Over every other layer, the brightness layer's among them.</summary>
	private const int ScreenLayer = 120;

	private CDirectorySettings _directories = null!;
	private CLoadingPicture _picture = null!;
	private CLocalisation? _localisation;
	private bool _firstOnly;

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="firstOnly">Whether the first main menu has come up, after which every loading screen shows the first picture.</param>
	public void Setup(CDirectorySettings directories, bool firstOnly)
	{
		_directories = directories;
		_firstOnly = firstOnly;
	}

	/// <summary>
	/// The localisation the lines are written from. Until it is given, a line is not shown: the loading screen comes up
	/// before anything is read.
	/// </summary>
	public CLocalisation? Localisation
	{
		get => _localisation;
		set
		{
			_localisation = value;
			_picture?.QueueRedraw();
		}
	}

	public override void _Ready()
	{
		Layer = ScreenLayer;
		ProcessMode = ProcessModeEnum.Always;
		_picture = new CLoadingPicture { Name = "Picture" };
		_picture.Setup(_directories, _firstOnly);
		AddChild(_picture);
	}

	/// <summary>Every key, button and movement taken here while the screen is up.</summary>
	public override void _Input(InputEvent @event)
	{
		GetViewport().SetInputAsHandled();
	}

	/// <summary>
	/// The line of a key shown, as it stands in the localisation, replacing the last; nothing where none is loaded yet. The
	/// pictures stepped so many times first: once for a line the original shows with the next picture, and once more for each
	/// such line of the original's that has no step here, so that a line shows the picture the original shows with it.
	/// </summary>
	public void Say(string key, int steps = 0)
	{
		for (int step = 0; step < steps; step++)
		{
			_picture.Step();
		}
		string? text = _localisation?.Find(key);
		if (text is null && key == CLoadingSteps.MapModes)
		{
			text = CLoadingSteps.MapModesText;
		}
		_picture.Line = _localisation is null ? "" : text ?? _localisation.Localise(key);
		if (_picture.Line.Length > 0 && ShotPath() is { } path)
		{
			_shotTaken = true;
			TakeShot(path);
		}
	}

	/// <summary>
	/// <c>--loadingshot=path.png</c>: the first loading screen with a line on it saved as a picture, once that line is drawn -
	/// a look at the screen, which no other switch can take since the game's own come after it.
	/// </summary>
	private static string? ShotPath()
	{
		if (_shotTaken)
		{
			return null;
		}
		foreach (string argument in CSession.UserArguments())
		{
			if (argument.StartsWith(ShotSwitch, StringComparison.Ordinal) && argument.Length > ShotSwitch.Length)
			{
				return argument[ShotSwitch.Length..];
			}
		}
		return null;
	}

	private const string ShotSwitch = "--loadingshot=";
	private static bool _shotTaken;

	private async void TakeShot(string path)
	{
		await Drawn(2);
		Error saved = GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print($"--loadingshot: {path} {(saved == Error.Ok ? "saved" : "not saved: " + saved)}.");
	}

	/// <summary>A step whose work touches no engine object: its line shown, then the work run on a thread of its own.</summary>
	public async Task Work(string key, Action work, int steps = 0)
	{
		Say(key, steps);
		await Drawn();
		await Task.Run(work);
	}

	/// <summary>As <see cref="Work"/>, the work answering something.</summary>
	public async Task<T> Work<T>(string key, Func<T> work, int steps = 0)
	{
		Say(key, steps);
		await Drawn();
		return await Task.Run(work);
	}

	/// <summary>A step that makes nodes or textures: its line shown and drawn, then the work run on the main thread.</summary>
	public async Task OnMain(string key, Action work, int steps = 0)
	{
		Say(key, steps);
		await Drawn();
		work();
	}

	/// <summary>Waits for the next frame to have been drawn, so that what was just set is on the screen.</summary>
	public async Task Drawn()
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
	}

	/// <summary>
	/// Waits for the frames under the screen to come at their ordinary pace: the first frames of a new map compile its
	/// shaders and make its materials' state, and take several times as long as the rest. Done when
	/// <see cref="SettledFrames"/> frames in a row each took under <see cref="SettledMilliseconds"/>, or after
	/// <see cref="SettleLimit"/> frames whatever they took.
	/// </summary>
	public async Task Settled()
	{
		int calm = 0;
		long last = System.Diagnostics.Stopwatch.GetTimestamp();
		for (int frame = 0; frame < SettleLimit && calm < SettledFrames; frame++)
		{
			await Drawn();
			long now = System.Diagnostics.Stopwatch.GetTimestamp();
			double took = System.Diagnostics.Stopwatch.GetElapsedTime(last, now).TotalMilliseconds;
			last = now;
			calm = took < SettledMilliseconds ? calm + 1 : 0;
		}
	}

	private const int SettledFrames = 5;
	private const double SettledMilliseconds = 40.0;
	private const int SettleLimit = 240;

	/// <summary>Waits for some frames to have been drawn.</summary>
	public async Task Drawn(int frames)
	{
		for (int frame = 0; frame < frames; frame++)
		{
			await Drawn();
		}
	}
}
