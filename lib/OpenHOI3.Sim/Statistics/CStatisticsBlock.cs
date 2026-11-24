using System;
using System.IO;
using System.IO.Compression;

namespace OpenHOI3.Sim.Statistics;

/// <summary>
/// One country's year of the statistics record, packed: every series a run of its day-to-day differences, each
/// difference a number of as few bytes as it needs, and the whole deflated. A finished year is packed once and kept
/// so. See <c>docs/mechanics/statisticsrecord.md</c>, section 4.
/// </summary>
public static class CStatisticsBlock
{
	/// <summary>The format's number, the block's first byte: another is not unpacked.</summary>
	public const byte Version = 1;

	/// <summary>
	/// Packs a year: <paramref name="series"/> a row for each series, every row <paramref name="days"/> values long.
	/// </summary>
	public static byte[] Pack(long[][] series, int days)
	{
		using var plain = new MemoryStream();
		foreach (long[] row in series)
		{
			long last = 0;
			for (int day = 0; day < days; day++)
			{
				long value = day < row.Length ? row[day] : last;
				WriteNumber(plain, value - last);
				last = value;
			}
		}
		using var packed = new MemoryStream();
		packed.WriteByte(Version);
		WriteCount(packed, series.Length);
		WriteCount(packed, days);
		using (var deflate = new DeflateStream(packed, CompressionLevel.SmallestSize, leaveOpen: true))
		{
			plain.Position = 0;
			plain.CopyTo(deflate);
		}
		return packed.ToArray();
	}

	/// <summary>The rows of a packed year, or none where the bytes are not a block of this format.</summary>
	public static long[][]? Unpack(byte[] block)
	{
		try
		{
			using var packed = new MemoryStream(block);
			if (packed.ReadByte() != Version)
			{
				return null;
			}
			int count = ReadCount(packed);
			int days = ReadCount(packed);
			if (count < 0 || days < 0 || count > 100_000 || days > 400)
			{
				return null;
			}
			using var deflate = new DeflateStream(packed, CompressionMode.Decompress);
			using var plain = new MemoryStream();
			deflate.CopyTo(plain);
			plain.Position = 0;
			var series = new long[count][];
			for (int row = 0; row < count; row++)
			{
				series[row] = new long[days];
				long last = 0;
				for (int day = 0; day < days; day++)
				{
					last += ReadNumber(plain);
					series[row][day] = last;
				}
			}
			return series;
		}
		catch (Exception e) when (e is InvalidDataException or EndOfStreamException)
		{
			return null;
		}
	}

	/// <summary>A whole number with its sign folded into its lowest bit, seven bits to a byte, the last byte's top bit clear.</summary>
	private static void WriteNumber(Stream stream, long value)
	{
		ulong folded = (ulong)((value << 1) ^ (value >> 63));
		while (folded >= 0x80)
		{
			stream.WriteByte((byte)(folded | 0x80));
			folded >>= 7;
		}
		stream.WriteByte((byte)folded);
	}

	private static long ReadNumber(Stream stream)
	{
		ulong folded = 0;
		for (int shift = 0; shift < 70; shift += 7)
		{
			int read = stream.ReadByte();
			if (read < 0)
			{
				throw new EndOfStreamException();
			}
			folded |= (ulong)(read & 0x7F) << shift;
			if ((read & 0x80) == 0)
			{
				return (long)(folded >> 1) ^ -(long)(folded & 1);
			}
		}
		throw new InvalidDataException("a number of the statistics record runs past ten bytes");
	}

	private static void WriteCount(Stream stream, int count) => WriteNumber(stream, count);

	private static int ReadCount(Stream stream) => (int)ReadNumber(stream);
}
