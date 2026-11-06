namespace OpenHOI3.Content.Shaders;

/// <summary>Two operands and an operator between them: arithmetic, a comparison or a logical one.</summary>
public sealed record CFxBinary(int Line, string Operator, CFxExpression Left, CFxExpression Right) : CFxExpression(Line);
