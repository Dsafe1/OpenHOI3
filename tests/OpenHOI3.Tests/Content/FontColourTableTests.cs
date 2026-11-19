using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Gui;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>A font's colour tables, against <c>reversing/findings/FINDINGS-fontterrain.md</c>, section 1.</summary>
public class FontColourTableTests
{
	[Fact]
	public void TheFirstValueALetterIsGivenStandsAndALetterIsItsKeysFirstCharacterCaseAndAll()
	{
		var table = new CFontColourTable();
		table.Add(ClausewitzParser.Parse("B = { 0 0 255 } b = { 0 0 0 } B = { 255 0 0 } Wx = { 1 0.5 0 }"));
		table.Add(ClausewitzParser.Parse("W = { 255 255 255 }"));

		Assert.Equal(3, table.Count);
		Assert.True(table.TryGet('B', out CColor blue));
		Assert.Equal(new CColor(0f, 0f, 1f), blue);
		Assert.True(table.TryGet('b', out CColor black));
		Assert.Equal(new CColor(0f, 0f, 0f), black);
		// Divided by 255 only above one, each on its own: 1 is whole and 0.5 half.
		Assert.True(table.TryGet('W', out CColor white));
		Assert.Equal(new CColor(1f, 0.5f, 0f), white);
	}

	[Fact]
	public void ALineThatIsNotThreeNumbersIsPassedOverAndSaidWithItsLine()
	{
		var passed = new List<(string? Key, int Line)>();
		var table = new CFontColourTable();
		table.Add(ClausewitzParser.Parse("R = { 255 0 }\nG = yes\nY = { 255 189 0 0.5 }"), line => passed.Add((line.Key, line.Line)));

		Assert.Equal(new (string?, int)[] { ("R", 1), ("G", 2) }, passed);
		Assert.True(table.TryGet('Y', out CColor yellow));
		Assert.Equal(new CColor(1f, 189f / 255f, 0f, 0.5f), yellow);
	}

	[Fact]
	public void TheSharedTableSaysTheFileOfALinePassedOver()
	{
		ClausewitzNode core = ClausewitzParser.Parse(
			"bitmapfonts = {\n\tbitmapfont = {\n\t\tname = \"vic_18\"\n\t\tcolorcodes = {\n\t\t\tG = { 0 255 }\n\t\t}\n\t}\n}");
		var passed = new List<(string File, string? Key, int Line)>();

		CFontColourTable.Shared(new[] { ("core.gfx", core) }, (file, line) => passed.Add((file, line.Key, line.Line)));

		Assert.Equal(new[] { ("core.gfx", (string?)"G", 5) }, passed);
	}

	[Fact]
	public void TheSharedTableIsFilledInTheListingsByteOrderOfFileNames()
	{
		ClausewitzNode core = ClausewitzParser.Parse(
			"bitmapfonts = { bitmapfont = { name = \"vic_18\" colorcodes = { G = { 0 255 0 } r = { 200 0 0 } } } }");
		ClausewitzNode chat = ClausewitzParser.Parse(
			"bitmapfonts = { bitmapfont = { name = \"chat\" colorcodes = { G = { 0 159 3 } P = { 200 30 170 } } } }");
		ClausewitzNode upper = ClausewitzParser.Parse(
			"bitmapfonts = { bitmapfont = { name = \"z\" colorcodes = { P = { 1 1 1 } } } }");

		// Given in the layers' order; a capital sorts before every small letter.
		CFontColourTable shared = CFontColourTable.Shared(new[] { ("core.gfx", core), ("chatfonts.gfx", chat), ("Zed.gfx", upper) });

		Assert.True(shared.TryGet('G', out CColor green));
		Assert.Equal(new CColor(0f, 159f / 255f, 3f / 255f), green);
		Assert.True(shared.TryGet('P', out CColor purple));
		Assert.Equal(new CColor(1f, 1f, 1f), purple);
		Assert.True(shared.TryGet('r', out _));
	}

	[Fact]
	public void AFontsOwnTableIsItsColourOverrideAlone()
	{
		ClausewitzNode font = ClausewitzParser.Parse(
			"colorcodes = { G = { 0 159 3 } } color_override = { G = { 0 255 0 } Y = { 0 0 180 } }");

		CFontColourTable own = CFontColourTable.Own(font);

		Assert.Equal(2, own.Count);
		Assert.True(own.TryGet('G', out CColor green));
		Assert.Equal(new CColor(0f, 1f, 0f), green);
	}
}
