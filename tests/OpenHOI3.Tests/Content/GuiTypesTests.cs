using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Gui;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Reading interface files, against <c>docs/mechanics/interface.md</c>. The fragments
/// are written here in the files' own syntax; none is lifted from an install.
/// </summary>
public class GuiTypesTests
{
	private static CGuiTypes Read(string text, List<string>? warnings = null)
	{
		warnings ??= new List<string>();
		return CGuiTypes.Parse(text, warnings.Add);
	}

	private const string Bar = """
		guiTypes = {
			windowType = {
				name = "bar"
				backGround = ""
				position = { x=0 y=0 }
				size = { x=2048 y=100 }
				moveable = 0
				dontRender = ""
				fullScreen = yes

				iconType = {
					name ="bar_bg"
					spriteType = "GFX_bar_bg"
					position = { x= 0 y = -1 }
					Orientation = "UPPER_LEFT"
				}
				textBoxType = {
					name = "energy_number"
					position = { x = 121 y = 1 }
					textureFile = ""
					font = "Arial12"
					borderSize = {x = 0 y = 4}
					text = "1000"
					maxWidth = 45
					maxHeight = 32
					format = centre
				}
				guiButtonType = {
					name = "bar_outlinerbutton"
					quadTextureSprite ="GFX_button_outliner"
					position = { x = -25 y = 58 }
					Orientation = "UPPER_RIGHT"
					shortcut = "F1"
				}
				listboxType = {
					name = "a_list"
					position = { x = 4 y = 5 }
					size = { x = 100 y = 200 }
				}
			}
			positionType = {
				name = "list_start"
				position = { x = 0 y = 144 }
			}
		}
		""";

	[Fact]
	public void ReadsAWindowWithItsChildrenInTheOrderTheyAreDeclared()
	{
		var warnings = new List<string>();
		CGuiWindowType bar = Read(Bar, warnings).FindWindow("bar")!;

		Assert.Empty(warnings);
		Assert.True(bar.FullScreen);
		Assert.False(bar.Moveable);
		Assert.Equal(new CGuiPoint(2048, 100), bar.Size);
		Assert.Equal(new[] { "bar_bg", "energy_number", "bar_outlinerbutton", "a_list" },
			bar.Children.Select(child => child.Name));
	}

	[Fact]
	public void ReadsWhatEachKindOfElementIsGiven()
	{
		CGuiWindowType bar = Read(Bar).FindWindow("bar")!;

		CGuiIconType icon = bar.Find<CGuiIconType>("bar_bg")!;
		Assert.Equal("GFX_bar_bg", icon.Sprite);
		Assert.Equal(new CGuiPoint(0, -1), icon.Position);
		Assert.Equal(1f, icon.Scale);

		CGuiTextBoxType box = bar.Find<CGuiTextBoxType>("energy_number")!;
		Assert.Equal("Arial12", box.Font);
		Assert.Equal("1000", box.Text);
		Assert.Equal(45, box.MaxWidth);
		Assert.Equal(new CGuiPoint(0, 4), box.BorderSize);
		Assert.Equal(EGuiTextFormat.Centre, box.Format);
		Assert.False(box.IsInstant);

		CGuiButtonType button = bar.Find<CGuiButtonType>("bar_outlinerbutton")!;
		Assert.Equal("GFX_button_outliner", button.Sprite);
		Assert.Equal(EGuiOrientation.UpperRight, button.Orientation);
		Assert.Equal(new CGuiPoint(-25, 58), button.Position);
		Assert.Equal("F1", button.Shortcut);
	}

	[Fact]
	public void AListBoxInAWindowKeepsItsPlaceInTheOrder()
	{
		CGuiWindowType bar = Read(Bar).FindWindow("bar")!;

		CGuiListBoxType list = bar.Find<CGuiListBoxType>("a_list")!;
		Assert.Equal("listboxType", list.Keyword);
		Assert.Equal(new CGuiPoint(100, 200), list.Size);
	}

