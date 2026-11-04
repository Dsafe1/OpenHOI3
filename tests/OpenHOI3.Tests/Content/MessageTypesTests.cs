using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Messages;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The message types with the player's file over the install's, as <c>docs/mechanics/ingamemenu.md</c>, section 5, has
/// them: the order of first declaration, a type overwritten whole in its place, the file the original writes, and Reset.
/// </summary>
public class MessageTypesTests : IDisposable
{
	private readonly string _root;

	public MessageTypesTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "interface"));
		File.WriteAllText(Path.Combine(_root, "interface", "messagetypes.txt"),
			"ATTACKUS = { log = yes popup = yes categories = \"cat_combat\" }\n"
			+ "DIPLOEXPIRE = { log = yes onmap = yes option = yes categories = \"cat_diplomacy\" }\n");
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
		GC.SuppressFinalize(this);
	}

	private string Custom => Path.Combine(_root, CMessageTypes.CustomFile);

	private CMessageTypes Load()
	{
		return CMessageTypes.Load(CDirectorySettings.At(_root), Custom, _ => { });
	}

	[Fact]
	public void The_players_file_overwrites_a_type_whole_in_its_place_and_adds_a_new_one_last()
	{
		File.WriteAllText(Custom, "MYOWN = { log = no popup = yes }\nATTACKUS = { onmap = yes }\n");

		CMessageTypes types = Load();

		Assert.Equal(new[] { "ATTACKUS", "DIPLOEXPIRE", "MYOWN" }, types.All.Select(type => type.Name));
		CMessageType attack = types.Find("ATTACKUS");
		Assert.True(attack.OnMap);
		Assert.True(attack.Log);                                     // no key: the default
		Assert.False(attack.Popup);                                  // the install's yes is gone with the rest
		Assert.Equal(EMessageCategory.Other, attack.Category);
	}

	[Fact]
	public void Written_as_the_original_writes_it_and_read_back_the_same()
	{
		CMessageTypes types = Load();
		types.Find("ATTACKUS").PausePopup = true;

		types.WriteCustom(Custom);

		string written = File.ReadAllText(Custom);
		Assert.StartsWith("ATTACKUS=\r\n{\r\nlog=yes\r\nonmap=no\r\npopup=yes\r\nidle=no\r\noption=no\r\npausepopup=yes\r\n"
			+ "categories=\"cat_combat\"\r\n}\r\nDIPLOEXPIRE=\r\n{\r\n", written);
		Assert.EndsWith("categories=\"cat_diplomacy\"\r\n}\r\n", written);
		Assert.True(Load().Find("ATTACKUS").PausePopup);
	}

	[Fact]
	public void Reset_deletes_the_file_and_reads_the_install_again_over_the_same_types()
	{
		File.WriteAllText(Custom, "ATTACKUS = { log = no }\nMYOWN = { log = no }\n");
		CMessageTypes types = Load();
		CMessageType attack = types.Find("ATTACKUS");

		types.Reset(CDirectorySettings.At(_root), Custom);

		Assert.False(File.Exists(Custom));
		Assert.Same(attack, types.Find("ATTACKUS"));                // a row's type is still the type
		Assert.True(attack.Log);
		Assert.True(attack.Popup);
		Assert.False(types.Find("MYOWN").Log);                     // only the player's file declared it
	}
}
