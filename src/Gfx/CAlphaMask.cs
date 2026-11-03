using Godot;

namespace OpenHOI3.Gfx;

/// <summary>
/// Where a texture is see-through: a pixel whose alpha is nought. What a sprite with <c>transparencecheck</c> lets a
/// click through at (<c>docs/mechanics/interface.md</c>, section 5).
/// </summary>
public sealed class CAlphaMask
{
	private readonly bool[] _solid;

	public CAlphaMask(Image image)
	{
		Width = image.GetWidth();
		Height = image.GetHeight();
		_solid = new bool[Width * Height];
		for (int y = 0; y < Height; y++)
		{
			for (int x = 0; x < Width; x++)
			{
				_solid[y * Width + x] = image.GetPixel(x, y).A8 != 0;
			}
		}
	}

	public int Width { get; }

	public int Height { get; }

	/// <summary>Whether the pixel is drawn at all; one outside the texture is not.</summary>
	public bool IsSolid(int x, int y)
	{
		return x >= 0 && y >= 0 && x < Width && y < Height && _solid[y * Width + x];
	}
}
