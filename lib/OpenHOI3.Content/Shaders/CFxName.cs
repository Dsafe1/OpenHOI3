namespace OpenHOI3.Content.Shaders;

/// <summary>A variable, parameter or constant named.</summary>
public sealed record CFxName(int Line, string Name) : CFxExpression(Line);
