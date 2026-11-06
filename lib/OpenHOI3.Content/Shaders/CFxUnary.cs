namespace OpenHOI3.Content.Shaders;

/// <summary><c>-x</c>, <c>!x</c>, <c>++x</c>, or with <see cref="Postfix"/> <c>x++</c>.</summary>
public sealed record CFxUnary(int Line, string Operator, CFxExpression Operand, bool Postfix = false) : CFxExpression(Line);
