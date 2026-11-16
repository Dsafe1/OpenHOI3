using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Textures;

namespace OpenHOI3.Tests.Content;

public class MaskedPixelsTests
{
	private const uint Rgb = 0x40, AlphaPixels = 0x1;

	private static byte[] Words(params ushort[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();

	private static CDecodedImage Decode(byte[] file) => CDdsDecoder.Decode(file, CDdsHeader.Parse(file)!);

	[Fact]
	public void WidensAChannelByRepeatingItsBits()
	{
		Assert.Equal(255, CMaskedPixels.Widen(31, 5));
		Assert.Equal(132, CMaskedPixels.Widen(16, 5));
		Assert.Equal(255, CMaskedPixels.Widen(63, 6));
		Assert.Equal(136, CMaskedPixels.Widen(8, 4));
		Assert.Equal(182, CMaskedPixels.Widen(5, 3));
		Assert.Equal(255, CMaskedPixels.Widen(1, 1));
		Assert.Equal(0, CMaskedPixels.Widen(0, 1));
		Assert.Equal(0xAB, CMaskedPixels.Widen(0xABC, 12));
	}

	[Fact]
	public void Reads1555WithItsAlphaBit()
	{
		byte[] file = TestDds.Masked(3, 1, Rgb | AlphaPixels, 16, 0x7C00, 0x03E0, 0x001F, 0x8000,
			Words(0xFC00, 0x7FFF, 0x4000 | 0x8000));
		CDecodedImage image = Decode(file);

		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(image, 0, 0));
		Assert.Equal((255, 255, 255, 0), TestDds.Pixel(image, 1, 0));
		Assert.Equal((132, 0, 0, 255), TestDds.Pixel(image, 2, 0));
	}

	[Fact]
	public void X1555AndX4444AreSolidWhateverTheirSpareBits()
	{
		CDecodedImage x1555 = Decode(TestDds.Masked(1, 1, Rgb, 16, 0x7C00, 0x03E0, 0x001F, 0x8000, Words(0x7FFF)));
		Assert.Equal((255, 255, 255, 255), TestDds.Pixel(x1555, 0, 0));

		CDecodedImage x4444 = Decode(TestDds.Masked(1, 1, Rgb, 16, 0x0F00, 0x00F0, 0x000F, 0, Words(0x0F21)));
		Assert.Equal((255, 34, 17, 255), TestDds.Pixel(x4444, 0, 0));
	}

	[Fact]
	public void Reads565And4444ByTheirMasks()
	{
		CDecodedImage rgb565 = Decode(TestDds.Masked(2, 1, Rgb, 16, 0xF800, 0x07E0, 0x001F, 0, Words(0x07E0, 0x001F)));
		Assert.Equal((0, 255, 0, 255), TestDds.Pixel(rgb565, 0, 0));
		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(rgb565, 1, 0));

		CDecodedImage argb4444 = Decode(TestDds.Masked(1, 1, Rgb | AlphaPixels, 16, 0x0F00, 0x00F0, 0x000F, 0xF000,
			Words(0x8F21)));
		Assert.Equal((255, 34, 17, 136), TestDds.Pixel(argb4444, 0, 0));
	}

	[Fact]
	public void ReadsGreyAlphaAndTwentyFourBits()
	{
		CDecodedImage grey = Decode(TestDds.Masked(1, 1, 0x20000, 8, 0xFF, 0, 0, 0, new byte[] { 77 }));
		Assert.Equal((77, 77, 77, 255), TestDds.Pixel(grey, 0, 0));

		CDecodedImage greyAlpha = Decode(TestDds.Masked(1, 1, 0x20000 | AlphaPixels, 16, 0xFF, 0, 0, 0xFF00,
			new byte[] { 77, 9 }));
		Assert.Equal((77, 77, 77, 9), TestDds.Pixel(greyAlpha, 0, 0));

		CDecodedImage alpha = Decode(TestDds.Masked(1, 1, 0x2, 8, 0, 0, 0, 0xFF, new byte[] { 99 }));
		Assert.Equal((0, 0, 0, 99), TestDds.Pixel(alpha, 0, 0));

		CDecodedImage bgr = Decode(TestDds.Masked(1, 1, Rgb, 24, 0xFF0000, 0xFF00, 0xFF, 0, new byte[] { 3, 2, 1 }));
		Assert.Equal((1, 2, 3, 255), TestDds.Pixel(bgr, 0, 0));
	}

	[Fact]
	public void APaletteTakesItsFourthByteForAlpha()
	{
		var data = new byte[1024 + 2];
		new byte[] { 10, 20, 30, 0 }.CopyTo(data, 4);
		new byte[] { 40, 50, 60, 255 }.CopyTo(data, 8);
		data[1024] = 1;
		data[1025] = 2;
		CDecodedImage image = Decode(TestDds.Masked(2, 1, 0x20, 8, 0, 0, 0, 0, data));

		Assert.Equal((10, 20, 30, 0), TestDds.Pixel(image, 0, 0));
		Assert.Equal((40, 50, 60, 255), TestDds.Pixel(image, 1, 0));
	}

	[Fact]
	public void RefusesAFileCutShort()
	{
		byte[] file = TestDds.Masked(2, 2, Rgb, 16, 0xF800, 0x07E0, 0x001F, 0, Words(1, 2, 3));
		Assert.Throws<InvalidDataException>(() => Decode(file));
	}
}
