namespace OpenHOI3.Content.Shaders;

/// <summary>A struct's field, with its semantic where it has one - <c>POSITION</c>, <c>TEXCOORD0</c>, <c>COLOR</c>.</summary>
public sealed record CFxField(CFxType Type, string Name, int? ArraySize, string? Semantic);
