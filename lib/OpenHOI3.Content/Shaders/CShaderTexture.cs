namespace OpenHOI3.Content.Shaders;

/// <summary>
/// A sampler of a translation: the shader's uniform it is, the effect's texture it reads, the file that texture's
/// annotation names where it names one, and the filter and repeat the sampler's states give.
/// </summary>
public sealed record CShaderTexture(string FxSampler, string Name, string? Texture, string? File, string Filter, bool Repeat);
