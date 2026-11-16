using System.IO;
using OpenHOI3.Content.Textures;

namespace OpenHOI3.Tests.Content;

public class TgaDecoderTests
{
	[Fact]
	public void TrueColourRowsRunFromTheFootUnlessTheDescriptorSaysTheTop()
	{
		// Blue, green, red: the first stored row is the picture's bottom.
		byte[] body = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
		CDecodedImage fromFoot = CTgaDecoder.Decode(TestDds.Tga(2, 2, 2, 24, 0, body));
		Assert.Equal((3, 2, 1, 255), TestDds.Pixel(fromFoot, 0, 1));
		Assert.Equal((12, 11, 10, 255), TestDds.Pixel(fromFoot, 1, 0));

		CDecodedImage fromTop = CTgaDecoder.Decode(TestDds.Tga(2, 2, 2, 24, 0x20, body));
		Assert.Equal((3, 2, 1, 255), TestDds.Pixel(fromTop, 0, 0));

		CDecodedImage rightToLeft = CTgaDecoder.Decode(TestDds.Tga(2, 2, 2, 24, 0x30, body));
		Assert.Equal((3, 2, 1, 255), TestDds.Pixel(rightToLeft, 1, 0));
	}

	[Fact]
	public void AColourMapOf32BitsKeepsItsAlpha()
	{
		// Two entries, blue first with alpha last; the map starts at index 5.
		byte[] body = { 30, 20, 10, 128, 60, 50, 40, 255, 6, 5 };
		CDecodedImage image = CTgaDecoder.Decode(TestDds.Tga(1, 2, 1, 8, 0x20, body, colourMapLength: 2, colourMapBits: 32,
			colourMapFirst: 5));

		Assert.Equal((40, 50, 60, 255), TestDds.Pixel(image, 0, 0));
		Assert.Equal((10, 20, 30, 128), TestDds.Pixel(image, 1, 0));
	}

	[Fact]
	public void RunLengthPacketsRepeatOrCopy()
	{
		// A map of two 24-bit entries; a run of two of index 1, then two pixels as they are.
		byte[] body = { 0, 0, 255, 255, 0, 0, 0x81, 1, 0x01, 0, 1 };
		CDecodedImage image = CTgaDecoder.Decode(TestDds.Tga(9, 2, 2, 8, 0x20, body, colourMapLength: 2, colourMapBits: 24));

		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(image, 0, 0));
		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(image, 1, 0));
		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(image, 0, 1));
		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(image, 1, 1));
	}

	[Fact]
	public void SixteenBitsCountTheirAlphaBitOnlyWhereTheDescriptorGivesOne()
	{
		byte[] body = { 0x00, 0x7C };   // red, the top bit clear
		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(CTgaDecoder.Decode(TestDds.Tga(2, 1, 1, 16, 0, body)), 0, 0));
		Assert.Equal((255, 0, 0, 0), TestDds.Pixel(CTgaDecoder.Decode(TestDds.Tga(2, 1, 1, 16, 1, body)), 0, 0));
	}

	[Fact]
	public void GreyIsEachChannel()
	{
		CDecodedImage image = CTgaDecoder.Decode(TestDds.Tga(3, 1, 1, 8, 0, new byte[] { 90 }));
		Assert.Equal((90, 90, 90, 255), TestDds.Pixel(image, 0, 0));
	}

	[Fact]
	public void RefusesAFileCutShort()
	{
		Assert.Throws<InvalidDataException>(() => CTgaDecoder.Decode(TestDds.Tga(2, 2, 2, 24, 0, new byte[5])));
		Assert.Throws<InvalidDataException>(() => CTgaDecoder.Decode(TestDds.Tga(10, 2, 2, 24, 0, new byte[] { 0x83 })));
	}
}
