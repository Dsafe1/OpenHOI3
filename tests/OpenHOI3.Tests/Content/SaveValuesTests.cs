using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>A save's values as <c>docs/mechanics/savegame.md</c>, section 3, has them.</summary>
public class SaveValuesTests
{
	[Fact]
	public void A_date_with_its_hour_is_hours_and_one_without_is_its_midnight()
	{
		Assert.True(CSaveValues.TryHours("1936.1.7.22", out long hours));
		Assert.True(CDate.TryParse("1936.1.7", out CDate date));
		Assert.Equal((long)date.DayNumber * 24 + 22, hours);
		Assert.True(CSaveValues.TryHours("\"1936.1.7\"", out long midnight));
		Assert.Equal((long)date.DayNumber * 24, midnight);
		Assert.False(CSaveValues.TryHours("tomorrow", out _));
	}

	[Fact]
	public void Figures_are_thousandths_shares_five_places_and_an_id_its_serial()
	{
		Assert.Equal(55000, CSaveValues.Fixed("55.000").Raw);
		Assert.Equal(CFixedPoint.Zero, CSaveValues.Fixed((string?)null));
		Assert.Equal(new CFixed15((long)System.Math.Round(0.32m * CFixed15.Scale)), CSaveValues.Fixed15("0.32000"));
		ClausewitzNode unit = ClausewitzParser.Parse("id = { id = 4171 type = 41 }");
		Assert.Equal(4171, CSaveValues.Id(unit.Get("id")));
		Assert.Equal(3912752718L, CSaveValues.Whole("3912752718"));
	}
}
