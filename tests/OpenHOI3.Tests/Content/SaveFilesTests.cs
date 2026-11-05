using System;
using System.IO;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The save dialog's names and list, and the autosave's days and files, as <c>docs/mechanics/ingamemenu.md</c>, section
/// 6, and <c>savegame.md</c>, section 11, have them.
/// </summary>
public class SaveFilesTests
{
	[Fact]
	public void The_name_offered_is_the_countrys_with_its_first_space_made_an_underscore_and_the_hour()
	{
		long Hours(int year, int month, int day, int hour) => new CDate(year, month, day).DayNumber * 24L + hour;

		Assert.Equal("Germany1936_01_01_00", CSaveFiles.OfferedName("Germany", Hours(1936, 1, 1, 0)));
		Assert.Equal("United_Kingdom1941_07_16_19", CSaveFiles.OfferedName("United Kingdom", Hours(1941, 7, 16, 19)));
		// Only the first space.
		Assert.Equal("Union_of South Africa1938_12_31_23", CSaveFiles.OfferedName("Union of South Africa", Hours(1938, 12, 31, 23)));
	}

	[Fact]
	public void A_typed_name_gets_its_extension_unless_its_last_dot_begins_it_exactly_and_an_empty_box_saves_nothing()
	{
		Assert.Null(CSaveFiles.FileName(""));
		Assert.Equal("Germany1936_01_01_00.hoi3", CSaveFiles.FileName("Germany1936_01_01_00"));
		Assert.Equal("mine.hoi3", CSaveFiles.FileName("mine.hoi3"));
		Assert.Equal("my.save.hoi3", CSaveFiles.FileName("my.save"));
		// The test minds case, and a dot that is the first character does not count.
		Assert.Equal("x.HOI3.hoi3", CSaveFiles.FileName("x.HOI3"));
		Assert.Equal(".hoi3.hoi3", CSaveFiles.FileName(".hoi3"));
		Assert.Equal("a.hoi3.bak.hoi3", CSaveFiles.FileName("a.hoi3.bak"));
		// The trim is thrown away: blanks are a name.
		Assert.Equal("   .hoi3", CSaveFiles.FileName("   "));
	}

	[Fact]
	public void A_row_shows_the_name_cut_at_its_first_extension()
	{
		Assert.Equal("autosave", CSaveFiles.RowText("autosave.hoi3"));
		Assert.Equal("a", CSaveFiles.RowText("a.hoi3.hoi3"));
		Assert.Equal("nodothoi3", CSaveFiles.RowText("nodothoi3"));
	}

	[Fact]
	public void The_list_is_newest_first_of_names_ending_hoi3_with_none_above_127_and_the_first_folders_kept()
	{
		string root = Path.Combine(Path.GetTempPath(), "openhoi3_savefiles_" + Guid.NewGuid().ToString("N"));
		string mine = Path.Combine(root, "mine");
		string install = Path.Combine(root, "install");
		Directory.CreateDirectory(mine);
		Directory.CreateDirectory(install);
		try
		{
			void Make(string folder, string name, int minutesAgo)
			{
				string path = Path.Combine(folder, name);
				File.WriteAllText(path, "");
				File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
			}
			Make(mine, "old.hoi3", 30);
			Make(mine, "new.hoi3", 1);
			Make(mine, "nodothoi3", 10);
			Make(mine, "notes.txt", 0);
			Make(mine, "Françe.hoi3", 0);
			Make(install, "shipped.hoi3", 20);
			Make(install, "old.hoi3", 0);
			Directory.CreateDirectory(Path.Combine(mine, "folder.hoi3"));

			Assert.Equal(new[] { "new.hoi3", "nodothoi3", "shipped.hoi3", "old.hoi3" }, CSaveFiles.List(new[] { mine, install }));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Each_frequency_is_due_on_its_own_days()
	{
		Assert.False(CAutosave.IsDue("NEVER", new CDate(1936, 1, 1)));
		Assert.True(CAutosave.IsDue("MONTHLY", new CDate(1936, 3, 1)));
		Assert.False(CAutosave.IsDue("MONTHLY", new CDate(1936, 3, 2)));
		Assert.True(CAutosave.IsDue("HALFYEAR", new CDate(1937, 7, 1)));
		Assert.False(CAutosave.IsDue("HALFYEAR", new CDate(1937, 4, 1)));
		Assert.True(CAutosave.IsDue("YEARLY", new CDate(1938, 1, 1)));
		Assert.False(CAutosave.IsDue("YEARLY", new CDate(1938, 7, 1)));
		Assert.True(CAutosave.IsDue("FIVE_YEAR", new CDate(1940, 1, 1)));
		Assert.False(CAutosave.IsDue("FIVE_YEAR", new CDate(1941, 1, 1)));
		// Weekly by the day's number from the calendar's start, less one: every seventh day, whatever the month.
		var due = new CDate(1936, 1, 1);
		while (!CAutosave.IsDue("WEEKLY", due))
		{
			due = due.AddDays(1);
		}
		Assert.Equal(0, (due.DayNumber - 1) % 7);
		Assert.True(CAutosave.IsDue("WEEKLY", due.AddDays(7)));
		Assert.False(CAutosave.IsDue("WEEKLY", due.AddDays(3)));
	}

	[Fact]
	public void An_autosave_shifts_the_three_files_along_and_drops_the_oldest()
	{
		string folder = Path.Combine(Path.GetTempPath(), "openhoi3_autosave_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(folder);
		try
		{
			File.WriteAllText(Path.Combine(folder, "autosave.hoi3"), "first");
			File.WriteAllText(Path.Combine(folder, "oldautosave.hoi3"), "second");
			File.WriteAllText(Path.Combine(folder, "olderautosave.hoi3"), "third");
			var norway = new OpenHOI3.Sim.Countries.CCountry { Tag = new("NOR"), Name = "Norway", DefinitionFile = "" };
			var state = new CGameState(new CDate(1936, 2, 1), new[] { new OpenHOI3.Sim.Provinces.CProvince(0, "", isSea: false) }, new[] { norway });

			string written = CAutosave.Write(state, key => key, folder);

			Assert.Equal(Path.Combine(folder, "autosave.hoi3"), written);
			Assert.StartsWith("date=\"1936.2.1.0\"", File.ReadAllText(written));
			Assert.Equal("first", File.ReadAllText(Path.Combine(folder, "oldautosave.hoi3")));
			Assert.Equal("second", File.ReadAllText(Path.Combine(folder, "olderautosave.hoi3")));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
