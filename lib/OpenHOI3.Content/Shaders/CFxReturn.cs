namespace OpenHOI3.Content.Shaders;

/// <summary><c>return</c>, with a value where the function has one.</summary>
public sealed record CFxReturn(int Line, CFxExpression? Value) : CFxStatement(Line);
