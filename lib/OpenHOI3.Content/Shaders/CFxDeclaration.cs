using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>A local declaration: a type and the names it makes, <c>const</c> or not.</summary>
public sealed record CFxDeclaration(int Line, CFxType Type, bool IsConst, IReadOnlyList<CFxDeclarator> Names) : CFxStatement(Line);
