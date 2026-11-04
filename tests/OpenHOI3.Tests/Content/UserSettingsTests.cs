using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Settings;

namespace OpenHOI3.Tests.Content;

/// <summary>The player's <c>settings.txt</c>, against <c>docs/mechanics/settings.md</c>.</summary>
public class UserSettingsTests : IDisposable
{
	private readonly string _path = Path.Combine(Path.GetTempPath(), "openhoi3-settings-" + Guid.NewGuid().ToString("N") + ".txt");

	public void Dispose()
	{
		File.Delete(_path);
	}

	[Fact]
	public void The_file_s_map_options_and_language_are_read()
	{
		File.WriteAllText(_path, "gui=\n{\nlanguage=l_german\n}\nmapRenderingOptions=\n{\nrenderTrees=no\nalwaysCounters=yes\n"
			+ "counter_distance=250.000000\ncounter_scale=2.000000\n}\nautosave=\"MONTHLY\"\n");
		var warnings = new List<string>();
		CUserSettings settings = CUserSettingsReader.Read(_path, warnings.Add);
		Assert.Equal("l_german", settings.Language);
		Assert.True(settings.AlwaysCounters);
		Assert.Equal(2f, settings.CounterScale);
		Assert.Equal(250f, settings.CounterDistance);
		Assert.Empty(warnings);
	}

	[Fact]
	public void Counters_turned_off_are_reported_as_not_supported_and_no_file_gives_the_defaults()
	{
		File.WriteAllText(_path, "mapRenderingOptions=\n{\nalwaysCounters=no\n}\n");
		var warnings = new List<string>();
		Assert.False(CUserSettingsReader.Read(_path, warnings.Add).AlwaysCounters);
		Assert.Contains("not supported", Assert.Single(warnings));

		CUserSettings none = CUserSettingsReader.Read(_path + ".missing", warnings.Add);
		Assert.Same(CUserSettings.Default, none);
		Assert.Equal(1f, none.CounterScale);
		Assert.Equal(100f, none.CounterDistance);
	}

	[Fact]
	public void The_outliner_s_sections_are_read_whole_and_with_no_key_are_open_with_the_units()
	{
		File.WriteAllText(_path, "outliner=\n{\n2 3 7 8 9 1 10 }\n");
		Assert.Equal(new[] { 2, 3, 7, 8, 9, 1, 10 }, CUserSettingsReader.Read(_path).Outliner);

		File.WriteAllText(_path, "gui=\n{\nlanguage=l_english\n}\n");
		Assert.Equal(new[] { 10, 4, 5, 6 }, CUserSettingsReader.Read(_path).Outliner);
		Assert.Equal(new[] { 10, 4, 5, 6 }, CUserSettings.Default.Outliner);
	}

	[Fact]
	public void Written_in_the_originals_order_and_format_and_read_back_the_same()
	{
		CUserSettings settings = CUserSettings.Default with { Width = 1920, Height = 1080, RefreshRate = 144, MasterVolume = 55f, Autosave = "MONTHLY" };

		string text = CUserSettingsWriter.Write(settings);

		Assert.StartsWith("gui=\r\n{\r\nlanguage=l_english\r\n}\r\ngraphics=\r\n{\r\nsize=\r\n{\r\n\tx=1920\r\n\ty=1080\r\n}\r\n\r\n"
			+ "refreshRate=144\r\nfullScreen=yes\r\nshadows=no\r\nshadowSize=2048\r\nmulti_sampling=0\r\nanisotropic_filtering=0\r\n"
			+ "gamma=50.000000\r\n}\r\nsound_fx_volume=100.000000\r\n", text);
		Assert.Contains("master_volume=55.000000\r\nambient_volume=50.000000\r\nmapRenderingOptions=\r\n{\r\nrenderTrees=yes\r\n", text);
		Assert.EndsWith("autosave=\"MONTHLY\"\r\nsimple=yes\r\nuse_legacy_camera=no\r\ncategories=\r\n{\r\n0 0 0 0 0 0 }\r\n"
			+ "outliner=\r\n{\r\n10 4 5 6 }\r\n", text);
		File.WriteAllText(_path, text);
		Assert.Equal(text, CUserSettingsWriter.Write(CUserSettingsReader.Read(_path) with { Path = null }));
	}

	[Fact]
	public void The_keys_off_their_defaults_are_written_only_then_and_a_small_size_is_raised()
	{
		string plain = CUserSettingsWriter.Write(CUserSettings.Default);
		Assert.DoesNotContain("pause_on_event", plain);
		Assert.DoesNotContain("vsync", plain);
		Assert.DoesNotContain("adapter", plain);

		string off = CUserSettingsWriter.Write(CUserSettings.Default with
		{
			PauseOnEvent = false, Vsync = false, Adapter = 1, DebugMonitor = true, Categories = Array.Empty<int>(),
		});
		Assert.Contains("gamma=50.000000\r\nadapter=1\r\nvsync=no\r\n}\r\n", off);   // debugmonitor only with adapter 0
		Assert.Contains("ambient_volume=50.000000\r\npause_on_event=no\r\n", off);
		Assert.DoesNotContain("categories", off);

		File.WriteAllText(_path, "graphics=\n{\nsize=\n{\nx=800\ny=600\n}\nshadowSize=9000\n}\nautosave=\"SOMETIMES\"\n");
		var warnings = new List<string>();
		CUserSettings read = CUserSettingsReader.Read(_path, warnings.Add);
		Assert.Equal((1024, 768, 4096), (read.Width, read.Height, read.ShadowSize));
		Assert.Equal("YEARLY", read.Autosave);
		Assert.Single(warnings);
	}
}
