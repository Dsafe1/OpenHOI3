namespace OpenHOI3.Content.Shaders;

/// <summary>A render state a pass sets - <c>ALPHABLENDENABLE = True</c>, <c>ColorOp[0] = Modulate</c> - its name in upper case and the value as written.</summary>
public sealed record CFxPassState(string Name, int? Index, string Value);
