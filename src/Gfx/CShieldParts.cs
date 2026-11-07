using Godot;
using OpenHOI3.Content.Shaders;

namespace OpenHOI3.Gfx;

/// <summary>
/// What every shield of one <c>maskedShieldType</c> shares: the flag's quad, the box it is drawn in - the overlay's size
/// - the overlay and the mask, the shader of each technique in the order of <see cref="Gui.EButtonLook"/>, and the
/// translation that names the effect's parameters.
/// </summary>
public sealed record CShieldParts(ArrayMesh Quad, Vector2 Box, Texture2D? Overlay, Texture2D Mask, Shader?[] Shaders,
	CGodotShader Translation);
