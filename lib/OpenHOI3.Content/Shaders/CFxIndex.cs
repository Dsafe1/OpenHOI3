namespace OpenHOI3.Content.Shaders;

/// <summary><c>x[i]</c>: an array's element, a vector's component or a matrix's row.</summary>
public sealed record CFxIndex(int Line, CFxExpression Target, CFxExpression Index) : CFxExpression(Line);
