namespace OpenHOI3.Content.Shaders;

/// <summary><c>break</c>, <c>continue</c> or <c>discard</c>.</summary>
public sealed record CFxJump(int Line, string Keyword) : CFxStatement(Line);
