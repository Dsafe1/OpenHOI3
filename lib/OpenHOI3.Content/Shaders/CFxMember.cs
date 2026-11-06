namespace OpenHOI3.Content.Shaders;

/// <summary><c>x.member</c>: a struct's field or a swizzle such as <c>.rgb</c>.</summary>
public sealed record CFxMember(int Line, CFxExpression Target, string Member) : CFxExpression(Line);
