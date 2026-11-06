namespace OpenHOI3.Content.Shaders;

/// <summary><c>target = value</c>, or a compound such as <c>+=</c>.</summary>
public sealed record CFxAssign(int Line, string Operator, CFxExpression Target, CFxExpression Value) : CFxExpression(Line);
