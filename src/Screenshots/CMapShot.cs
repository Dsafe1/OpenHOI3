namespace OpenHOI3.Screenshots;

/// <summary>The picture of the whole map, made and ready to be written: its name up to the number, its size, and its pixels from the top down, blue, green, red.</summary>
public sealed record CMapShot(string Stem, int Width, int Height, byte[] Pixels);
