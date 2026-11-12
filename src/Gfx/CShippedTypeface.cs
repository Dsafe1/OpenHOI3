namespace OpenHOI3.Gfx;

/// <summary>
/// One of the open typefaces in <c>fonts/</c>, as the stand-in for a typeface the
/// original names: which file to draw each weight and slant from.
/// </summary>
/// <param name="Regular">The file under <c>res://fonts/</c> for upright text of ordinary weight.</param>
/// <param name="Bold">The file for bold, or null to draw bold from <paramref name="Regular"/>.</param>
/// <param name="Italic">The file for italic, or null to draw it from the upright file.</param>
/// <param name="BoldItalic">The file for bold italic, or null to draw it from the bold file.</param>
/// <param name="SameWidths">
/// Its letters are as wide as the original typeface's at every size, so text set in it
/// is the original's text, and it can be sized by width alone.
/// </param>
/// <param name="VariableWeight">
/// One file holds every weight, and bold is asked of it by number and not by file.
/// </param>
/// <param name="Fetched">
/// The file is not in the repository, its licence forbidding that it be published there: whoever builds the game
/// downloads it into <c>fonts/</c> as <c>fonts/readme.md</c> says, and where it is not there the text is drawn in Liberation
/// Sans, with a warning rather than an error.
/// </param>
public sealed record CShippedTypeface(string Regular, string? Bold = null, string? Italic = null,
	string? BoldItalic = null, bool SameWidths = false, bool VariableWeight = false, bool Fetched = false);