	[Fact]
	public void AnEditBoxIsABoxOfTextWithItsSizeForRoom()
	{
		CGuiWindowType screen = Read("""
			guiTypes = {
				windowType = {
					name = "screen"
					iconType = { name = "before" spriteType = "GFX_a" }
					editBoxType = { name = "typing" position = { x = 4 y = 8 } size = { x = 100 y = 20 } font = "Arial12" borderSize = { x = 8 y = 4 } }
					iconType = { name = "after" spriteType = "GFX_b" }
				}
			}
			""").FindWindow("screen")!;

		CGuiTextBoxType box = screen.Find<CGuiTextBoxType>("typing")!;
		Assert.Equal("editBoxType", box.Keyword);
		Assert.True(box.IsEdit);
		Assert.False(box.IsInstant);
		Assert.Equal(100, box.MaxWidth);
		Assert.Equal(20, box.MaxHeight);
		Assert.Equal(new CGuiPoint(8, 4), box.BorderSize);
		Assert.Equal(EGuiTextFormat.Left, box.Format);
		Assert.Equal("Arial12", box.Font);
		Assert.Equal(new[] { "before", "typing", "after" }, screen.Children.Select(child => child.Name));
	}

	[Fact]
	public void FindsByNameWhateverTheCase()
	{
		CGuiTypes types = Read(Bar);

		Assert.NotNull(types.FindWindow("BAR"));
		Assert.Equal(new CGuiPoint(0, 144), types.FindPosition("LIST_START")!.Position);
		Assert.NotNull(types.FindWindow("bar")!.Find<CGuiTextBoxType>("Energy_Number"));
		Assert.Null(types.FindWindow("list_start"));                 // it is a position, not a window
	}

	[Fact]
	public void TheLaterOfTwoOfOneNameIsKeptAndTheReplacingIsReported()
	{
		var warnings = new List<string>();
		CGuiTypes types = Read("""
			guiTypes = {
				positionType = { name = "spot" position = { x = 1 y = 1 } }
				positionType = { name = "SPOT" position = { x = 2 y = 2 } }
			}
			""", warnings);

		Assert.Equal(1, types.Count);
		Assert.Equal(new CGuiPoint(2, 2), types.FindPosition("spot")!.Position);
		Assert.Contains(warnings, warning => warning.Contains("more than once"));
	}

	[Fact]
	public void AKeyGivenTwiceLeavesWhatTheSecondSaidAndIsReported()
	{
		// brigade_entry_3's button is written this way and is drawn at the later place.
		var warnings = new List<string>();
		CGuiWindowType line = Read("""
			guiTypes = {
				windowType = {
					name = "line"
					guiButtonType = {
						name = "select"
						position = { x = 0 y = 4 }
						quadTextureSprite = "GFX_listbutton"
						position = { x = 3 y = 0 }
					}
					iconType = { name = "a" spriteType = "x" position = { x = 1 y = 1 } }
					iconType = { name = "b" spriteType = "x" position = { x = 2 y = 2 } }
				}
			}
			""", warnings).FindWindow("line")!;

		Assert.Equal(new CGuiPoint(3, 0), line.Find<CGuiButtonType>("select")!.Position);
		Assert.Equal("GFX_listbutton", line.Find<CGuiButtonType>("select")!.Sprite);
		// Elements of one kind are each their own and are all kept.
		Assert.Equal(new CGuiPoint(1, 1), line.Find<CGuiIconType>("a")!.Position);
		Assert.Equal(new CGuiPoint(2, 2), line.Find<CGuiIconType>("b")!.Position);
		Assert.Equal(3, line.Children.Count);
		string warning = Assert.Single(warnings);
		Assert.Contains("guiButtonType gives 'position' more than once", warning);
	}

	[Fact]
	public void AKeyAnElementDoesNotTakeIsReportedOnceHoweverOftenItComesUp()
	{
		var warnings = new List<string>();
		Read("""
			guiTypes = {
				windowType = {
					name = "w"
					iconType = { name = "a" spriteType = "x" colour = red }
					iconType = { name = "b" spriteType = "x" colour = blue }
				}
			}
			""", warnings);

		string warning = Assert.Single(warnings);
		Assert.Contains("iconType does not take 'colour'", warning);
		Assert.Contains("1 more", warning);
	}

	[Fact]
	public void TheFiveKeysEveryElementTakesAreNotReported()
	{
		var warnings = new List<string>();
		Read("""
			guiTypes = {
				iconType = { name = "a" spriteType = "x" dontRender = "" pdx_tooltip = "T" pdx_tooltip_delayed = "D" scope = s }
			}
			""", warnings);

		Assert.Empty(warnings);
	}

