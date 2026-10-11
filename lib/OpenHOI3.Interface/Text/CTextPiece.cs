namespace OpenHOI3.Interface.Text;

/// <summary>A piece of a run of text: words, or with <see cref="Tag"/> set a country's flag.</summary>
public readonly record struct CTextPiece(string Text, string? Tag);
