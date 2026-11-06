using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>A call by name: a function of the file, an intrinsic, or a type's constructor such as <c>float4(...)</c>.</summary>
public sealed record CFxCall(int Line, string Name, IReadOnlyList<CFxExpression> Arguments) : CFxExpression(Line);
