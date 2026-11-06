namespace OpenHOI3.Content.Shaders;

/// <summary>
/// What a translation's vertex shader takes of a vertex, by its semantic - <c>POSITION</c>, <c>TEXCOORD1</c> - and the
/// channel of a Godot mesh it is read from (<c>VERTEX</c>, <c>UV</c>, <c>CUSTOM0</c>...), so the engine builds its meshes
/// with each in its place.
/// </summary>
public sealed record CShaderVertexInput(string Semantic, string Channel, int Components);
