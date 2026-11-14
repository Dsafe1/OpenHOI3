namespace OpenHOI3.Interface.Text;

/// <summary>
/// One of the nine parts a text box's picture is drawn in (<see cref="CTextBoxPicture"/>): where it goes, from the
/// picture's upper left corner, and which pixels of the picture it shows there, stretched to fit.
/// </summary>
public readonly record struct CPicturePart(
	float X, float Y, float Width, float Height, int SourceX, int SourceY, int SourceWidth, int SourceHeight);
