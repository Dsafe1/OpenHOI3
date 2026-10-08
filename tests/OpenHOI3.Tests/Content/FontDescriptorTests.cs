
using OpenHOI3.Content.Gui;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading a font descriptor for the typeface it names and the size its text was meant
/// to be, against <c>docs/mechanics/interface.md</c>, section 6. The descriptor here is
/// written for the test in the format's own shape; it is not one of an install's.
/// </summary>
public class FontDescriptorTests
{
	private const string Descriptor = """
		info face="Franklin Gothic Demi Cond" size=18 bold=1 italic=0 charset="ANSI" stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=2,2
		common lineHeight=18 base=14 scaleW=256 scaleH=128 pages=1
		char id=32   x=106   y=108   width=1     height=0     xoffset=0     yoffset=14    xadvance=3     page=0
		char id=48   x=10    y=20    width=6     height=9     xoffset=0     yoffset=3     xadvance=7     page=0
		char id=49   x=20    y=20    width=4     height=9     xoffset=1     yoffset=3     xadvance=5     page=0
		char id=65   x=30    y=20    width=8     height=9     xoffset=-1    yoffset=3     xadvance=8     page=0
		kerning first=65 second=49 amount=-1
		""";

	[Fact]
	public void ReadsTheTypefaceTheGlyphsWereMadeFrom()
	{
		CFontDescriptor font = CFontDescriptor.Parse(Descriptor);

		Assert.Equal("Franklin Gothic Demi Cond", font.Face);
		Assert.True(font.Bold);
		Assert.False(font.Italic);
	}

	[Fact]
	public void ReadsTheHeightOfALineAndWhereItsLettersStand()
	{
		CFontDescriptor font = CFontDescriptor.Parse(Descriptor);

		Assert.Equal(18, font.LineHeight);
		Assert.Equal(14, font.Base);
	}

	[Fact]
	public void ATextIsAsWideAsThePenMovesForItsCharacters()
	{
		CFontDescriptor font = CFontDescriptor.Parse(Descriptor);

		Assert.Equal(4, font.Count);
		Assert.Equal(7, font.Advance('0'));
		Assert.Equal(7 + 5 + 3 + 8, font.Width("01 A"));
		Assert.Equal(7, font.Width("0z"));                           // a character it does not have adds nothing
	}

	[Fact]
	public void ACharacterLineIsReadByThePlaceOfItsNumbersAsTheOriginalReadsIt()
	{
		// No names worth the name: the first number is the character, the eighth how far the pen moves.
		CFontDescriptor font = CFontDescriptor.Parse("char a=66 b=0 c=0 d=5 e=9 f=-1 g=3 h=6 i=0");

		Assert.Equal(6, font.Advance('B'));
	}

	[Fact]
	public void ItIsMeasuredByTheLettersAndDigitsItHas()
	{
		CFontDescriptor font = CFontDescriptor.Parse(Descriptor);

		Assert.Equal("01A", font.Measurable());
		Assert.Equal(20, font.Width(font.Measurable()));
	}

	[Fact]
	public void ADescriptorWithNoHeaderStillGivesWidths()
	{
		CFontDescriptor font = CFontDescriptor.Parse("char id=48 x=0 y=0 width=6 height=9 xoffset=0 yoffset=3 xadvance=7");

		Assert.Equal("", font.Face);
		Assert.Equal(0, font.LineHeight);
		Assert.Equal(7, font.Advance('0'));
	}
}
