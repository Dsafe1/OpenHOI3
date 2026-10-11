using System.Linq;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The flag and the unit counter in text, against <c>docs/mechanics/tooltips.md</c>, section 7.
/// A letter is one wide.
/// </summary>
public class FlagIconsTests
{
	[Fact]
	public void An_at_and_three_letters_are_a_flag()
	{
		Assert.Equal(new[]
		{
			new CTextPiece(" - ( ", null), new CTextPiece("", "POL"), new CTextPiece("/", null),
			new CTextPiece("", "GER"), new CTextPiece(")", null),
		}, CTextIcons.Split(" - ( @POL/@GER)").ToArray());
	}

	[Fact]
	public void A_flag_at_the_end_takes_what_letters_there_are()
	{
		Assert.Equal(new[] { new CTextPiece("a", null), new CTextPiece("", "GE") }, CTextIcons.Split("a@GE").ToArray());
	}

	[Fact]
	public void A_flag_is_24_wide_and_its_letters_nothing()
	{
		Assert.Equal(5f + 24f + 1f, CTextIcons.Measure(" - ( @GER)", text => text.Length));
	}

	[Fact]
	public void A_pound_and_a_name_are_a_counter_ending_at_the_first_other_character()
	{
		Assert.Equal(new[]
		{
			new CTextPiece("", null, "infantry_brigade"), new CTextPiece(" 1. Division\n", null),
			new CTextPiece("", null, "artillery_brigade"),
		}, CTextIcons.Split("£infantry_brigade 1. Division\n£artillery_brigade").ToArray());
	}

	[Fact]
	public void A_counter_is_30_wide_and_its_name_nothing()
	{
		Assert.Equal(30f + 2f, CTextIcons.Measure("£armor_brigade x", text => text.Length));
	}
}
