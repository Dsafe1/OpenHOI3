using System;
using System.Linq;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>A province's name laid out on the near map, as <c>docs/mechanics/mapdraw.md</c>, section 4, has the original lay it out.</summary>
public class ProvinceNameLayoutTests
{
	/// <summary>A space of 30 and no height; A and B 100 wide boxes of 80, 60 high, 10 down from the line's top; line 76.</summary>
	private static readonly CFontDescriptor Font = CFontDescriptor.Parse("""
		info face="Test" size=80 bold=0 italic=0
		common lineHeight=76 base=63
		char id=32 x=0 y=0 width=1  height=0  xoffset=0 yoffset=76 xadvance=30
		char id=65 x=0 y=0 width=80 height=60 xoffset=10 yoffset=10 xadvance=100
		char id=66 x=0 y=0 width=80 height=60 xoffset=10 yoffset=10 xadvance=100
		kerning first=65 second=66 amount=-20
		""");

	/// <summary>Scale 2: a font pixel 0.05 map pixels.</summary>
	private static readonly CProvinceLabel Level = new(1000, 500, true, 0, 2);

	[Fact]
	public void OneLineIsCentredAndStandsOnThePoint()
	{
		var letters = CProvinceNameLayout.Lay("AA", Font, Level);
		Assert.Equal(2, letters.Count);
		// Width 200, the pen from −100: the first box 10 to 90 → middle −50; the second at 50. One line: top 76, the box 6 to 66.
		Assert.Equal(1000 - 50 * 0.05f, letters[0].X, 4);
		Assert.Equal(1000 + 50 * 0.05f, letters[1].X, 4);
		Assert.Equal(500 + 36 * 0.05f, letters[0].Z, 4);
		Assert.Equal(0.05f, letters[0].Scale);
	}

	[Fact]
	public void KerningMovesTheNextLetter()
	{
		var letters = CProvinceNameLayout.Lay("AB", Font, Level);
		// Width 180: the pen from −90; A's middle −40, B's 100 − 20 on, −90 + 80 + 50 = 40.
		Assert.Equal(-40 * 0.05f, letters[0].X - 1000, 4);
		Assert.Equal(40 * 0.05f, letters[1].X - 1000, 4);
	}

	[Fact]
	public void LineBreaksBeforeTheWordThatTakesItPast320()
	{
		// "AA AA" is 430 wide at its space-less end, but the test comes only at a space: after "AA " the line is 230, no break;
		// "AA AA AA": after the second space the line is 460 - a break before the second word.
		var letters = CProvinceNameLayout.Lay("AA AA AA", Font, Level);
		Assert.Equal(6, letters.Count);
		float[] rows = letters.Select(l => MathF.Round(l.Z - 500, 3)).Distinct().ToArray();
		// Two lines, the first a line higher than the second, which stands on the point.
		Assert.Equal(new[] { MathF.Round((76 + 36) * 0.05f, 3), MathF.Round(36 * 0.05f, 3) }, rows);
	}

	[Fact]
	public void RotationTurnsTheNameAnticlockwise()
	{
		var letters = CProvinceNameLayout.Lay("A", Font, Level with { Rotation = MathF.PI / 2 });
		// Along north, up west: the box's middle 36 font pixels up is 1.8 west of the point.
		Assert.Equal(1000 - 36 * 0.05f, letters[0].X, 3);
		Assert.Equal(0, letters[0].AlongX, 4);
		Assert.Equal(1, letters[0].AlongZ, 4);
	}

	[Fact]
	public void LongNameIsCutTo49()
	{
		Assert.Equal(49, CProvinceNameLayout.Lay(new string('A', 51), Font, Level).Count);
		Assert.Equal(50, CProvinceNameLayout.Lay(new string('A', 50), Font, Level).Count);
	}
}