	[Fact]
	public void AFileThatDoesNotOpenWithGuiTypesLoadsNothingAndSaysSo()
	{
		// The original reads the one key and drops the rest without a word.
		var warnings = new List<string>();
		CGuiTypes types = Read("""
			windowType = { name = "lost" }
			""", warnings);

		Assert.Equal(0, types.Count);
		Assert.Contains(warnings, warning => warning.Contains("'windowType' is not guiTypes"));
	}

	[Fact]
	public void AnElementKeywordNothingKnowsIsReported()
	{
		var warnings = new List<string>();
		CGuiTypes types = Read("""
			guiTypes = { gaugeType = { name = "g" } }
			""", warnings);

		Assert.Equal(0, types.Count);
		Assert.Contains(warnings, warning => warning.Contains("'gaugeType' is not a kind of element"));
	}

	[Theory]
	[InlineData("UPPER_LEFT", EGuiOrientation.UpperLeft)]
	[InlineData("UPPER_RIGHT", EGuiOrientation.UpperRight)]
	[InlineData("LOWER_LEFT", EGuiOrientation.LowerLeft)]
	[InlineData("LOWER_RIGHT", EGuiOrientation.LowerRight)]
	[InlineData("CENTER", EGuiOrientation.Center)]
	[InlineData("CENTER_UP", EGuiOrientation.CenterUp)]
	[InlineData("CENTER_DOWN", EGuiOrientation.CenterDown)]
	public void ReadsTheSevenOrientations(string word, EGuiOrientation expected)
	{
		var warnings = new List<string>();
		CGuiTypes types = Read($$"""
			guiTypes = { iconType = { name = "a" spriteType = "x" orientation = {{word}} } }
			""", warnings);

		Assert.Equal(expected, types.Find("a")!.Orientation);
		Assert.Empty(warnings);
	}

	[Theory]
	[InlineData("center")]                                           // the compare is exact
	[InlineData("CENTRE")]
	[InlineData("TOP_LEFT")]
	public void AnyOtherOrientationIsTheUpperLeftAsInTheOriginalAndIsReported(string word)
	{
		var warnings = new List<string>();
		CGuiTypes types = Read($$"""
			guiTypes = { iconType = { name = "a" spriteType = "x" orientation = "{{word}}" } }
			""", warnings);

		Assert.Equal(EGuiOrientation.UpperLeft, types.Find("a")!.Orientation);
		Assert.Contains(warnings, warning => warning.Contains("is not one of the seven"));
	}

	[Theory]
	[InlineData("left", EGuiTextFormat.Left)]
	[InlineData("centre", EGuiTextFormat.Centre)]
	[InlineData("center", EGuiTextFormat.Centre)]
	[InlineData("right", EGuiTextFormat.Right)]
	public void ReadsTheFormatsTheFilesUse(string word, EGuiTextFormat expected)
	{
		CGuiTypes types = Read($$"""
			guiTypes = { instantTextBoxType = { name = "t" font = "f" format = {{word}} } }
			""");

		var box = (CGuiTextBoxType)types.Find("t")!;
		Assert.Equal(expected, box.Format);
		Assert.True(box.IsInstant);
	}

	[Fact]
	public void ADialogIsAWindowThatAlsoTakesShields()
	{
		var warnings = new List<string>();
		CGuiTypes types = Read("""
			guiTypes = {
				eu3dialogtype = {
					name = "dialog"
					backGround = "Background"
					size = { x = 400 y = 300 }
					orientation = "CENTER"
					guiButtonType = { name = "Background" quadTextureSprite = "GFX_tiles" }
					shieldtype = { name = "LeftShield" spriteType = "GFX_thinshield" rotation = -1.5708 }
				}
				windowType = {
					name = "plain"
					shieldtype = { name = "LeftShield" spriteType = "GFX_thinshield" }
				}
			}
			""", warnings);

		CGuiWindowType dialog = types.FindWindow("dialog")!;
		Assert.True(dialog.IsDialog);
		Assert.Equal("Background", dialog.Background);
		Assert.Equal(2, dialog.Children.Count);
		Assert.Empty(types.FindWindow("plain")!.Children);

		string warning = Assert.Single(warnings);
		Assert.Contains("windowType does not take 'shieldtype'", warning);
	}

