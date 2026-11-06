using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A pass of an effect's technique written as a Godot shader: its code, the parameters and textures the engine is to set
/// on it, what its vertex shader reads of a vertex, the pass's render states as written, and what could not be
/// translated. See <c>docs/mechanics/shaders.md</c>, section 3.
/// </summary>
public sealed record CGodotShader(string Technique, string Pass, string Code, IReadOnlyList<CShaderUniform> Uniforms,
	IReadOnlyList<CShaderTexture> Textures, IReadOnlyList<CShaderVertexInput> Inputs, IReadOnlyList<CFxPassState> States,
	IReadOnlyList<string> Problems);
