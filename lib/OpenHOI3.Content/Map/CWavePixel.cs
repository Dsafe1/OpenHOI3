using System;
using System.Buffers.Binary;

namespace OpenHOI3.Content.Map;

/// <summary>
/// One of the surf's records in the quad cache, a "wave pixel": a coastal pixel the terrain paints as water, x a column and
/// y counted from the south, and which of the 52 shapes of quad it takes, by where the land lies about it. The record's four
/// floats are left out: each is fixed by the type (<c>reversing/findings/FINDINGS-mapdraw.md</c>, C.7), and the table of
/// corners already holds them. See <c>docs/mechanics/mapdraw.md</c>, section 4.
/// </summary>
public readonly record struct CWavePixel(int X, int Y, int Type)
{
	public const int RecordSize = 0x1C;

	/// <summary>A tile's records as the cache's seventh piece holds them, one after another.</summary>
	public static CWavePixel[] Parse(ReadOnlySpan<byte> records)
	{
		var pixels = new CWavePixel[records.Length / RecordSize];
		for (int i = 0; i < pixels.Length; i++)
		{
			ReadOnlySpan<byte> record = records.Slice(i * RecordSize, RecordSize);
			pixels[i] = new CWavePixel(BinaryPrimitives.ReadInt32LittleEndian(record),
				BinaryPrimitives.ReadInt32LittleEndian(record[4..]), BinaryPrimitives.ReadInt32LittleEndian(record[8..]));
		}
		return pixels;
	}
}
