using System;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// Every province's pixels in one flat list, as <c>y * Width + x</c>: by id, and within an id from the south row by row and
/// from the west, the order everything in the cache walks them in.
/// </summary>
public sealed class CProvincePixels
{
	private readonly int[] _starts;
	private readonly int[] _pixels;
	private readonly int _width;

	public CProvincePixels(CMapCacheSource source)
	{
		_width = source.Width;
		int count = Math.Max(source.ProvinceCount, 1);
		_starts = new int[count + 1];
		foreach (ushort province in source.Provinces)
		{
			if (province < count)
			{
				_starts[province + 1]++;
			}
		}
		for (int id = 0; id < count; id++)
		{
			_starts[id + 1] += _starts[id];
		}
		_pixels = new int[_starts[count]];
		var next = new int[count];
		Array.Copy(_starts, next, count);
		for (int at = 0; at < source.Provinces.Length; at++)
		{
			ushort province = source.Provinces[at];
			if (province < count)
			{
				_pixels[next[province]++] = at;
			}
		}
	}

	/// <summary>A province's pixels, each <c>y * Width + x</c>.</summary>
	public ReadOnlySpan<int> Of(int province) => _pixels.AsSpan(_starts[province], _starts[province + 1] - _starts[province]);

	public int X(int pixel) => pixel % _width;

	public int Y(int pixel) => pixel / _width;

	/// <summary>A province's bounding box, as its least and greatest column and row; the province must have pixels.</summary>
	public (int X0, int Y0, int X1, int Y1) Box(int province)
	{
		ReadOnlySpan<int> pixels = Of(province);
		int x0 = int.MaxValue;
		int x1 = int.MinValue;
		foreach (int pixel in pixels)
		{
			x0 = Math.Min(x0, X(pixel));
			x1 = Math.Max(x1, X(pixel));
		}
		// The list runs from the south, so its ends are the box's.
		return (x0, Y(pixels[0]), x1, Y(pixels[^1]));
	}
}
