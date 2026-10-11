using System.Linq;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>The flag in text, against <c>docs/mechanics/tooltips.md</c>, section 7. A letter is one wide.</summary>
public class FlagIconsTests
{
	[Fact]
	public void An_at_and_three_letters_are_a_flag()
	{
		Assert.Equal(new[]
		{
			new CTextPiece(" - ( ", null), new CTextPiece("", "POL"), new CTextPiece("/", null),
			new CTextPiece("", "GER"), new CTextPiece(")", null),
		}, CFlagIcons.Split(" - ( @POL/@GER)").ToArray());
	}

	[Fact]
	public void A_flag_at_the_end_takes_what_letters_there_are()
	{
		Assert.Equal(new[] { new CTextPiece("a", null), new CTextPiece("", "GE") }, CFlagIcons.Split("a@GE").ToArray());
	}

	[Fact]
	public void A_flag_is_24_wide_and_its_letters_nothing()
	{
		Assert.Equal(5f + 24f + 1f, CFlagIcons.Measure(" - ( @GER)", text => text.Length));
	}
}
