using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Screenshots;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Interface;

/// <summary>The two pictures a game saves, against <c>docs/mechanics/screenshots.md</c>.</summary>
public class ScreenshotTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Slo = new("SLO");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Hun = new("HUN");

	private static readonly CColor Grey = new(0.4f, 0.4f, 0.4f);
	private static readonly CColor Teal = new(0f, 0.8f, 0.6f);
	private static readonly CColor Pink = new(1f, 0.5f, 0.5f);
	private static readonly CColor Brown = new(0.5f, 0.3f, 0.1f);

	private static CColor Colour(bool land, CCountryTag? owner, CCountryTag? controller, bool playersOnly, params CCountryTag[] played)
	{
		var colours = new Dictionary<CCountryTag, CColor> { [Ger] = Grey, [Slo] = Teal, [Pol] = Pink, [Hun] = Brown };
		var overlords = new Dictionary<CCountryTag, CCountryTag?> { [Slo] = Ger, [Hun] = Pol };
		return CMapPicture.Colour(land, owner, controller, playersOnly, tag => colours[tag], tag => Array.IndexOf(played, tag) >= 0,
			tag => overlords.GetValueOrDefault(tag));
	}

	[Fact]
	public void TheMapIsEachProvincesControllerWhiteWaterAndBlackForNobody()
	{
		Assert.Equal(CColor.White, Colour(false, null, null, false));
		Assert.Equal(CColor.White, Colour(false, Ger, Ger, false));
		// The controller's colour, not the owner's.
		Assert.Equal(Grey, Colour(true, Pol, Ger, false));
		Assert.Equal(Pink, Colour(true, Pol, Pol, false));
		Assert.Equal(new CColor(0, 0, 0), Colour(true, null, null, false));
	}

	[Fact]
	public void WithShiftItIsThePlayersLandAndTheirSubjectsAtThreeQuarters()
	{
		var black = new CColor(0, 0, 0);
		Assert.Equal(CColor.White, Colour(false, Ger, Ger, true, Ger));
		Assert.Equal(Grey, Colour(true, Pol, Ger, true, Ger));
		// A subject of a played overlord: the overlord's colour, each part times 0.75.
		Assert.Equal(new CColor((float)(0.4f * 0.75), (float)(0.4f * 0.75), (float)(0.4f * 0.75)), Colour(true, Slo, Slo, true, Ger));
		// A played subject is itself.
		Assert.Equal(Teal, Colour(true, Slo, Slo, true, Ger, Slo));
		// Everything else is black: an unplayed country, a subject of an unplayed overlord, land nobody owns.
		Assert.Equal(black, Colour(true, Pol, Pol, true, Ger));
		Assert.Equal(black, Colour(true, Hun, Hun, true, Ger));
		Assert.Equal(black, Colour(true, null, Ger, true, Ger));
		Assert.Equal(black, Colour(true, Ger, Ger, true));
	}

	[Fact]
	public void AColoursPartIsCutTowardsNoughtAndThePictureRunsFromTheNorth()
	{
		Assert.Equal(255, CMapPicture.Part(1f));
		Assert.Equal(0, CMapPicture.Part(0f));
		Assert.Equal(127, CMapPicture.Part(0.5f));   // 127.5 cut, not rounded
		Assert.Equal(76, CMapPicture.Part(0.3f));    // 76.5000019...

		// Two by two, the raster's rows counting north: province 1 in the south, 2 in the north-west, 9 unknown.
		ushort[] raster = { 1, 1, 2, 9 };
		var colours = new[] { CColor.White, new CColor(1f, 0f, 0f), new CColor(0f, 0f, 1f) };
		byte[] pixels = CMapPicture.Pixels(2, 2, raster, colours);
		Assert.Equal(new byte[]
		{
			255, 0, 0, 255, 255, 255,   // the north row first: province 2 blue first as the file has it, then white for what is none
			0, 0, 255, 0, 0, 255,       // the south row: province 1, red last
		}, pixels);
	}

	[Fact]
	public void TheNamesAreTheOriginals()
	{
		Assert.Equal("HoI3_MAP_GER_1939.9.1.0_", CScreenshotNames.MapStem("GER", false, new CDate(1939, 9, 1), 0));
		Assert.Equal("HoI3_MAP_ENGmp_1941.12.11.23_", CScreenshotNames.MapStem("ENG", true, new CDate(1941, 12, 11), 23));

		var taken = new HashSet<string> { "HoI3_1.bmp", "HoI3_2.bmp", "HoI3_4.bmp" };
		Assert.Equal("HoI3_3.bmp", CScreenshotNames.First(CScreenshotNames.ScreenStem, taken.Contains));
		Assert.Equal("HoI3_MAP_GER_1939.9.1.0_1.bmp", CScreenshotNames.First("HoI3_MAP_GER_1939.9.1.0_", taken.Contains));
		// 9999 is the last number tried.
		int asked = 0;
		Assert.Null(CScreenshotNames.First(CScreenshotNames.ScreenStem, _ => { asked++; return true; }));
		Assert.Equal(9999, asked);
	}

	[Fact]
	public void APictureIsTakenOnceInTenFramesAndAPressBetweenIsDropped()
	{
		var gate = new CPictureGate();
		Assert.False(gate.Frame());
		gate.Ask();
		Assert.True(gate.Frame());
		// The nine frames after take none, a press in them or not, and keep nothing for later.
		for (int frame = 1; frame < CPictureGate.Frames; frame++)
		{
			gate.Ask();
			Assert.False(gate.Frame());
		}
		Assert.False(gate.Frame());
		gate.Ask();
		Assert.True(gate.Frame());
	}

	[Fact]
	public void ANoticeStandsFifteenFramesTheFrameOfThePictureTheFirst()
	{
		var notice = new CNoticeFrames();
		Assert.False(notice.Frame());
		notice.Start();
		for (int frame = 0; frame < CNoticeFrames.Frames; frame++)
		{
			Assert.True(notice.Frame());
		}
		Assert.False(notice.Frame());
	}

	[Fact]
	public void ABitmapIsWrittenFromTheBottomUpWithItsRowsPadded()
	{
		// Three wide, two high: nine bytes a row, padded to twelve.
		byte[] pixels =
		{
			1, 2, 3, 4, 5, 6, 7, 8, 9,
			11, 12, 13, 14, 15, 16, 17, 18, 19,
		};
		byte[] file = CBitmapWriter.Write(3, 2, pixels);
		Assert.Equal(54 + 24, file.Length);
		Assert.Equal((byte)'B', file[0]);
		Assert.Equal((byte)'M', file[1]);
		Assert.Equal(file.Length, BitConverter.ToInt32(file, 2));
		Assert.Equal(54, BitConverter.ToInt32(file, 10));
		Assert.Equal(40, BitConverter.ToInt32(file, 14));
		Assert.Equal(3, BitConverter.ToInt32(file, 18));
		Assert.Equal(2, BitConverter.ToInt32(file, 22));
		Assert.Equal(1, BitConverter.ToInt16(file, 26));
		Assert.Equal(24, BitConverter.ToInt16(file, 28));
		Assert.Equal(0xB12, BitConverter.ToInt32(file, 38));
		Assert.Equal(new byte[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 0, 0, 0 }, file[54..66]);
		Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 0, 0 }, file[66..78]);
		Assert.Throws<ArgumentException>(() => CBitmapWriter.Write(3, 2, new byte[5]));
	}
}
