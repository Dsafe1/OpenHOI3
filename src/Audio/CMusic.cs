using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Music;
using OpenHOI3.Content.Settings;
using OpenHOI3.Interface.Music;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Audio;

/// <summary>
/// The soundtrack, as the original plays it (<c>docs/mechanics/music.md</c>; <c>reversing/findings/FINDINGS-music.md</c>):
/// one player for the whole run, which <c>Main</c> owns, so that the songs chosen last outlive a game as the original's do.
///
/// The title theme from the start of the load; in a game, the track stopped on its first frame and a song picked whenever
/// none plays or the last has ended - never on a timer, an event or the pause; the end-of-game screen's own track. A track
/// plays once. Nothing new starts while the settings' master volume is nought or below.
/// </summary>
public partial class CMusic : Node
{
	private CDirectorySettings _directories = null!;
	private Func<CUserSettings> _settings = () => CUserSettings.Default;
	private AudioStreamPlayer _player = null!;
	private readonly CSongPicker _picker = new();
	private float _volumeSent = float.NaN;

	/// <summary>Whether the track playing has come to its end, which the next look takes, as the original's end event.</summary>
	private bool _ended;

	/// <summary>The game whose songs are picked, the country played in it and its songs; null outside a game.</summary>
	private CGameState? _game;
	private Func<CCountry?> _played = () => null;
	private IReadOnlyList<CSong> _songs = Array.Empty<CSong>();

	/// <summary>Where no song is declared: the tracks of <c>music/</c> but the three named ones, the last played and when the game began.</summary>
	private string[]? _fallback;
	private int _lastFallback = -1;
	private ulong _enteredAt;

	/// <summary>Has to be called before the node enters the tree.</summary>
	/// <param name="settings">The player's settings as they stand, asked each frame: the game options window changes them.</param>
	public void Setup(CDirectorySettings directories, Func<CUserSettings> settings)
	{
		_directories = directories;
		_settings = settings;
	}

	public override void _Ready()
	{
		// Music plays on through a paused game, as the original's does.
		ProcessMode = ProcessModeEnum.Always;
		_player = new AudioStreamPlayer { Name = "Player" };
		_player.Finished += () => _ended = true;
		AddChild(_player);
	}

	private bool Audible => _settings().MasterVolume > 0f;

	/// <summary>The title theme, as the startup's load begins, where the master volume is above nought and no game has begun.</summary>
	public void PlayTitle()
	{
		if (Audible && _game is null)
		{
			Play(CMusicTracks.Title);
		}
	}

	/// <summary>
	/// A game's first frame: whatever plays stopped - the title theme, as a rule - and songs picked from now on, in the
	/// played country's scope.
	/// </summary>
	public void EnterGame(CGameState state, Func<CCountry?> played, IReadOnlyList<CSong> songs)
	{
		Stop();
		_game = state;
		_played = played;
		_songs = songs;
		_enteredAt = Time.GetTicksMsec();
		_lastFallback = -1;
	}

	/// <summary>A game left by any road: the music stopped, and no song picked until the next game.</summary>
	public void LeaveGame()
	{
		if (_game is null)
		{
			return;
		}
		_game = null;
		Stop();
	}

	/// <summary>
	/// The end-of-game screen: the game left, and the victory march where the player's faction won, else the dies irae, where
	/// the master volume is above nought. It plays once and carries on into the main menu.
	/// </summary>
	public void PlayEnd(bool won)
	{
		LeaveGame();
		if (Audible)
		{
			Play(won ? CMusicTracks.Victory : CMusicTracks.Defeat);
		}
	}

	/// <summary>
	/// The console's <c>nextsong</c>: the same pick, played at once over what plays, whatever the volume; the name of the
	/// song, or nothing where none could be picked or no game is played.
	/// </summary>
	public string NextSong()
	{
		if (_game is null || PickSong() is not { } path)
		{
			return "";
		}
		Play(path);
		return path;
	}

	/// <summary>The player set to the settings' volume, as they stand - each frame, and before any track starts.</summary>
	private void ApplyVolume()
	{
		CUserSettings settings = _settings();
		float decibels = CMusicTracks.Decibels(settings.MasterVolume, settings.MusicVolume);
		if (decibels != _volumeSent)
		{
			_volumeSent = decibels;
			_player.VolumeDb = decibels;
		}
	}

	/// <summary>Whether the game's hours run beside the frame, when nothing may read the game; null where none does.</summary>
	public Func<bool>? GameBusy { get; set; }

	public override void _Process(double delta)
	{
		ApplyVolume();
		if (_game is null || !Audible)
		{
			return;
		}
		bool ended = _ended;
		_ended = false;
		if (_player.Playing && !ended)
		{
			return;
		}
		// A song's chance asks the game, which is the hours' while they run beside the frame: picked once they are collected.
		if (GameBusy?.Invoke() == true)
		{
			_ended = ended;
			return;
		}
		if (PickSong() is { } path)
		{
			Play(path);
		}
	}

	/// <summary>The next song's path, or null: the picker over the songs, or the fallback list where none is declared.</summary>
	private string? PickSong()
	{
		if (_game is null)
		{
			return null;
		}
		if (_songs.Count == 0)
		{
			return PickFallback();
		}
		CGameState state = _game;
		CEventScope scope = CEventScope.Start(_played()?.Tag);
		int chosen = _picker.Pick(_songs, song => CEventChance.Days(state, song.Chance, scope), () => Random.Shared.Next(),
			state.Rules.Has(Sim.Rules.EFix.WeightedSongs));
		// A path of two characters or fewer is not played: a song with no name.
		return chosen >= 0 && _songs[chosen].Path.Length > 2 ? _songs[chosen].Path : null;
	}

	/// <summary>
	/// Where no <c>music/*.txt</c> declares a song: the track of whole seconds since the game began, modulo the count, never
	/// the last one again. The original waits for the second to turn in a loop, and with a single track for ever; here it waits
	/// a frame (<c>docs/mechanics/music.md</c>, *Ours*).
	/// </summary>
	private string? PickFallback()
	{
		_fallback ??= _directories.ListFiles("music", "*.mp3")
			.Select(path => "music/" + Path.GetFileName(path))
			.Where(CMusicTracks.IsFallbackTrack)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (_fallback.Length == 0)
		{
			return null;
		}
		int index = (int)((Time.GetTicksMsec() - _enteredAt) / 1000UL % (ulong)_fallback.Length);
		if (index == _lastFallback)
		{
			return null;
		}
		_lastFallback = index;
		return _fallback[index];
	}

	/// <summary>A track of the install's, through its layers, played from its start over what plays; a file that will not load is left out and what plays goes on.</summary>
	private void Play(string gameRelative)
	{
		string path = _directories.Resolve(gameRelative);
		if (!File.Exists(path))
		{
			GD.Print($"WARNING: music: {gameRelative} is not in the install.");
			return;
		}
		if (AudioStreamMP3.LoadFromFile(path) is not { } stream)
		{
			GD.Print($"WARNING: music: {gameRelative} could not be read.");
			return;
		}
		_player.Stream = stream;
		_ended = false;
		// The volume first: a track started before the first frame's look would otherwise begin at the player's full volume.
		ApplyVolume();
		_player.Play();
		GD.Print($"new song {gameRelative}");
	}

	private void Stop()
	{
		_player.Stop();
		_ended = false;
	}
}
