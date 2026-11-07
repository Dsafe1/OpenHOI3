namespace OpenHOI3.Interface.Text;

/// <summary>
/// A piece of a run of text: words, or with <see cref="Tag"/> set a country's flag, or with
/// <see cref="Counter"/> set a unit counter.
/// </summary>
public readonly record struct CTextPiece(string Text, string? Tag, string? Counter = null, int? Strength = null);
