using System.Linq;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Interface.BattlePlans;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Tests.Interface;

/// <summary>Battle plans, against <c>docs/mechanics/battleplans.md</c>.</summary>
public class BattlePlanTests
{
	private const string Sample = "title=\"Fall Gelb\"\r\ncountry=\"GER\"\r\narrows_count=1\r\ncontrol_points=\r\n{\r\n0 0 5000 0 10000 2500 }\r\ntext=\"7. Panzer\"\r\n"
		+ "matrix_transforms=\r\n{\r\n3281167 1630639 }\r\nignore_smoothing=\r\n{\r\n-1 }\r\ntype=\r\n{\r\nba_1 }\r\ncolor=\r\n{\r\n2 }\r\n"
		+ "icons_count=1\r\npositions=\r\n{\r\n8437 1830685 }\r\nmatrix_transforms=\r\n{\r\n3205919 1802316 }\r\ntype=\r\n{\r\nmi_1 }\r\ncolor=\r\n{\r\n0 }\r\nrotation=\r\n{\r\n-2850 }\r\n"
		+ "texts_count=1\r\npositions=\r\n{\r\n1000 2000 }\r\nmatrix_transforms=\r\n{\r\n3000 4000 }\r\ntext=\"Maas\"\r\nscale=\r\n{\r\n2000 }\r\nrotation=\r\n{\r\n0 }\r\n"
		+ "is_battle_plan_shared=no\r\nborders_count=0\r\n";

	private static CBattlePlan Read(string text) => CBattlePlanFile.Read(ClausewitzParser.Parse(text));

	private static CPlanEditor Editor(CBattlePlan plan)
	{
		var settings = new CBattlePlanSettings();
		settings.Read(ClausewitzParser.Parse("tool_color = { icon = c option = { name = RED red = 0.84 green = 0.15 blue = 0.15 } option = { name = GREEN red = 0.15 green = 0.84 blue = 0.15 } }\n"
			+ "tool_arrow = { icon = a option = { name = SLIM type = ba_0 } option = { name = THICK type = ba_2 } }\n"
			+ "tool_borders = { icon = b option = { name = ONE type = bb_0 } }\ntool_icons = { icon = i option = { name = TANK type = mi_1 } }"));
		var types = new CPlanObjectTypes();
		types.Read(ClausewitzParser.Parse("objectTypes = { battlearrow = { name = ba_0 start = 1 stop = 0.5 x = 5 y = 2 } mapinfo = { name = mi_1 textureFile = t scale = 8 } }"));
		return new CPlanEditor(settings, types) { Plan = plan };
	}

	[Fact]
	public void AFileComesBackKeyForKey()
	{
		Assert.Equal(Sample, CBattlePlanFile.Write(Read(Sample)));
	}

	[Fact]
	public void TheFileIsReadByPosition()
	{
		CBattlePlan plan = Read(Sample);
		Assert.Equal("Fall Gelb", plan.Title);
		CPlanStroke arrow = Assert.Single(plan.Arrows);
		Assert.Equal(new[] { (0f, 0f), (5f, 0f), (10f, 2.5f) }, arrow.Points);
		Assert.Equal("7. Panzer", arrow.Label);
		Assert.Equal(2, arrow.Colour);
		Assert.Equal(3281.167f, arrow.OriginX);
		Assert.Equal("Maas", Assert.Single(plan.Texts).Text);
		Assert.Equal(-2.85f, Assert.Single(plan.Icons).Rotation);
	}

	[Fact]
	public void ASavedGamesPlanHoldsWhetherItIsShown()
	{
		CBattlePlan plan = Read(Sample);
		plan.IsVisible = false;
		string written = CBattlePlanFile.Write(plan, visibility: true);
		Assert.Contains("visibility=no", written);
		Assert.False(Read(written).IsVisible);
		Assert.DoesNotContain("visibility", CBattlePlanFile.Write(plan));
	}