	[Fact]
	public void AWindowInsideAWindowIsReadTheSameWay()
	{
		CGuiTypes types = Read("""
			guiTypes = {
				windowType = {
					name = "outer"
					windowType = {
						name = "inner"
						size = { x = 10 y = 20 }
						checkboxType = { name = "tick" quadTextureSprite = "GFX_checkbox_default" }
					}
				}
			}
			""");

		CGuiWindowType inner = types.FindWindow("outer")!.Find<CGuiWindowType>("inner")!;
		Assert.Equal(new CGuiPoint(10, 20), inner.Size);
		Assert.True(inner.Find<CGuiButtonType>("tick")!.IsCheckbox);
		Assert.Null(types.FindWindow("inner"));                      // only what is at the top level is found there
	}

	[Fact]
	public void AScrollbarKeepsItsRangeAndTheFourButtonsItNames()
	{
		CGuiTypes types = Read("""
			guiTypes = {
				scrollbarType = {
					name = "a_slider"
					slider = "knob"
					track = "rail"
					leftbutton = "less"
					rightbutton = "more"
					size = { x = 180 y = 16 }
					borderSize = { x = 16 y = 16 }
					minValue = 0
					maxValue = 100
					startValue = 25
					stepSize = 0.001
					horizontal = 1
					guiButtonType = { name = "knob" quadTextureSprite = "a_knob" }
					guiButtonType = { name = "rail" quadTextureSprite = "a_rail" position = { x = 0 y = 20 } }
					guiButtonType = { name = "less" quadTextureSprite = "a_left" }
					guiButtonType = { name = "more" quadTextureSprite = "a_right" }
				}
			}
			""");

		CGuiScrollbarType slider = types.FindScrollbar("a_slider")!;
		Assert.True(slider.Horizontal);
		Assert.Equal(new CGuiPoint(180, 16), slider.Size);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.FromInt(100), slider.MaxValue);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.FromInt(25), slider.StartValue);
		Assert.Equal(1, slider.StepSize.Raw);                        // a thousandth, the smallest there is
		Assert.Equal(4, slider.Children.Count);
		Assert.Equal("a_knob", slider.FindButton(slider.Slider)!.Sprite);
		Assert.Equal("a_rail", slider.FindButton(slider.Track)!.Sprite);
		Assert.Equal("a_left", slider.FindButton(slider.LeftButton)!.Sprite);
		Assert.Equal("a_right", slider.FindButton(slider.RightButton)!.Sprite);
		Assert.Null(slider.FindButton("nobody"));
	}

	[Fact]
	public void AListBoxAndABoxOfOverlappingElementsKeepWhatLaysTheirContentsOut()
	{
		var warnings = new List<string>();
		CGuiTypes types = Read("""
			guiTypes = {
				windowType = {
					name = "screen"
					listboxType = {
						name = "queue"
						position = { x = 50 y = 332 }
						size = { x = 230 y = 248 }
						borderSize = { x = 0 y = 0 }
						spacing = 2
						scrollbartype = "standardlistbox_slider"
						Orientation = "UPPER_LEFT"
					}
					OverlappingElementsBoxType = {
						name = "row"
						position = { x = 580 y = 30 }
						size = { x = 300 y = 32 }
						format = right
						spacing = 7.0
					}
				}
			}
			""", warnings);

		CGuiWindowType screen = types.FindWindow("screen")!;
		CGuiListBoxType queue = screen.Find<CGuiListBoxType>("queue")!;
		Assert.Equal(new CGuiPoint(230, 248), queue.Size);
		Assert.Equal(2, queue.Spacing);
		Assert.Equal("standardlistbox_slider", queue.Scrollbar);

		CGuiOverlappingBoxType row = screen.Find<CGuiOverlappingBoxType>("row")!;
		Assert.Equal(new CGuiPoint(300, 32), row.Size);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.FromInt(7), row.Spacing);
		Assert.Equal(EGuiTextFormat.Right, row.Format);
		Assert.Empty(warnings);
	}
}
