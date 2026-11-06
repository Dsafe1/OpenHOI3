namespace OpenHOI3.Content.Shaders;

/// <summary><c>condition ? whenTrue : whenFalse</c>.</summary>
public sealed record CFxConditional(int Line, CFxExpression Condition, CFxExpression WhenTrue, CFxExpression WhenFalse)
	: CFxExpression(Line);
