using System.Linq;
using Godot;
using OpenHOI3.Content.Shaders;

namespace OpenHOI3.Gui;

/// <summary>A progress bar's material and the translation that names the effect's parameters on it.</summary>
public sealed class CProgressBarMaterial
{
	private readonly CGodotShader _translation;

	public CProgressBarMaterial(ShaderMaterial material, CGodotShader translation)
	{
		Material = material;
		_translation = translation;
	}

	public ShaderMaterial Material { get; }

	/// <summary>An effect's parameter, by the effect's name for it.</summary>
	public void Set(string fxName, Variant value)
	{
		foreach (CShaderUniform uniform in _translation.Uniforms.Where(u => u.FxName == fxName && !u.Shared))
		{
			Material.SetShaderParameter(uniform.Name, value);
		}
	}

	/// <summary>An effect's texture, for every sampler that reads it.</summary>
	public void SetTexture(string texture, Texture2D value)
	{
		foreach (CShaderTexture sampler in _translation.Textures.Where(t => t.Texture == texture))
		{
			Material.SetShaderParameter(sampler.Name, value);
		}
	}
}
