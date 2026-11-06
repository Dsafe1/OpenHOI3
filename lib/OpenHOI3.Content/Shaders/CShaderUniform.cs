using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A parameter of an effect the engine sets, as a translation names it: the effect's name and the shader's - the same
/// but where the effect's is a word of Godot's language - the effect's type and the shader's, its array size, its
/// semantic and its annotations. A matrix of the effect's rows by columns is a square matrix in the shader whose columns
/// are the effect's rows, padded with noughts (<c>docs/mechanics/shaders.md</c>, section 3).
/// </summary>
public sealed record CShaderUniform(string FxName, string Name, CFxType FxType, string Type, int? ArraySize, string? Semantic,
	IReadOnlyDictionary<string, string> Annotations);
