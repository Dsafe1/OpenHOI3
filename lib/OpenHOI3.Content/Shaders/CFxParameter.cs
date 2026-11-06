namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A function's parameter: its modifier (<c>in</c>, <c>out</c>, <c>inout</c>, <c>uniform</c> - one the
/// <c>compile</c> line gives - or none), type, name and, on an entry point, its semantic.
/// </summary>
public sealed record CFxParameter(string Modifier, CFxType Type, string Name, int? ArraySize, string? Semantic);
