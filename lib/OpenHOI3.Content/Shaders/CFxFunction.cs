using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>A function: what it returns - with the semantic an entry point's return has - its parameters and its body.</summary>
public sealed record CFxFunction(int Line, CFxType ReturnType, string Name, IReadOnlyList<CFxParameter> Parameters,
	string? ReturnSemantic, CFxBlock Body);
