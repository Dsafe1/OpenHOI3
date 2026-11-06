namespace OpenHOI3.Content.Shaders;

/// <summary><c>for (initialiser; condition; step) body</c>; <c>while</c> is one with neither initialiser nor step.</summary>
public sealed record CFxFor(int Line, CFxStatement? Initialiser, CFxExpression? Condition, CFxExpression? Step, CFxStatement Body)
	: CFxStatement(Line);
