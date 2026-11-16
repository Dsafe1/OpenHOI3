using System;
using System.IO;
using OpenHOI3.Content.Textures;

namespace OpenHOI3.Tests.Content;

public class TextureSniffingTests
{
	[Fact]
	public void AFileIsToldByWhatItHoldsNotItsName()
	{
		Assert.Equal(ETextureFormat.Dds, CTextureSniffer.Sniff(TestDds.Compressed("DXT1", 4, 4, new byte[8])));
		Assert.Equal(ETextureFormat.Png, CTextureSniffer.Sniff(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }));
		Assert.Equal(ETextureFormat.Jpeg, CTextureSniffer.Sniff(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 }));
		Assert.Equal(ETextureFormat.Bmp, CTextureSniffer.Sniff(TestBitmap.Rgb24(1, new[] { new (byte, byte, byte)[] { (1, 2, 3) } })));
		// BlackICE's TGA counters named .dds: 32 bits, true colour, eight bits of alpha.
		Assert.Equal(ETextureFormat.Tga, CTextureSniffer.Sniff(TestDds.Tga(2, 64, 64, 32, 8, new byte[64 * 64 * 4])));
		Assert.Equal(ETextureFormat.Tga, CTextureSniffer.Sniff(TestDds.Tga(9, 2, 2, 8, 0, new byte[4], 1, 32)));
	}

	[Fact]
	public void WhatNoFormatCouldBeIsUnknown()
	{
		Assert.Equal(ETextureFormat.Unknown, CTextureSniffer.Sniff(new byte[3]));
		var bytes = new byte[40];
		Array.Fill(bytes, (byte)0x55);
		Assert.Equal(ETextureFormat.Unknown, CTextureSniffer.Sniff(bytes));
		// A TGA header in all but a pixel depth no TGA has.
		Assert.Equal(ETextureFormat.Unknown, CTextureSniffer.Sniff(TestDds.Tga(2, 4, 4, 12, 0, new byte[24])));
		// Colour-mapped with no map.
		Assert.Equal(ETextureFormat.Unknown, CTextureSniffer.Sniff(TestDds.Tga(1, 4, 4, 8, 0, new byte[16])));
	}

	[Fact]
	public void ADdsHeaderSaysItsFormatAndShape()
	{
		CDdsHeader odd = CDdsHeader.Parse(TestDds.Compressed("DXT3", 291, 27, new byte[0], mipMapCount: 9))!;
		Assert.Equal(291, odd.Width);
		Assert.Equal(27, odd.Height);
		Assert.True(odd.IsBlockCompressed);
		Assert.Equal(16, odd.BlockBytes);
		Assert.False(odd.IsWholeBlocks);
		Assert.True(odd.HasMipmaps);

		CDdsHeader counter = CDdsHeader.Parse(TestDds.Masked(64, 64, 0x41, 16, 0x7C00, 0x03E0, 0x1F, 0x8000, new byte[0]))!;
		Assert.False(counter.IsBlockCompressed);
		Assert.Equal("", counter.FourCc);
		Assert.Equal(0x8000u, counter.AlphaMask);
		Assert.False(counter.HasMipmaps);

		Assert.Null(CDdsHeader.Parse(new byte[100]));
		Assert.Null(CDdsHeader.Parse(TestDds.Tga(2, 64, 64, 32, 8, new byte[200])));
	}

	[Fact]
	public void AnOddSizedCompressedPictureIsDecodedWhole()
	{
		// 5 by 6: two blocks across and two down, DXT3, each block's colour its own.
		var blocks = new byte[4 * 16];
		ushort[] colours = { 0xF800, 0x07E0, 0x001F, 0xFFFF };
		for (int block = 0; block < 4; block++)
		{
			BitConverter.GetBytes(ulong.MaxValue).CopyTo(blocks, block * 16);
			BitConverter.GetBytes(colours[block]).CopyTo(blocks, block * 16 + 8);
		}
		byte[] file = TestDds.Compressed("DXT3", 5, 6, blocks);
		CDdsHeader header = CDdsHeader.Parse(file)!;

		Assert.True(CDdsDecoder.Reads(header));
		CDecodedImage image = CDdsDecoder.Decode(file, header);
		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(image, 3, 3));
		Assert.Equal((0, 255, 0, 255), TestDds.Pixel(image, 4, 3));
		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(image, 0, 5));
		Assert.Equal((255, 255, 255, 255), TestDds.Pixel(image, 4, 5));

		Assert.Throws<InvalidDataException>(() => CDdsDecoder.Decode(file[..^1], header));
	}

	[Fact]
	public void WhatIsNotReadHereIsSaid()
	{
		byte[] dx10 = TestDds.Compressed("DX10", 4, 4, new byte[36]);
		CDdsHeader header = CDdsHeader.Parse(dx10)!;
		Assert.False(CDdsDecoder.Reads(header));
		Assert.Throws<InvalidDataException>(() => CDdsDecoder.Decode(dx10, header));
	}

	[Fact]
	public void ARecompressedPictureTakesTheNearestColourOfFiveSixAndFiveBits()
	{
		// TFH's political colour map's land grey, as the maintainer's screenshot has it (docs/mechanics/mapdraw.md, section 6).
		byte[] pixels = { 121, 121, 121, 77, 255, 0, 8, 255 };
		CRecompression.ToNearest565(pixels);
		Assert.Equal(new byte[] { 123, 121, 123, 77, 255, 0, 8, 255 }, pixels);

		Assert.False(CRecompression.Applies(2, 128));
		Assert.False(CRecompression.Applies(1, 1));
		Assert.True(CRecompression.Applies(291, 27));
	}
}
