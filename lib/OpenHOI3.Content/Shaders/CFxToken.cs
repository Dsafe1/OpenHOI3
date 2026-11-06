namespace OpenHOI3.Content.Shaders;

/// <summary>A token of an effect file, with the line of the preprocessed text it is on.</summary>
public readonly record struct CFxToken(EFxTokenKind Kind, string Text, int Line)
{
	public bool Is(string text) => Kind is EFxTokenKind.Symbol or EFxTokenKind.Identifier && Text == text;
}
