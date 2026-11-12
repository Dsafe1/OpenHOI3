using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Settings;
using OpenHOI3.Content.Sound;
using OpenHOI3.Interface.Sound;

namespace OpenHOI3.Audio;

/// <summary>
/// The original's sound effects: <c>interface/sound.sfx</c>'s sounds, each played by name, 64 voices or one, at the volume the
/// settings give - linear in decibels - and most of them only when not already sounding. See <c>docs/mechanics/sound.md</c>
/// and <c>reversing/findings/FINDINGS-soundeffects.md</c>.
/// </summary>
public partial class CSoundEffects : Node
{
	private CDirectorySettings _directories = null!;
	private Func<CUserSettings> _settings = () => CUserSettings.Default;
	private Dictionary<string, CSoundEffect> _effects = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Each sound's player, made the first time it plays; null where its wav could not be read.</summary>
	private readonly Dictionary<string, AudioStreamPlayer?> _players = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The names asked for that sound.sfx has not got, each reported once, as the original logs them.</summary>
	private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Whether a sound may start: the original's is switched off while a game's screen is being built, and nothing is heard
	/// then (section 6).
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CDirectorySettings directories, Func<CUserSettings> settings)
	{
		_directories = directories;
		_settings = settings;
		_effects = CSoundEffectReader.Load(directories, CDiagnostics.Warn);
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
	}

	/// <summary>Whether any voice of the sound is playing.</summary>
	public bool IsPlaying(string name)
	{
		return _players.TryGetValue(name, out AudioStreamPlayer? player) && player is not null && player.Playing;
	}

	/// <summary>Plays the sound unless it is already playing: the way nearly every sound of the original is played.</summary>
	public void PlayUnlessPlaying(string name)
	{
		if (!IsPlaying(name))
		{
			Play(name);
		}
	}

	/// <summary>Plays the sound on a free voice of its 64, or its one voice from the start; the event window's way.</summary>
	public void Play(string name)
	{
		if (!Enabled || name.Length == 0)
		{
			return;
		}
		CUserSettings settings = _settings();
		float scale = CSoundVolume.Scale(settings.MasterVolume, settings.SoundFxVolume);
		if (scale <= 0f || Player(name) is not { } player || !_effects.TryGetValue(name, out CSoundEffect? effect))
		{
			return;
		}
		player.VolumeDb = CSoundVolume.Decibels(effect.Volume, scale);
		player.Play();	}

	private AudioStreamPlayer? Player(string name)
	{
		if (_players.TryGetValue(name, out AudioStreamPlayer? made))
		{
			return made;
		}
		AudioStreamPlayer? player = null;
		if (!_effects.TryGetValue(name, out CSoundEffect? effect))
		{
			if (_missing.Add(name))
			{
				CDiagnostics.Warn($"sound: no sound '{name}' in {CSoundEffectReader.FilePath}");
			}
		}
		else if (Load(effect) is { } stream)
		{
			player = new AudioStreamPlayer
			{
				Name = "Sound_" + effect.Name,
				Stream = stream,
				MaxPolyphony = effect.Single ? 1 : CSoundEffect.Voices,
				// A pitch is a playback rate in Hz, given against the file's own rate.
				PitchScale = effect.Pitch > 0 && stream.MixRate > 0 ? (float)effect.Pitch / stream.MixRate : 1f,
			};
			if (effect.Pan != 0)
			{
				CDiagnostics.Warn($"sound: '{effect.Name}' has a pan, which is not applied here");
			}
			AddChild(player);
		}
		_players[name] = player;
		return player;
	}

	private AudioStreamWav? Load(CSoundEffect effect)
	{
		string path = _directories.Resolve(effect.Path);
		if (!System.IO.File.Exists(path))
		{
			CDiagnostics.Warn($"sound: '{effect.Name}' names {effect.Path}, which is missing");
			return null;
		}
		AudioStreamWav? stream = AudioStreamWav.LoadFromFile(path);
		if (stream is null)
		{
			CDiagnostics.Warn($"sound: {effect.Path} could not be read");
		}
		return stream;
	}
}
