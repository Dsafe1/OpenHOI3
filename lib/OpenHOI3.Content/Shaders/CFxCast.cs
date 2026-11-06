namespace OpenHOI3.Content.Shaders;

/// <summary><c>(type)x</c>, among them <c>(VS_OUTPUT)0</c>, which makes a struct of noughts.</summary>
public sealed record CFxCast(int Line, CFxType Type, CFxExpression Operand) : CFxExpression(Line);
