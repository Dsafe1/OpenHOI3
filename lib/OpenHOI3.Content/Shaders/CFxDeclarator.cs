namespace OpenHOI3.Content.Shaders;

/// <summary>One name a declaration makes, with its array size where it is an array and its initial value where it has one.</summary>
public sealed record CFxDeclarator(string Name, int? ArraySize, CFxExpression? Initialiser);
