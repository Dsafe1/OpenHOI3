using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary><c>{ ... }</c>.</summary>
public sealed record CFxBlock(int Line, IReadOnlyList<CFxStatement> Statements) : CFxStatement(Line);
