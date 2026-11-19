using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Sound;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// Declarations the client reports faults in keep the file and the line they were read at, so that its reports say
/// <c>file:line:</c> (<c>docs/mechanics/logs.md</c>, section 4). The fragments are made up.
/// </summary>
public class DeclarationLineTests : IDisposable
{
	private readonly string _root;

	public DeclarationLineTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-lines-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "interface"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	[Fact]
	public void AnInterfaceElementKeepsItsFileAndTheLineItsBlockBeginsOn()
	{
		CGuiTypes types = CGuiTypes.Parse("guiTypes = {\n\twindowType = {\n\t\tname = \"w\"\n\t\ticonType = {\n\t\t\tname = \"i\"\n\t\t}\n\t}\n}");

		CGuiWindowType window = types.FindWindow("w")!;
		Assert.Equal(2, window.Line);
		Assert.Equal("text", window.File);
		CGuiType icon = window.Children.Single();
		Assert.Equal(4, icon.Line);
		Assert.Equal("text", icon.File);
		Assert.Equal("text:4: a fault", CDiagnostics.Located(icon.File, icon.Line, "a fault"));
	}

	[Fact]
	public void ACounterTypeAndABillboardTypeKeepTheirFileAndLine()
	{
		string file = Path.Combine(_root, "interface", "mapitems.gfx");
		File.WriteAllText(file, "objectTypes = {\n\tCounterType = { name = \"unit_counter\" scale = 3 }\n\n\tbillboardType = { name = \"counter_arrow\" scale = 1.0 }\n}");
		CDirectorySettings directories = CDirectorySettings.At(_root);

		CCounterType counter = CCounterTypes.Load(directories, _ => { }).Find(CCounterTypes.Far)!;
		CBillboardType arrow = CBillboardTypes.Load(directories, _ => { }).Find(CBillboardTypes.CounterArrow)!;

		Assert.Equal(2, counter.Line);
		Assert.Equal(4, arrow.Line);
		Assert.Equal(Path.GetFileName(file), Path.GetFileName(counter.File));
		Assert.Equal(Path.GetFileName(file), Path.GetFileName(arrow.File));
	}

	[Fact]
	public void ASoundEffectKeepsItsLine()
	{
		var sounds = CSoundEffectReader.Read(ClausewitzParser.Parse("click = { file = \"click.wav\" }\n\nbeep = {\n\tfile = \"beep.wav\"\n}"), _ => { });

		Assert.Equal(1, sounds["click"].Line);
		Assert.Equal(3, sounds["beep"].Line);
	}
}
