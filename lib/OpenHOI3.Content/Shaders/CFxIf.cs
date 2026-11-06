namespace OpenHOI3.Content.Shaders;

/// <summary><c>if (condition) then else otherwise</c>.</summary>
public sealed record CFxIf(int Line, CFxExpression Condition, CFxStatement Then, CFxStatement? Else) : CFxStatement(Line);