	[Fact]
	public void AFileNameIsThePartBeforeTheFirstPoint()
	{
		Assert.Equal("Fall", CBattlePlanText.FileNameShown("a/b/Fall.old.pln"));
	}

	[Theory]
	[InlineData("plan", true)]
	[InlineData("a/b", false)]
	[InlineData("a\\b", false)]
	[InlineData("caf\u00e9", false)]
	public void AFileNameMayNotHoldASlashOrAHighByte(string name, bool legal)
	{
		Assert.Equal(legal, CBattlePlanText.IsLegalFileName(name));
	}

	[Theory]
	[InlineData("plan", "plan.pln")]
	[InlineData("plan.pln", "plan.pln")]
	[InlineData("plan.PLN", "plan.PLN.pln")]
	[InlineData(".pln", ".pln.pln")]
	public void TheExtensionIsAddedUnlessThere(string name, string file)
	{
		Assert.Equal(file, CBattlePlanText.WithExtension(name));
	}

	[Fact]
	public void TheLastPointFollowsAndNewOnesComeFiveApart()
	{
		var points = new System.Collections.Generic.List<(float X, float Y)>();
		CArrowMesh.AddPoint(points, 0, 0);
		Assert.Equal(2, points.Count);
		CArrowMesh.AddPoint(points, 3, 0);
		Assert.Equal(2, points.Count);
		Assert.Equal((3f, 0f), points[1]);
		CArrowMesh.AddPoint(points, 6, 0);
		Assert.Equal(3, points.Count);
	}

	[Fact]
	public void SmoothingLeavesTheEndsAndMovesTheMiddle()
	{
		var points = new System.Collections.Generic.List<(float X, float Y)> { (0, 0), (10, 8), (20, 0) };
		CArrowMesh.Smooth(points, -1);
		Assert.Equal((0f, 0f), points[0]);
		Assert.Equal((20f, 0f), points[2]);
		Assert.Equal((10f, 2f), points[1]);
	}

	[Fact]
	public void TheCurveHasFourSamplesASegmentAndPassesThroughThePoints()
	{
		var points = new[] { (0f, 0f), (10f, 0f), (20f, 0f) };
		var samples = CArrowMesh.Samples(points);
		Assert.Equal(9, samples.Count);
		Assert.Equal((0f, 0f), samples[0]);
		Assert.Equal((10f, 0f), samples[4]);
		Assert.Equal((20f, 0f), samples[8]);
	}

	[Fact]
	public void TwoPointsOnOneSpotMakeNoCurve()
	{
		Assert.Empty(CArrowMesh.Samples(new[] { (1f, 1f), (1f, 1f) }));
	}

	[Fact]
	public void TheStripIsTwoVerticesASampleAndFourForTheHead()
	{
		var stroke = new CPlanStroke();
		stroke.Points.AddRange(new[] { (0f, 0f), (10f, 0f), (20f, 0f) });
		var type = new CBattleArrowType("ba", "", "", 2f, 1f, 5f, 3f, "", 1f, 0f, false);
		(var vertices, int body) = CArrowMesh.Build(stroke, type);
		Assert.Equal(18, body);
		Assert.Equal(22, vertices.Count);
		Assert.Equal(2f, System.Math.Abs(vertices[0].Y));
		Assert.Equal(1f, System.Math.Abs(vertices[16].Y));
		Assert.Equal(25f, vertices[20].X);
	}

	[Fact]
	public void AStrokeDrawnIsKeptSmoothedAndSelected()
	{
		var plan = new CBattlePlan { IsLive = true };
		CPlanEditor editor = Editor(plan);
		editor.PressTool(EPlanTool.Arrows);
		editor.Press(100, 100, false);
		editor.Move(110, 100);
		editor.Release(120, 100);
		CPlanStroke arrow = Assert.Single(plan.Arrows);
		Assert.Equal("ba_0", arrow.Type);
		Assert.True(arrow.IsSelected);
		Assert.Null(editor.Drawing);
	}

