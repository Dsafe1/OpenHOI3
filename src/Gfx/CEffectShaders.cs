using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Shaders;

namespace OpenHOI3.Gfx;

/// <summary>
/// The install's effect files as Godot shaders: each read through the layers - a mod's over the install's - the first
/// time it is asked for, each pass of a technique translated for the target asked and made a <see cref="Shader"/> once,
/// and what a translation reports told once. A technique the file does not have, or a file no layer has, gives none.
/// See <c>docs/mechanics/shaders.md</c>.
/// </summary>
public sealed class CEffectShaders
{
	private readonly CDirectorySettings _directories;
	private readonly Dictionary<string, CFxEffect?> _effects = new(System.StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<(string File, string Technique, int Pass, EShaderTarget Target, string? Drawn, string States), (Shader? Shader, CGodotShader? Translation)> _shaders = new();

	public CEffectShaders(CDirectorySettings directories)
	{
		_directories = directories;
	}

	/// <summary>An effect file as read, or null where no layer has it.</summary>
	public CFxEffect? Effect(string file)
	{
		if (!_effects.TryGetValue(file, out CFxEffect? effect))
		{
			effect = CFxEffectReader.Read(_directories, file);
			_effects[file] = effect;
			if (effect is null)
			{
				Content.CDiagnostics.Warn($"shaders: no layer has gfx/FX/{file}");
			}
			else
			{
				foreach (string problem in effect.Problems)
				{
					Content.CDiagnostics.Warn($"shaders: {file}: {problem}");
				}
			}
		}
		return effect;
	}

	/// <summary>
	/// A pass of a technique of an effect file as a Godot shader, with its translation - what the engine is to set on it -
	/// or nulls where there is no such technique. <paramref name="device"/> is the device's render states around the draw,
	/// which the pass's own override (<c>shaders.md</c>, section 3.7), and <paramref name="shared"/> the parameters made
	/// Godot's global uniforms and their names, which the caller registers first (section 3.11).
	/// </summary>
	public (Shader? Shader, CGodotShader? Translation) Get(string file, string technique, EShaderTarget target,
		string? drawnTexture = null, int pass = 0, IReadOnlyDictionary<string, string>? device = null,
		IReadOnlyDictionary<string, string>? shared = null)
	{
		string states = device is null ? "" : string.Join(";", System.Linq.Enumerable.Select(
			System.Linq.Enumerable.OrderBy(device, s => s.Key, System.StringComparer.Ordinal), s => s.Key + "=" + s.Value));
		states += "|" + (shared is null ? "" : string.Join(";", System.Linq.Enumerable.Select(
			System.Linq.Enumerable.OrderBy(shared, s => s.Key, System.StringComparer.Ordinal), s => s.Key + "=" + s.Value)));
		var key = (file, technique, pass, target, drawnTexture, states);
		if (_shaders.TryGetValue(key, out var made))
		{
			return made;
		}
		made = (null, null);
		if (Effect(file) is { } effect && effect.Technique(technique) is { } found && pass < found.Passes.Count)
		{
			CGodotShader translation = CGodotShaderWriter.Write(effect, found, found.Passes[pass], target, drawnTexture, device, shared);
			foreach (string problem in translation.Problems)
			{
				Content.CDiagnostics.Warn($"shaders: {file} {technique}.{found.Passes[pass].Name}: {problem}");
			}
			made = (new Shader { Code = translation.Code }, translation);
		}
		else if (Effect(file) is not null)
		{
			Content.CDiagnostics.Warn($"shaders: gfx/FX/{file} has no technique '{technique}'");
		}
		_shaders[key] = made;
		return made;
	}
}
