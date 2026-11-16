namespace OpenHOI3.Content.Textures;

/// <summary>A picture decoded to four bytes a pixel - red, green, blue, alpha - row after row from the top.</summary>
public sealed record CDecodedImage(int Width, int Height, byte[] Rgba);
