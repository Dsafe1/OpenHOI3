using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A variable at the top of an effect file: a parameter the engine sets (a matrix, a colour, a texture) where it is
/// neither <c>const</c> nor <c>static</c>, otherwise a constant. A texture's annotations name the file it starts with -
/// <c>&lt; string name = "Base.tga"; &gt;</c> or <c>ResourceName</c>.
/// </summary>
public sealed record CFxVariable(int Line, CFxType Type, string Name, int? ArraySize, bool IsConst, bool IsStatic, string? Semantic,
	IReadOnlyDictionary<string, string> Annotations, CFxExpression? Initialiser)
{
	/// <summary>Whether the engine sets it: neither <c>const</c> nor <c>static</c>.</summary>
	public bool IsParameter => !IsConst && !IsStatic;
}
