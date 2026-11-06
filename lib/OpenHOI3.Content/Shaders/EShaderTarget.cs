namespace OpenHOI3.Content.Shaders;

/// <summary>
/// What a translation is written for: Godot's 3D renderer, where the effect's vertex shader places the vertex as the
/// original's does - the map - or its 2D canvas, where Godot places the element and the vertex shader runs for what else
/// it gives - the interface. See <c>docs/mechanics/shaders.md</c>, section 3.
/// </summary>
public enum EShaderTarget
{
	Spatial,
	Canvas,
}
