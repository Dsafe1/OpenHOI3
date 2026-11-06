using OpenHOI3.Content.Saves;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Content;

/// <summary>A save's head as the lobby's row reads it, by position, as <c>docs/mechanics/frontend.md</c>, section 3, has it.</summary>
public class SaveHeaderTests
{
	[Fact]
	public void The_date_is_the_first_value_and_the_player_the_second()
	{
		CSaveHeader header = CSaveHeader.Parse("date=\"1941.7.16.18\"\r\nplayer=\"IRE\"\r\nai=\r\n{\r\n");

		Assert.True(CSaveValues.TryHours("1941.7.16.18", out long hours));
		Assert.Equal(hours, header.Hours);
		Assert.Equal(new CCountryTag("IRE"), header.Player);
	}

	[Fact]
	public void After_a_scenario_the_player_is_the_third_and_with_none_there_is_no_player()
	{
		Assert.Equal(new CCountryTag("GER"), CSaveHeader.Parse("date=\"1942.6.28.0\"\nscenario=\"fall_blau\"\nplayer=\"GER\"\n").Player);
		// A multiplayer save has no player: its second key is something else.
		Assert.Null(CSaveHeader.Parse("date=\"1936.1.1.0\"\nrebel=1\nconvoy=262\n").Player);
	}
}
