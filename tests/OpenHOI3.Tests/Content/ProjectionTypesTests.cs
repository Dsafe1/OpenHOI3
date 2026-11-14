using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The <c>projectionType</c> declarations, against <c>docs/mechanics/movement.md</c>, section 7. The file is made up, in
/// the shape an install's has.
/// </summary>
public class ProjectionTypesTests : IDisposable
{
	private readonly string _root;
	private readonly List<string> _warnings = new();

	public ProjectionTypesTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-projections-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "interface"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	[Fact]
	public void AProjectionTypeIsReadWithTheOriginalsDefaultsForWhatItLeavesOut()
	{
		File.WriteAllText(Path.Combine(_root, "interface", "mapitems.gfx"), @"
			objectTypes = {
				projectionType = {
					name = ""legal_selection_projection""
					textureFile = ""gfx\\test\\legal.tga""
					size = 4
					spin = 0
					pulsating = yes
					pulseLowest = 0.8
					pulseSpeed = 1
					additative = no
					expanding = 0.3

					duration = 1
					fadeout = 1
				}
				projectionType = { name = ""bare"" glow = yes }
				projectionType = { size = 2 }
				billboardType = { name = ""capital"" scale = 2.5 }
			}");

		CProjectionTypes types = CProjectionTypes.Load(CDirectorySettings.At(_root), _warnings.Add);

		Assert.Equal(2, types.Count);
		CProjectionType legal = types.Find(CProjectionTypes.Legal)!;
		Assert.Equal(@"gfx\test\legal.tga", legal.TextureFile);
		Assert.Equal(4f, legal.Size);
		Assert.True(legal.Pulsating);
		Assert.Equal(0.8f, legal.PulseLowest);
		Assert.Equal(1f, legal.PulseSpeed);
		Assert.False(legal.Additive);
		Assert.Equal(0.3f, legal.Expanding);
		Assert.Equal(1f, legal.Duration);
		Assert.Equal(1f, legal.Fadeout);
		// What a declaration leaves out is the original's default: size 1, pulseLowest 0.4, pulseSpeed 3, the rest nought.
		CProjectionType bare = types.Find("bare")!;
		Assert.Equal(1f, bare.Size);
		Assert.Equal(0.4f, bare.PulseLowest);
		Assert.Equal(3f, bare.PulseSpeed);
		Assert.Equal(0f, bare.Expanding + bare.Duration + bare.Fadeout + bare.Spin);
		Assert.False(bare.Pulsating || bare.Additive);
		Assert.Null(types.Find("capital"));
		// A key the original drops in silence is reported here.
		Assert.Contains(_warnings, warning => warning.Contains("'bare' has a key nothing reads: glow"));
		Assert.Contains(_warnings, warning => warning.Contains("a projectionType has no name"));
	}
}
