using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The <c>CounterType</c> declarations, against <c>docs/mechanics/mapunits.md</c>,
/// section 2. The file is made up, in the shape an install's has.
/// </summary>
public class CounterTypesTests : IDisposable
{
	private readonly string _root;
	private readonly List<string> _warnings = new();

	public CounterTypesTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-counters-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "interface"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private void Write(string file, string content)
	{
		File.WriteAllText(Path.Combine(_root, "interface", file), content);
	}

	private CCounterTypes Load() => CCounterTypes.Load(CDirectorySettings.At(_root), _warnings.Add);

	[Fact]
	public void ACounterTypeIsReadFromAnObjectTypesBlockWithItsListsOfNumbers()
	{
		Write("mapitems.gfx", @"
			objectTypes = {
				billboardType = { name = ""capital"" scale = 2.5 }
				CounterType = {
					name = ""unit_counter_close""
					effectFile = ""gfx\\FX\\counter.fx""
					textureFile = ""gfx\\mapitems\\plate.tga""
					textureFile2 = ""gfx\\mapitems\\mask.tga""
					textureFile3 = ""gfx\\mapitems\\sizes.tga""
					textureFile4 = ""gfx\\mapitems\\arrows.dds""
					noOfFrames = 6 # frames in army size
					scale = 2.5
					size = 4.5
					font = ""Arial_17_2""
					shieldtype = { 0.45 0.3 0.0 }
					offset = { 2.5 0.0 -5.0 }
					offset2 = { -7.0 0.0 -6.4}
					offset3 = { 5.8 0.0 5.5 }
					color1 = { 0.0 1.0 0.0 1.0}
					color2 = { 0.28 0.8 0.82 1.0}
					delayAfterAttackColor = { 1.0 1.0 0.0 1.0}
				}
				CounterType = { name = ""unit_counter"" scale = 3 level_scale = 0.20 noOfFrames = 6 }
			}");

		CCounterTypes types = Load();

		Assert.Equal(2, types.Count);
		CCounterType near = types.Find(CCounterTypes.Near)!;
		Assert.Equal(@"gfx\mapitems\plate.tga", near.BackgroundFile);
		Assert.Equal(@"gfx\mapitems\mask.tga", near.MaskFile);
		Assert.Equal(@"gfx\mapitems\sizes.tga", near.SizeFile);
		Assert.Equal(6, near.Frames);
		Assert.Equal(2.5f, near.Scale);
		Assert.Equal(0f, near.LevelScale);
		Assert.Equal(4.5f, near.TextSize);
		Assert.Equal("Arial_17_2", near.Font);
		Assert.True(near.HasFlag);
		Assert.Equal(0.45f, near.FlagWidth);
		Assert.Equal(0.3f, near.FlagHeight);
		Assert.Equal(new Vector3(2.5f, 0f, -5f), near.FlagOffset);
		Assert.Equal(new Vector3(-7f, 0f, -6.4f), near.FiguresOffset);
		Assert.Equal(new Vector3(5.8f, 0f, 5.5f), near.NameOffset);
		Assert.Equal(new CColor(0f, 1f, 0f, 1f), near.Color1);
		Assert.Equal(new CColor(0.28f, 0.8f, 0.82f, 1f), near.Color2);

		CCounterType far = types.Find(CCounterTypes.Far)!;
		Assert.Equal(3f, far.Scale);
		Assert.Equal(0.2f, far.LevelScale);
		Assert.False(far.HasFlag);
		Assert.Null(types.Find("capital"));                    // a billboard is not a counter
		Assert.Empty(_warnings);
	}

	[Fact]
	public void ALaterFileReplacesACounterTypeAndAKeyNothingReadsIsReported()
	{
		Write("a.gfx", @"objectTypes = { CounterType = { name = ""unit_counter"" scale = 3 } }");
		Write("b.gfx", @"objectTypes = {
			CounterType = { name = ""unit_counter"" scale = 4 sparkle = yes }
			CounterType = { scale = 9 }
		}");

		CCounterTypes types = Load();

		Assert.Equal(1, types.Count);
		Assert.Equal(4f, types.Find("UNIT_COUNTER")!.Scale);
		Assert.Contains(_warnings, warning => warning.Contains("'unit_counter' has a key nothing reads: sparkle"));
		Assert.Contains(_warnings, warning => warning.Contains("has no name"));
	}

	[Fact]
	public void AnArrowTypeIsReadFromTheSameBlocks()
	{
		Write("arrows.gfx", @"
			objectTypes = {
				arrowType = {
					name = ""moveArrow""
					size = 5.0
					textureFile = ""gfx\\mapitems\\movearrow.tga""
					bodytexture = ""gfx\\mapitems\\movearrow.tga""
					color = { 0.0 1.0 0.0 }
					colortwo = { 1.0 0.0 0.0 }
					endAt = 1.0f
					height = 8.0
					type = 0
					heading = 1.0f
					effect = ""gfx\\fx\\arrow.fx""
				}
				arrowType = { name = ""odd"" wobble = yes }
				arrowType = { size = 2 }
			}");

		CArrowTypes types = CArrowTypes.Load(CDirectorySettings.At(_root), _warnings.Add);

		Assert.Equal(2, types.Count);
		CArrowType move = types.Find(CArrowTypes.Move)!;
		Assert.Equal(@"gfx\mapitems\movearrow.tga", move.TextureFile);
		Assert.Equal(@"gfx\mapitems\movearrow.tga", move.BodyTextureFile);
		Assert.Equal(5f, move.Size);
		Assert.Contains(_warnings, warning => warning.Contains("'odd' has a key nothing reads: wobble"));
		Assert.Contains(_warnings, warning => warning.Contains("an arrowType has no name"));
	}

	[Fact]
	public void ABillboardTypeIsReadFromTheSameBlocks()
	{
		Write("mapitems.gfx", @"
			objectTypes = {
				CounterType = { name = ""unit_counter"" scale = 3 }
				billboardType = {
					name = ""counter_arrow""
					textureFile = ""gfx\\mapitems\\counter_arrows.dds""
					scale = 1.0
					offset = 4.5
					noOfFrames = 2
				}
				billboardType = { name = ""combat_status"" scale = 2.6 noOfFrames = 3 font_size = 7 offset2 = { 0.0 0.0 -2.5 } }
				billboardType = { name = ""odd"" glow = yes }
				billboardType = { scale = 2 }
			}");

		CBillboardTypes types = CBillboardTypes.Load(CDirectorySettings.At(_root), _warnings.Add);

		Assert.Equal(3, types.Count);
		CBillboardType arrow = types.Find(CBillboardTypes.CounterArrow)!;
		Assert.Equal(@"gfx\mapitems\counter_arrows.dds", arrow.TextureFile);
		Assert.Equal(1f, arrow.Scale);
		Assert.Equal(4.5f, arrow.Offset);
		Assert.Equal(2, arrow.Frames);
		Assert.Equal(0f, types.Find("combat_status")!.Offset);
		Assert.Null(types.Find("unit_counter"));                       // a counter is not a billboard
		Assert.Contains(_warnings, warning => warning.Contains("'odd' has a key nothing reads: glow"));
		Assert.Contains(_warnings, warning => warning.Contains("a billboardType has no name"));
	}
}
