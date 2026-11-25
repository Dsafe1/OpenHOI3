using System;
using System.IO;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Interface.Screenshots;

namespace OpenHOI3.Screenshots;

/// <summary>
/// The original's two picture keys (<c>docs/mechanics/screenshots.md</c>): F11 saves the screen as it was last drawn
/// and, in a game, F12 the whole map by who controls each province - both as bitmaps in <c>Screenshots</c> below the
/// player's folder, both taken before anything else sees the key, each with a notice for fifteen frames. The front
/// end has F11 alone. The map's picture reads the game, so it waits while hours run beside the frame.
/// </summary>
public partial class CPictureKeys : Node
{
	private const string ScreenshotKey = "SCREENSHOT_TAKEN";
	private const string MapKey = "MAP_SAVED";

	private readonly CPictureGate _screenGate = new();
	private readonly CPictureGate _mapGate = new();
	private readonly CNoticeFrames _screenFrames = new();
	private readonly CNoticeFrames _mapFrames = new();

	private CDirectorySettings _directories = null!;
	private Func<string, string> _localise = null!;
	private Func<bool>? _gameBusy;
	private Func<bool, CMapShot?>? _map;
	private Loading.CScreenNotice _screenNotice = null!;
	private Loading.CScreenNotice _mapNotice = null!;
	private bool _shiftAtPress;

	/// <summary>
	/// Has to be called before the node enters the tree. <paramref name="map"/> makes the map's picture, told whether
	/// Shift is held; without it there is no F12, as in the front end.
	/// </summary>
	public void Setup(CDirectorySettings directories, Func<string, string> localise, Func<bool>? gameBusy = null,
		Func<bool, CMapShot?>? map = null)
	{
		_directories = directories;
		_localise = localise;
		_gameBusy = gameBusy;
		_map = map;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_screenNotice = new Loading.CScreenNotice { Name = "ScreenshotNotice" };
		_screenNotice.Setup(_directories);
		AddChild(_screenNotice);
		_mapNotice = new Loading.CScreenNotice { Name = "MapNotice" };
		_mapNotice.Setup(_directories);
		AddChild(_mapNotice);
	}

	/// <summary>The keys are taken here, ahead of the interface and of whatever else reads keys, and nothing else sees them.</summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true } key)
		{
			return;
		}
		if (key.Keycode == Key.F11)
		{
			_screenGate.Ask();
		}
		else if (key.Keycode == Key.F12 && _map is not null)
		{
			_mapGate.Ask();
			_shiftAtPress = key.ShiftPressed;
		}
		else
		{
			return;
		}
		GetViewport().SetInputAsHandled();
	}

	public override void _Process(double delta)
	{
		if (_screenGate.Frame())
		{
			TakeScreen();
			// Saved or not, as the original: the notice follows the attempt.
			_screenFrames.Start();
		}
		// The map's picture reads the game: a press waits out the hours that run beside the frame.
		if (!(_mapGate.Asked && _gameBusy?.Invoke() == true) && _mapGate.Frame() && TakeMap())
		{
			_mapFrames.Start();
		}
		_screenNotice.Line = _screenFrames.Frame() ? _localise(ScreenshotKey) : "";
		_mapNotice.Line = _mapFrames.Frame() ? _localise(MapKey) : "";
	}

	private void TakeScreen()
	{
		try
		{
			using Image? image = GetViewport().GetTexture()?.GetImage();
			if (image is null || image.IsEmpty())
			{
				GD.PrintErr("Failed taking screenshot: there is no picture of the screen.");
				return;
			}
			image.Convert(Image.Format.Rgb8);
			byte[] pixels = image.GetData();
			// Red, green, blue as the engine has them; the file wants blue first.
			for (int at = 0; at + 2 < pixels.Length; at += 3)
			{
				(pixels[at], pixels[at + 2]) = (pixels[at + 2], pixels[at]);
			}
			string? path = Save(CScreenshotNames.ScreenStem, image.GetWidth(), image.GetHeight(), pixels);
			if (path is not null)
			{
				GD.Print($"Screenshot taken: {path}");
			}
		}
		catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
		{
			GD.PrintErr($"Failed taking screenshot: {failed.Message}");
		}
	}

	private bool TakeMap()
	{
		try
		{
			if (_map!(_shiftAtPress || Input.IsKeyPressed(Key.Shift)) is not { } shot)
			{
				return false;
			}
			string? path = Save(shot.Stem, shot.Width, shot.Height, shot.Pixels);
			if (path is null)
			{
				return false;
			}
			GD.Print($"Map look saved: {path}");
			return true;
		}
		catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
		{
			GD.PrintErr($"Failed saving the map's picture: {failed.Message}");
			return false;
		}
	}

	/// <summary>Writes a picture under the first free name of its stem; null where all 9999 are taken.</summary>
	private string? Save(string stem, int width, int height, byte[] topDownBgr)
	{
		string folder = Path.Combine(_directories.UserFolder, CScreenshotNames.Folder);
		if (CScreenshotNames.First(stem, name => File.Exists(Path.Combine(folder, name))) is not { } name)
		{
			return null;
		}
		Directory.CreateDirectory(folder);
		string path = Path.Combine(folder, name);
		File.WriteAllBytes(path, CBitmapWriter.Write(width, height, topDownBgr));
		return path;
	}
}
