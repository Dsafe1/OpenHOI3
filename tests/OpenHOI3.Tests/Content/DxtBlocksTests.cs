using System;
using System.IO;
using OpenHOI3.Content.Textures;

namespace OpenHOI3.Tests.Content;

public class DxtBlocksTests
{
	private const ushort Red = 0xF800, Green = 0x07E0, Blue = 0x001F;

	/// <summary>A colour block: two colours and sixteen two-bit codes, texel 0 in the lowest bits.</summary>
	private static byte[] ColourBlock(ushort c0, ushort c1, params int[] codes)
	{
		uint packed = 0;
		for (int texel = 0; texel < codes.Length; texel++)
		{
			packed |= (uint)codes[texel] << (2 * texel);
		}
		var block = new byte[8];
		BitConverter.GetBytes(c0).CopyTo(block, 0);
		BitConverter.GetBytes(c1).CopyTo(block, 2);
		BitConverter.GetBytes(packed).CopyTo(block, 4);
		return block;
	}

	[Fact]
	public void Dxt1OfFourColoursGivesTheThirds()
	{
		CDecodedImage image = CDxtBlocks.Decode(ColourBlock(Red, Blue, 0, 1, 2, 3), 4, 4, "DXT1");

		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(image, 0, 0));
		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(image, 1, 0));
		Assert.Equal((170, 0, 85, 255), TestDds.Pixel(image, 2, 0));
		Assert.Equal((85, 0, 170, 255), TestDds.Pixel(image, 3, 0));
		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(image, 3, 3));
	}

	[Fact]
	public void Dxt1OfThreeColoursHasTheHalfAndSeeThroughBlack()
	{
		byte[] block = ColourBlock(Blue, Red, 0, 1, 2, 3);
		CDecodedImage image = CDxtBlocks.Decode(block, 4, 4, "DXT1");

		Assert.Equal((127, 0, 127, 255), TestDds.Pixel(image, 2, 0));
		Assert.Equal((0, 0, 0, 0), TestDds.Pixel(image, 3, 0));
		Assert.True(CDxtBlocks.HasSeeThroughTexels(block, 4, 4));
		Assert.False(CDxtBlocks.HasSeeThroughTexels(ColourBlock(Blue, Red, 0, 1, 2), 4, 4));
		Assert.False(CDxtBlocks.HasSeeThroughTexels(ColourBlock(Red, Blue, 3, 3), 4, 4));
	}

	[Fact]
	public void APictureNotOfWholeBlocksKeepsItsOwnSize()
	{
		// Five by three: two blocks across, one down, the second's texels past column 4 and row 2 left out.
		byte[] blocks = new byte[16];
		ColourBlock(Red, 0).CopyTo(blocks, 0);
		ColourBlock(Green, 0).CopyTo(blocks, 8);
		CDecodedImage image = CDxtBlocks.Decode(blocks, 5, 3, "DXT1");

		Assert.Equal(5, image.Width);
		Assert.Equal(3, image.Height);
		Assert.Equal(5 * 3 * 4, image.Rgba.Length);
		Assert.Equal((255, 0, 0, 255), TestDds.Pixel(image, 3, 2));
		Assert.Equal((0, 255, 0, 255), TestDds.Pixel(image, 4, 2));
		Assert.Equal(16, CDxtBlocks.LevelBytes(5, 3, 8));
		Assert.Equal(8, CDxtBlocks.LevelBytes(1, 1, 8));
	}

	[Fact]
	public void Dxt3HasFourBitAlphasAndAlwaysFourColours()
	{
		var block = new byte[16];
		BitConverter.GetBytes(0xFul | 0x8ul << 4).CopyTo(block, 0);
		// The colours in three-colour order, which DXT3 ignores: code 3 is a colour, not see-through.
		ColourBlock(Blue, Red, 0, 3, 3).CopyTo(block, 8);
		CDecodedImage image = CDxtBlocks.Decode(block, 4, 4, "DXT3");

		Assert.Equal((0, 0, 255, 255), TestDds.Pixel(image, 0, 0));
		Assert.Equal((170, 0, 85, 136), TestDds.Pixel(image, 1, 0));
		Assert.Equal((170, 0, 85, 0), TestDds.Pixel(image, 2, 0));
	}

	[Fact]
	public void Dxt5InterpolatesItsAlphasInSevenOrFiveSteps()
	{
		static byte[] Block(byte a0, byte a1, params int[] codes)
		{
			var block = new byte[16];
			block[0] = a0;
			block[1] = a1;
			ulong bits = 0;
			for (int texel = 0; texel < codes.Length; texel++)
			{
				bits |= (ulong)codes[texel] << (3 * texel);
			}
			for (int i = 0; i < 6; i++)
			{
				block[2 + i] = (byte)(bits >> (8 * i));
			}
			ColourBlock(Red, Blue).CopyTo(block, 8);
			return block;
		}

		CDecodedImage seven = CDxtBlocks.Decode(Block(200, 100, 0, 1, 2, 7), 4, 4, "DXT5");
		Assert.Equal(200, TestDds.Pixel(seven, 0, 0).A);
		Assert.Equal(100, TestDds.Pixel(seven, 1, 0).A);
		Assert.Equal((6 * 200 + 100) / 7, TestDds.Pixel(seven, 2, 0).A);
		Assert.Equal((200 + 6 * 100) / 7, TestDds.Pixel(seven, 3, 0).A);

		CDecodedImage five = CDxtBlocks.Decode(Block(50, 100, 2, 6, 7), 4, 4, "DXT5");
		Assert.Equal((4 * 50 + 100) / 5, TestDds.Pixel(five, 0, 0).A);
		Assert.Equal(0, TestDds.Pixel(five, 1, 0).A);
		Assert.Equal(255, TestDds.Pixel(five, 2, 0).A);
	}

	[Fact]
	public void RefusesTooFewBlocksAndAnotherFormat()
	{
		Assert.Throws<InvalidDataException>(() => CDxtBlocks.Decode(new byte[8], 5, 4, "DXT1"));
		Assert.Throws<InvalidDataException>(() => CDxtBlocks.Decode(new byte[16], 4, 4, "ATI2"));
	}
}
