using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// An effect file as read: its variables, samplers, structs, functions and techniques, each in the order the file gives
/// them, and what could not be read. See <c>docs/mechanics/shaders.md</c>.
/// </summary>
public sealed record CFxEffect(IReadOnlyList<CFxVariable> Variables, IReadOnlyList<CFxSampler> Samplers,
	IReadOnlyList<CFxStruct> Structs, IReadOnlyList<CFxFunction> Functions, IReadOnlyList<CFxTechnique> Techniques,
	IReadOnlyList<string> Problems)
{
	public CFxStruct? Struct(string name) => Structs.FirstOrDefault(s => s.Name == name);

	public CFxFunction? Function(string name) => Functions.LastOrDefault(f => f.Name == name);

	public CFxTechnique? Technique(string name) => Techniques.FirstOrDefault(t => t.Name == name);
}
