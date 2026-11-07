
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

public class RgbParsingTests
{
	/// <summary>A fallback no game file would produce, so a hit on it is unmistakable.</summary>
	private static readonly CColor Magenta = new(1f, 0f, 1f);

	[Fact]
	public void ReadsByteComponents()
	{
		// The shape used by every country file.
		ClausewitzNode root = ClausewitzParser.Parse("color = { 64  160  167 }");
		CColor color = root.GetRgb("color");

		Assert.Equal(64 / 255f, color.R, 3);
		Assert.Equal(160 / 255f, color.G, 3);
		Assert.Equal(167 / 255f, color.B, 3);
	}

	[Fact]
	public void ReadsFractionalComponents()
	{
		ClausewitzNode root = ClausewitzParser.Parse("color = { 0.25 0.5 1.0 }");
		CColor color = root.GetRgb("color");

		Assert.Equal(0.25f, color.R, 3);
		Assert.Equal(1.0f, color.B, 3);
	}

	[Fact]
	public void AnInterfaceColourDividesEachChannelOverOneAloneAndTakesItsAlphaAsWritten()
	{
		ClausewitzNode root = ClausewitzParser.Parse("a = { 0.0 128 1.0 } b = { 0.0 0.0 0.0 0.0 } c = { 0.5 0.5 } d = { 1 1 1 200 }");

		Assert.Equal(new CColor(0f, 128f / 255f, 1f, 1f), root.GetInterfaceColor("a"));
		// org_bar's: transparent black.
		Assert.Equal(new CColor(0f, 0f, 0f, 0f), root.GetInterfaceColor("b"));
		Assert.Equal(Magenta, root.GetInterfaceColor("c", Magenta));
		Assert.Equal(200f, root.GetInterfaceColor("d").A);
	}

	[Fact]
	public void FallsBackWhenMissingOrShort()
	{
		ClausewitzNode root = ClausewitzParser.Parse("a = { color = { 1 2 } }");
		Assert.Equal(Magenta, root.GetRgb("nope", Magenta));
		Assert.Equal(Magenta, root.Get("a")!.GetRgb("color", Magenta));
	}
}
