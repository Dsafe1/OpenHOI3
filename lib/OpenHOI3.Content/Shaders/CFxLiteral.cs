namespace OpenHOI3.Content.Shaders;

/// <summary>A number as written, or <c>true</c> or <c>false</c>.</summary>
public sealed record CFxLiteral(int Line, string Text) : CFxExpression(Line);
