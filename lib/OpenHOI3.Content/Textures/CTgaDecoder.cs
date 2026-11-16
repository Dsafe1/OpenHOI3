using System;
using System.IO;
using System.Buffers.Binary;

namespace OpenHOI3.Content.Textures;

/// <summary>
/// A TGA file decoded: colour-mapped, true colour or grey, plain or run-length encoded, its rows from the foot unless the
/// descriptor says from the top. Pixels and colour map entries are stored blue first: 15 and 16 bits as five each of red,
/// green and blue under an alpha bit that counts only where the descriptor gives a bit of alpha, 24 bits as blue, green
/// and red, 32 bits with alpha after them. The client hands Godot's own loader every TGA it reads; this takes the
/// colour-mapped ones whose entries are not 24 bits, which Godot's refuses - BlackICE's portrait <c>L600780</c> has 32
/// (<c>docs/mechanics/interface.md</c>, section 5).
/// </summary>
public static class CTgaDecoder
{
	public static CDecodedImage Decode(ReadOnlySpan<byte> file)
	{
		CTgaHeader header = CTgaHeader.Parse(file) ?? throw new InvalidDataException("not a TGA header");
		int width = header.Width, height = header.Height;
		int pixelBytes = (header.PixelBits + 7) / 8;
		int entryBytes = (header.ColourMapEntryBits + 7) / 8;
		ReadOnlySpan<byte> map = default;
		if (header.HasColourMap)
		{
			int mapEnd = header.ColourMapOffset + header.ColourMapLength * entryBytes;
			if (file.Length < mapEnd)
			{
				throw new InvalidDataException($"the file ends at {file.Length} bytes inside its colour map");
			}
			map = file[header.ColourMapOffset..mapEnd];
		}
		// The pixels as stored, each its own bytes, run-length packets expanded.
		var stored = new byte[width * height * pixelBytes];
		int at = header.PixelOffset;
		if (!header.IsRunLength)
		{
			if (file.Length < at + stored.Length)
			{
				throw new InvalidDataException($"the file ends at {file.Length} bytes where its pixels need {at + stored.Length}");
			}
			file.Slice(at, stored.Length).CopyTo(stored);
		}
		else
		{
			for (int filled = 0; filled < stored.Length;)
			{
				if (at >= file.Length)
				{
					throw new InvalidDataException("the file ends inside its run-length packets");
				}
				int packet = file[at++];
				int count = (packet & 0x7F) + 1;
				int length = Math.Min(count * pixelBytes, stored.Length - filled);
				if ((packet & 0x80) != 0)
				{
					if (at + pixelBytes > file.Length)
					{
						throw new InvalidDataException("the file ends inside a run");
					}
					for (int i = 0; i < length; i++)
					{
						stored[filled + i] = file[at + i % pixelBytes];
					}
					at += pixelBytes;
				}
				else
				{
					if (at + length > file.Length)
					{
						throw new InvalidDataException("the file ends inside a packet of pixels");
					}
					file.Slice(at, length).CopyTo(stored.AsSpan(filled));
					at += count * pixelBytes;
				}
				filled += length;
			}
		}
		var pixels = new byte[width * height * 4];
		bool alphaBit = header.AlphaBits > 0;
		for (int row = 0; row < height; row++)
		{
			int y = header.TopToBottom ? row : height - 1 - row;
			for (int column = 0; column < width; column++)
			{
				int x = header.RightToLeft ? width - 1 - column : column;
				ReadOnlySpan<byte> pixel = stored.AsSpan((row * width + column) * pixelBytes, pixelBytes);
				Span<byte> into = pixels.AsSpan((y * width + x) * 4, 4);
				if (header.IsColourMapped)
				{
					int index = (pixelBytes == 1 ? pixel[0] : BinaryPrimitives.ReadUInt16LittleEndian(pixel)) - header.ColourMapFirst;
					if (index < 0 || index >= header.ColourMapLength)
					{
						into.Clear();
						continue;
					}
					Colour(map.Slice(index * entryBytes, entryBytes), header.ColourMapEntryBits, alphaBit, into);
				}
				else if (header.IsGrey)
				{
					into[0] = into[1] = into[2] = pixel[0];
					into[3] = pixelBytes > 1 ? pixel[1] : (byte)255;
				}
				else
				{
					Colour(pixel, header.PixelBits, alphaBit, into);
				}
			}
		}
		return new CDecodedImage(width, height, pixels);
	}

	/// <summary>One stored colour, blue first, as red, green, blue and alpha.</summary>
	private static void Colour(ReadOnlySpan<byte> stored, int bits, bool alphaBit, Span<byte> into)
	{
		switch (bits)
		{
			case 15:
			case 16:
				int value = BinaryPrimitives.ReadUInt16LittleEndian(stored);
				into[0] = CMaskedPixels.Widen((uint)(value >> 10 & 31), 5);
				into[1] = CMaskedPixels.Widen((uint)(value >> 5 & 31), 5);
				into[2] = CMaskedPixels.Widen((uint)(value & 31), 5);
				into[3] = bits == 16 && alphaBit && (value & 0x8000) == 0 ? (byte)0 : (byte)255;
				break;
			case 24:
				into[0] = stored[2];
				into[1] = stored[1];
				into[2] = stored[0];
				into[3] = 255;
				break;
			default:
				into[0] = stored[2];
				into[1] = stored[1];
				into[2] = stored[0];
				into[3] = stored[3];
				break;
		}
	}
}
