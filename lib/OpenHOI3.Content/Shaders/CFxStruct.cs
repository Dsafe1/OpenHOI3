using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>A struct: what a vertex shader is given or gives, or a value of the file's own.</summary>
public sealed record CFxStruct(int Line, string Name, IReadOnlyList<CFxField> Fields);
