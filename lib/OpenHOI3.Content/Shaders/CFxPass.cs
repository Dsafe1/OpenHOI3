using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>A technique's pass: its render states and its two shaders, either missing where the fixed pipeline draws.</summary>
public sealed record CFxPass(string Name, IReadOnlyList<CFxPassState> States, CFxShaderBinding? Vertex, CFxShaderBinding? Pixel);
