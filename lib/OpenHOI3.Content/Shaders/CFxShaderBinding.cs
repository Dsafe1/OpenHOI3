using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A pass's <c>VertexShader = compile vs_2_0 Name(arguments)</c> or <c>PixelShader = ...</c>: the shader model it is
/// compiled for, the entry point, and the values its <c>uniform</c> parameters are given.
/// </summary>
public sealed record CFxShaderBinding(string Target, string Function, IReadOnlyList<CFxExpression> Arguments);
