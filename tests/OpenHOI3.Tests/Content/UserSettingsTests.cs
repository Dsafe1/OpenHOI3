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
}
