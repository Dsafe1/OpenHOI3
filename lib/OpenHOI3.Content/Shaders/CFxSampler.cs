using System.Collections.Generic;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A sampler: the texture it reads (<c>Texture = &lt;tex0&gt;</c>) and its states - <c>MinFilter</c>, <c>MagFilter</c>,
/// <c>MipFilter</c>, <c>AddressU</c>, <c>AddressV</c> - each by its name in upper case, the value as written.
/// </summary>
public sealed record CFxSampler(int Line, CFxType Type, string Name, string? Texture, IReadOnlyDictionary<string, string> States);
