using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>A message's text on the map: its life and its layout, against <c>docs/mechanics/messages.md</c>, section 6.</summary>
public class MapTextTests
{
	private static int FramesDrawn(int gameSpeed)
	{
		var rise = new CMapTextRise(0.005f);
		int drawn = 0;
		while (true)
		{
			rise.Advance(gameSpeed);
			if (rise.Done)
			{
				return drawn;
			}
			drawn++;
		}
	}

	[Fact]
	public void ItLastsTwoHundredFramesAtTheSlowestSpeedAndFortyAtTheFastest()
	{
		Assert.Equal(200, FramesDrawn(0));
		Assert.Equal(100, FramesDrawn(1));
		Assert.Equal(66, FramesDrawn(2));
		Assert.Equal(50, FramesDrawn(3));
		Assert.Equal(40, FramesDrawn(4));
	}

	[Fact]
	public void AnEnhancedGamesTextLivesByTheSecondWhateverTheFramesAre()
	{
		// The same 3.33 seconds at the slowest speed, drawn at thirty frames a second or at a hundred and twenty.
		float Seconds(int gameSpeed, float framesASecond)
		{
			var rise = new CMapTextRise(0.005f);
			int drawn = 0;
			while (true)
			{
				rise.Advance(gameSpeed, CMapTextRise.FramesASecond / framesASecond);
				if (rise.Done)
				{
					return drawn / framesASecond;
				}
				drawn++;
			}
		}

		Assert.Equal(200 / 60f, Seconds(0, 30f), 1);
		Assert.Equal(200 / 60f, Seconds(0, 120f), 1);
		Assert.Equal(200 / 60f, Seconds(0, 60f), 1);
		// And still shorter the faster the game runs, as the original's.
		Assert.Equal(40 / 60f, Seconds(4, 120f), 1);
	}

	[Fact]
	public void ItIsWholeForThreeQuartersOfItsRiseThenFadesAndGrowsToTwiceItsSize()
	{
		var rise = new CMapTextRise(0.005f);
		for (int frame = 0; frame < 150; frame++)
		{
			rise.Advance(0);
		}
		Assert.Equal(1.5f, rise.Height, 4);
		Assert.Equal(1f, rise.Alpha);
		Assert.Equal(1f, rise.Scale);
		Assert.Equal(4.5f, rise.Rise, 4);

		for (int frame = 0; frame < 25; frame++)
		{
			rise.Advance(0);
		}
		Assert.Equal(0.5f, rise.Alpha, 3);
		Assert.Equal(1.5f, rise.Scale, 3);
	}

	[Fact]
	public void ATextOfAHundredCharactersIsCutToNinetyNine()
	{
		Assert.Equal(99, CMapTextLayout.Cut(new string('a', 100)).Length);
		Assert.Equal(99, CMapTextLayout.Cut(new string('a', 99)).Length);
		Assert.Equal(98, CMapTextLayout.Cut(new string('a', 98)).Length);
	}

	[Fact]
	public void TheWidestLineIsMeasuredWithBreaksAndTheRowDrawnWithout()
	{
		// Ten a letter: "aaaa " is 50 wide.
		static float Measure(string run) => run.Length * 10f;
		string text = string.Join(" ", System.Linq.Enumerable.Repeat("aaaa", 8));          // 39 letters, 390 wide

		// The line reaches 300 with six words and their spaces; the seventh would bring it to 350, so it ends there.
		Assert.Equal(300f, CMapTextLayout.WidestLine(text, Measure));
		Assert.Equal(-150f, CMapTextLayout.Start(300f));
		Assert.Equal(text, CMapTextLayout.Row(text));

		// An end of line ends a line in the measuring and is no letter in the row.
		Assert.Equal(40f, CMapTextLayout.WidestLine("aaaa\nbb", Measure));
		Assert.Equal("aaaabb", CMapTextLayout.Row("aaaa\nbb"));
	}
}
