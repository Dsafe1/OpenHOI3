using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>A technique: a way of drawing the engine picks by name - <c>WaterFar</c>, <c>TerrainShader_General</c> - and its passes.</summary>
public sealed record CFxTechnique(int Line, string Name, IReadOnlyList<CFxPass> Passes);
