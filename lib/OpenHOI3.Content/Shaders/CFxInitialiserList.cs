using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary><c>{ a, b, c }</c>, which initialises a vector, a matrix, an array or a struct.</summary>
public sealed record CFxInitialiserList(int Line, IReadOnlyList<CFxExpression> Items) : CFxExpression(Line);