	[Fact]
	public void AClickSelectsTheNearestArrowByItsPoints()
	{
		var plan = new CBattlePlan { IsLive = true };
		var near = new CPlanStroke();
		near.Points.AddRange(new[] { (0f, 0f), (10f, 0f) });
		var far = new CPlanStroke();
		far.Points.AddRange(new[] { (5f, 3f) });
		plan.Arrows.Add(far);
		plan.Arrows.Add(near);
		CPlanEditor editor = Editor(plan);
		editor.Press(10, 1, false);
		editor.Release(10, 1);
		Assert.True(near.IsSelected);
		Assert.False(far.IsSelected);
	}

	[Fact]
	public void TheEraseToolDeletesAndLeavesTheToolAsItWas()
	{
		var plan = new CBattlePlan { IsLive = true };
		plan.Arrows.Add(new CPlanStroke { IsSelected = true });
		plan.Icons.Add(new CPlanIcon());
		CPlanEditor editor = Editor(plan);
		editor.PressTool(EPlanTool.Arrows);
		editor.PressTool(EPlanTool.Erase);
		Assert.Empty(plan.Arrows);
		Assert.Single(plan.Icons);
		Assert.Equal(EPlanTool.Arrows, editor.Tool);
	}

	[Fact]
	public void AColourRecoloursWhatIsSelectedButATextSign()
	{
		var plan = new CBattlePlan { IsLive = true };
		var arrow = new CPlanStroke { IsSelected = true };
		plan.Arrows.Add(arrow);
		CPlanEditor editor = Editor(plan);
		editor.PressTool(EPlanTool.Colours);
		editor.ChooseOption(1);
		Assert.Equal(1, arrow.Colour);
	}

	[Fact]
	public void PlainSelectLeavesTheClickToTheMapAndATakeNeedsAModifier()
	{
		CPlanEditor editor = Editor(new CBattlePlan { IsLive = true });
		Assert.False(editor.TakesLeftPress(false, false));
		Assert.True(editor.TakesLeftPress(true, false));
		editor.PressTool(EPlanTool.Icons);
		Assert.True(editor.TakesLeftPress(false, false));
	}

	[Fact]
	public void TheSizeStaysBetweenAQuarterAndTwenty()
	{
		CPlanEditor editor = Editor(new CBattlePlan { IsLive = true });
		editor.PressTool(EPlanTool.Text);
		for (int step = 0; step < 40; step++)
		{
			editor.Wheel(1, true, false);
		}
		Assert.Equal(20f, editor.Size);
		for (int step = 0; step < 40; step++)
		{
			editor.Wheel(-1, true, false);
		}
		Assert.Equal(0.25f, editor.Size);
	}

	[Fact]
	public void TheTextToolOffersNewTextWithNothingSelected()
	{
		CPlanEditor editor = Editor(new CBattlePlan { IsLive = true });
		Assert.Equal(CPlanEditor.NewText, editor.PressTool(EPlanTool.Text));
		Assert.True(editor.PlacingText);
	}

	[Fact]
	public void TextIsPlacedWhereTheMouseIs()
	{
		var plan = new CBattlePlan { IsLive = true };
		CPlanEditor editor = Editor(plan);
		editor.PressTool(EPlanTool.Text);
		editor.Move(50, 60);
		editor.Press(50, 60, false);
		CPlanText sign = Assert.Single(plan.Texts);
		Assert.Equal(50f, sign.OriginX);
		Assert.Equal(CPlanEditor.NewText, sign.Text);
	}

	[Fact]
	public void ApplyingNothingToASelectedSignDeletesIt()
	{
		var plan = new CBattlePlan { IsLive = true };
		plan.Texts.Add(new CPlanText { Text = "x", IsSelected = true });
		CPlanEditor editor = Editor(plan);
		editor.Apply("");
		Assert.Empty(plan.Texts);
	}

	[Fact]
	public void APaletteIndexPastTheEndIsMagenta()
	{
		CPlanEditor editor = Editor(new CBattlePlan { IsLive = true });
		Assert.Equal((1f, 0f, 1f), editor.Colour(9));
	}
}
