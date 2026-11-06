namespace OpenHOI3.Content.Shaders;

/// <summary>An expression run for what it does: an assignment, a call.</summary>
public sealed record CFxExpressionStatement(int Line, CFxExpression Expression) : CFxStatement(Line);
