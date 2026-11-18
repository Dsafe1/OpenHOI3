using System;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Content.Logs;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The game's own log: the player's folder's <c>logs</c>, this run's file freed by moving the runs before it one place
/// back, four kept, against <c>docs/mechanics/logs.md</c>.
/// </summary>
public sealed class LogFilesTests : IDisposable
{
	private readonly string _folder = Path.Combine(Path.GetTempPath(), "ohoi-logs-" + Guid.NewGuid().ToString("N"));

	public void Dispose()
	{
		if (Directory.Exists(_folder))
		{
			Directory.Delete(_folder, recursive: true);
		}
	}

	[Fact]
	public void TheFolderIsThePlayersFoldersLogs()
	{
		CDirectorySettings directories = CDirectorySettings.At(_folder);
		Assert.Equal(Path.Combine(directories.UserFolder, "logs"), CLogFiles.Folder(directories));
	}

	[Fact]
	public void AFirstRunMakesTheFolderAndWritesOpenhoi3Log()
	{
		string path = CLogFiles.Start(_folder);
		Assert.True(Directory.Exists(_folder));
		Assert.Equal(Path.Combine(_folder, "openhoi3.log"), path);
	}

	[Fact]
	public void EachRunMovesTheOnesBeforeItBackAndKeepsFour()
	{
		for (int run = 1; run <= 6; run++)
		{
			File.WriteAllText(CLogFiles.Start(_folder), "run " + run);
		}
		Assert.Equal("run 6", File.ReadAllText(Path.Combine(_folder, "openhoi3.log")));
		Assert.Equal("run 5", File.ReadAllText(Path.Combine(_folder, "openhoi3_1.log")));
		Assert.Equal("run 2", File.ReadAllText(Path.Combine(_folder, "openhoi3_4.log")));
		Assert.False(File.Exists(Path.Combine(_folder, "openhoi3_5.log")));
	}

	[Fact]
	public void TheOriginalsOwnLogsAreLeftAlone()
	{
		Directory.CreateDirectory(_folder);
		File.WriteAllText(Path.Combine(_folder, "game.log"), "the original's");
		CLogFiles.Start(_folder);
		Assert.Equal("the original's", File.ReadAllText(Path.Combine(_folder, "game.log")));
	}
}
