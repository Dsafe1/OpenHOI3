namespace OpenHOI3.Interface.Map;

/// <summary>
/// A letter of a country's name on the map: the character, the middle of its glyph's box in map units (x the column, z
/// the row from the south), the way the name reads there as a unit vector, and the map units to one of the font's pixels -
/// so the glyph's box is its width and height in the font's pixels times that, about the middle, along the way it reads.
/// </summary>
public readonly record struct CMapNameLetter(char Character, float X, float Z, float AlongX, float AlongZ, float Scale);
